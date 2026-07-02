namespace ActionView.Core.Models;

/// <summary>Severity of a single validation finding.</summary>
public enum ValidationSeverity
{
    /// <summary>Blocks a strict ingest and marks the result not-ok.</summary>
    Error,

    /// <summary>Surfaced for the caller to fix, but does not block a non-strict ingest.</summary>
    Warning
}

/// <summary>
/// A single validation finding. Deliberately small so a producer (LLM or otherwise)
/// can retry against precise, machine-readable feedback without re-reading the whole
/// schema or paying to echo large payloads back.
/// </summary>
public sealed class ValidationDiagnostic
{
    /// <summary>
    /// JSON Pointer to the offending location, e.g. <c>/content/3/type</c>.
    /// Empty string means the document root.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Stable machine code, e.g. <c>schema.enum</c>, <c>schema.required</c>,
    /// <c>block.missingRequired</c>, <c>tag.notAllowed</c>, <c>json.parse</c>.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>Human-readable, retry-actionable message.</summary>
    public required string Message { get; set; }

    /// <summary>Whether this is a blocking error or an advisory warning.</summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}

/// <summary>Options controlling how an entry is validated.</summary>
public sealed class EntryValidationOptions
{
    /// <summary>
    /// Promote warnings (missing required content blocks, disallowed tags) to errors,
    /// so the result is not-ok unless the entry fully conforms to its template.
    /// </summary>
    public bool Strict { get; set; }

    /// <summary>
    /// Include the normalized entry in the result. Off by default to keep the response
    /// small — echoing a large entry back defeats the point of cheap validation.
    /// </summary>
    public bool IncludeNormalized { get; set; }

    /// <summary>Maximum diagnostics (errors + warnings) returned, to bound response size.</summary>
    public int MaxDiagnostics { get; set; } = 50;

    public static EntryValidationOptions Default => new();
}

/// <summary>Compact, structured result of validating a candidate entry.</summary>
public sealed class EntryValidationResult
{
    /// <summary>True when there are no errors (in strict mode, no warnings either).</summary>
    public bool Ok { get; set; }

    /// <summary>Blocking problems. Fix these before the entry can be ingested.</summary>
    public List<ValidationDiagnostic> Errors { get; set; } = [];

    /// <summary>Advisory problems. Non-blocking unless strict mode is on.</summary>
    public List<ValidationDiagnostic> Warnings { get; set; } = [];

    /// <summary>Number of diagnostics dropped to satisfy <see cref="EntryValidationOptions.MaxDiagnostics"/>, if any.</summary>
    public int? Truncated { get; set; }

    /// <summary>The normalized entry — only populated when <see cref="EntryValidationOptions.IncludeNormalized"/> is set.</summary>
    public Entry? Normalized { get; set; }
}
