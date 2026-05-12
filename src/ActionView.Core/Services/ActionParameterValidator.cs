using System.Globalization;
using System.Text.RegularExpressions;
using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Validates and normalizes a user-supplied parameter dictionary against an action's
/// declared <see cref="ActionParameter"/> list.
///
/// Rules:
/// <list type="bullet">
///   <item>Required parameters must be present and non-empty.</item>
///   <item>Numeric parameters must parse as <see cref="double"/>.</item>
///   <item>Boolean parameters must parse as <see cref="bool"/>; coerced to <c>"true"</c>/<c>"false"</c>.</item>
///   <item>Select parameters must match one of <see cref="ActionParameter.Options"/>.</item>
///   <item>Parameter names must match <c>[A-Za-z_][A-Za-z0-9_]*</c>.</item>
///   <item>Unknown keys (not declared on the action) are rejected to surface typos early.</item>
/// </list>
/// </summary>
public static partial class ActionParameterValidator
{
    /// <summary>
    /// Validates and returns a normalized dictionary safe to feed into the resolvers.
    /// </summary>
    /// <returns>
    /// (errors, normalized) — when <c>errors</c> is non-empty, <c>normalized</c> is null.
    /// </returns>
    public static (IReadOnlyList<string> Errors, IReadOnlyDictionary<string, string>? Normalized) Validate(
        IReadOnlyList<ActionParameter>? declared,
        IReadOnlyDictionary<string, string>? supplied)
    {
        declared ??= [];
        supplied ??= new Dictionary<string, string>(StringComparer.Ordinal);

        var errors = new List<string>();
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        // Validate declared shape first (cheap defense against malformed templates).
        foreach (var param in declared)
        {
            if (string.IsNullOrWhiteSpace(param.Name) || !NamePattern().IsMatch(param.Name))
            {
                errors.Add($"Action parameter has invalid name: '{param.Name}'");
                continue;
            }
        }

        // Reject any supplied key that wasn't declared — surfaces typos and prevents
        // accidental injection of placeholders the action author never intended.
        foreach (var key in supplied.Keys)
        {
            if (!declared.Any(p => string.Equals(p.Name, key, StringComparison.Ordinal)))
                errors.Add($"Unknown parameter '{key}'.");
        }

        foreach (var param in declared)
        {
            supplied.TryGetValue(param.Name, out var raw);
            var value = raw ?? param.Default;

            if (string.IsNullOrEmpty(value))
            {
                if (param.Required)
                {
                    errors.Add($"Parameter '{param.Name}' is required.");
                    continue;
                }
                // Optional with no value: substitute empty string so {{param.X}} disappears.
                normalized[param.Name] = string.Empty;
                continue;
            }

            switch (param.Type)
            {
                case ActionParameterType.Number:
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        errors.Add($"Parameter '{param.Name}' must be a number (got '{value}').");
                    else
                        normalized[param.Name] = value;
                    break;

                case ActionParameterType.Boolean:
                    if (!bool.TryParse(value, out var b))
                        errors.Add($"Parameter '{param.Name}' must be true or false (got '{value}').");
                    else
                        normalized[param.Name] = b ? "true" : "false";
                    break;

                case ActionParameterType.Select:
                    if (param.Options is null || param.Options.Count == 0)
                    {
                        errors.Add($"Parameter '{param.Name}' is a select but has no options.");
                    }
                    else if (!param.Options.Contains(value, StringComparer.Ordinal))
                    {
                        errors.Add($"Parameter '{param.Name}' value '{value}' is not in allowed options [{string.Join(", ", param.Options)}].");
                    }
                    else
                    {
                        normalized[param.Name] = value;
                    }
                    break;

                case ActionParameterType.Text:
                case ActionParameterType.Multiline:
                default:
                    normalized[param.Name] = value;
                    break;
            }
        }

        return errors.Count > 0
            ? (errors, null)
            : (errors, normalized);
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex NamePattern();
}
