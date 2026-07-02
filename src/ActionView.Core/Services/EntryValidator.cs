using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using Json.Schema;

namespace ActionView.Core.Services;

/// <summary>
/// Validates candidate entry JSON against the published schema and its type template
/// without persisting anything.
///
/// This is the shared "retry oracle": a producer (LLM or otherwise) submits best-effort
/// JSON, gets back precise, compact, machine-readable diagnostics, fixes them, and
/// resubmits — instead of reasoning about the full schema up front (which is expensive
/// and unreliable for large entries). The same pipeline backs the CLI <c>validate</c>
/// command, the MCP <c>validate_entry</c> tool, the REST <c>/validate</c> endpoint, and
/// strict ingest.
/// </summary>
public sealed class EntryValidator
{
    private readonly EntryNormalizer? _normalizer;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonDocumentOptions NodeParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly EvaluationOptions SchemaEvaluationOptions = new()
    {
        OutputFormat = OutputFormat.List
    };

    public EntryValidator(EntryNormalizer? normalizer = null)
    {
        _normalizer = normalizer;
    }

    /// <summary>Validate a candidate entry from raw JSON text.</summary>
    public EntryValidationResult Validate(string rawJson, EntryValidationOptions? options = null)
    {
        options ??= EntryValidationOptions.Default;
        var result = new EntryValidationResult();

        // 1. Parse (also validates that the input is well-formed JSON).
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawJson, NodeParseOptions);
        }
        catch (JsonException ex)
        {
            result.Errors.Add(new ValidationDiagnostic
            {
                Path = "",
                Code = "json.parse",
                Message = $"Invalid JSON: {ex.Message}"
            });
            return Finalize(result, options);
        }

        using (document)
        {
            var root = document.RootElement;

            // 2. Schema validation — precise JSON-Pointer paths + readable messages.
            var evaluation = EntrySchemaProvider.Schema.Evaluate(root, SchemaEvaluationOptions);
            if (!evaluation.IsValid)
            {
                foreach (var detail in Flatten(evaluation))
                {
                    if (detail.Errors is not { Count: > 0 })
                        continue;

                    foreach (var (keyword, message) in detail.Errors)
                    {
                        result.Errors.Add(new ValidationDiagnostic
                        {
                            Path = detail.InstanceLocation.ToString(),
                            Code = $"schema.{keyword}",
                            Message = message
                        });
                    }
                }
            }
        }

        // 3. Deserialize + normalize. Normalization yields template findings (missing
        //    required blocks, disallowed tags) as warnings.
        Entry? entry = null;
        try
        {
            entry = JsonSerializer.Deserialize<Entry>(rawJson, ReadOptions);
        }
        catch (JsonException ex)
        {
            // Only surface a bind error when the schema stage didn't already explain it,
            // to avoid duplicate noise for the same underlying problem.
            if (result.Errors.Count == 0)
            {
                result.Errors.Add(new ValidationDiagnostic
                {
                    Path = "",
                    Code = "deserialize",
                    Message = $"Entry could not be bound: {ex.Message}"
                });
            }
        }

        var effectiveStrict = options.Strict;

        if (entry is not null)
        {
            effectiveStrict = effectiveStrict || (_normalizer?.IsStrictType(entry.Type) ?? false);

            var findings = _normalizer?.Normalize(entry) ?? [];
            result.Warnings.AddRange(findings);

            if (options.IncludeNormalized)
                result.Normalized = entry;
        }

        // 4. Strict promotion: warnings become blocking errors.
        if (effectiveStrict && result.Warnings.Count > 0)
        {
            foreach (var warning in result.Warnings)
            {
                warning.Severity = ValidationSeverity.Error;
                result.Errors.Add(warning);
            }
            result.Warnings.Clear();
        }

        return Finalize(result, options);
    }

    /// <summary>
    /// Formats a validation result as a compact, human-readable block suitable for an
    /// <c>errors/*.error.txt</c> companion file or a log line.
    /// </summary>
    public static string FormatDiagnostics(EntryValidationResult result)
    {
        var sb = new StringBuilder();

        if (result.Errors.Count > 0)
        {
            sb.AppendLine($"{result.Errors.Count} error(s):");
            foreach (var e in result.Errors)
                sb.AppendLine($"  [{e.Code}] {Loc(e.Path)}: {e.Message}");
        }

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine($"{result.Warnings.Count} warning(s):");
            foreach (var w in result.Warnings)
                sb.AppendLine($"  [{w.Code}] {Loc(w.Path)}: {w.Message}");
        }

        if (result.Truncated is > 0)
            sb.AppendLine($"  ...and {result.Truncated} more.");

        return sb.ToString().TrimEnd();

        static string Loc(string path) => string.IsNullOrEmpty(path) ? "(root)" : path;
    }

    private static EntryValidationResult Finalize(EntryValidationResult result, EntryValidationOptions options)
    {
        // Bound the number of diagnostics so the response stays token-cheap. Keep errors
        // first (they block), then warnings.
        var total = result.Errors.Count + result.Warnings.Count;
        if (total > options.MaxDiagnostics)
        {
            result.Truncated = total - options.MaxDiagnostics;

            var keepErrors = Math.Min(result.Errors.Count, options.MaxDiagnostics);
            var keepWarnings = Math.Max(0, options.MaxDiagnostics - keepErrors);

            if (result.Errors.Count > keepErrors)
                result.Errors = result.Errors.Take(keepErrors).ToList();
            if (result.Warnings.Count > keepWarnings)
                result.Warnings = result.Warnings.Take(keepWarnings).ToList();
        }

        result.Ok = result.Errors.Count == 0;
        return result;
    }

    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults results)
    {
        yield return results;

        if (results.Details is null)
            yield break;

        foreach (var child in results.Details)
            foreach (var descendant in Flatten(child))
                yield return descendant;
    }
}
