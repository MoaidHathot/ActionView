namespace ActionView.Core.Models;

/// <summary>
/// Defines the canonical shape for a specific entry type.
/// Templates are used to normalize AI-generated entries so
/// entries of the same type are consistently structured.
/// </summary>
public sealed class EntryTemplate
{
    /// <summary>Entry type this template applies to (e.g., "pr-review").</summary>
    public required string Type { get; set; }

    /// <summary>Human-readable description of what this entry type represents.</summary>
    public string? Description { get; set; }

    /// <summary>Default values applied when the entry does not specify them.</summary>
    public EntryDefaults Defaults { get; set; } = new();

    /// <summary>Expected content blocks in canonical order.</summary>
    public List<ContentTemplateBlock> ContentTemplate { get; set; } = [];

    /// <summary>Expected entry-level actions.</summary>
    public List<ActionTemplateBlock> ExpectedActions { get; set; } = [];

    /// <summary>
    /// Optional map of alternative tag spellings to canonical tags (case-insensitive),
    /// analogous to <see cref="ContentTemplateBlock.KeyAliases"/>. Keeps tag spelling
    /// consistent across submitters so saved views / filters stay reliable.
    /// E.g. { "back-end": "backend", "perf": "performance" }.
    /// </summary>
    public Dictionary<string, string>? TagAliases { get; set; }

    /// <summary>How to normalize tag casing during ingest. Default: none.</summary>
    public TagCaseMode TagCaseMode { get; set; } = TagCaseMode.None;

    /// <summary>
    /// Optional allow-list of canonical tags for this type. Tags outside the set are
    /// <em>flagged</em> as a <c>tag.notAllowed</c> warning (surfaced by validation) but
    /// never silently dropped, so information is not lost.
    /// </summary>
    public List<string>? AllowedTags { get; set; }

    /// <summary>
    /// When true, entries of this type are held to the template strictly: validation
    /// warnings (e.g. a missing required content block) become blocking errors at ingest,
    /// even when the global <c>ingest.strict</c> default is false.
    /// </summary>
    public bool Strict { get; set; }
}

/// <summary>How tag casing is normalized during ingest.</summary>
public enum TagCaseMode
{
    /// <summary>Leave tag casing as authored.</summary>
    None,

    /// <summary>Lower-case all tags (invariant culture).</summary>
    Lower
}

/// <summary>
/// Default field values applied to entries matching this template
/// when the entry does not already provide them.
/// </summary>
public sealed class EntryDefaults
{
    public string? Icon { get; set; }
    public Severity? Severity { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Describes an expected content block within a template.
/// Used for normalization and validation of incoming entries.
/// </summary>
public sealed class ContentTemplateBlock
{
    /// <summary>Block type (markdown, keyValue, section, etc.).</summary>
    public required ContentBlockType Type { get; set; }

    /// <summary>Expected label for this block.</summary>
    public string? Label { get; set; }

    /// <summary>Whether this block is required.</summary>
    public bool Required { get; set; }

    /// <summary>For keyValue blocks: keys that should be present.</summary>
    public List<string>? RequiredKeys { get; set; }

    /// <summary>
    /// For keyValue blocks: maps alternative key names to canonical names.
    /// Keys are case-insensitive. E.g., {"repo": "Repository", "pr_number": "PR Number"}
    /// </summary>
    public Dictionary<string, string>? KeyAliases { get; set; }

    /// <summary>For section blocks: expected section title.</summary>
    public string? Title { get; set; }

    /// <summary>For section blocks: alternative titles that should be normalized to Title.</summary>
    public List<string>? TitleAliases { get; set; }
}

/// <summary>
/// Describes an expected action button for template documentation.
/// </summary>
public sealed class ActionTemplateBlock
{
    public required string Label { get; set; }
    public ActionStyle Style { get; set; } = ActionStyle.Default;
}
