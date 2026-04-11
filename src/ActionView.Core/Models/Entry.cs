namespace ActionView.Core.Models;

/// <summary>
/// The root model for an ActionView entry.
/// This is the JSON contract that external orchestration tools produce.
/// </summary>
public sealed class Entry
{
    /// <summary>Unique identifier. Auto-generated if not provided.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Schema version for forward compatibility. Current: "1".</summary>
    public string SchemaVersion { get; set; } = "1";

    /// <summary>Category for grouping/filtering (e.g., "pr-review", "incident", "deploy").</summary>
    public required string Type { get; set; }

    /// <summary>Which orchestration tool created this entry.</summary>
    public required string Source { get; set; }

    /// <summary>When the entry was created by the orchestration tool.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // --- Header (shown in list view) ---

    /// <summary>Primary display text.</summary>
    public required string Title { get; set; }

    /// <summary>Secondary display text.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Urgency level.</summary>
    public Severity Severity { get; set; } = Severity.Medium;

    /// <summary>Icon name from the lucide icon set.</summary>
    public string? Icon { get; set; }

    /// <summary>Tags for filtering.</summary>
    public List<string> Tags { get; set; } = [];

    // --- Content (rendered in detail view) ---

    /// <summary>Ordered list of content blocks to render.</summary>
    public List<ContentBlock> Content { get; set; } = [];

    // --- Actions ---

    /// <summary>Entry-level actions the user can take.</summary>
    public List<EntryAction> Actions { get; set; } = [];

    // --- Grouping & Priority ---

    /// <summary>Optional group identifier for related entries (e.g., same CI run, same repo).</summary>
    public string? GroupId { get; set; }

    /// <summary>Display label for the group (e.g., "CI Run #1847").</summary>
    public string? GroupLabel { get; set; }

    /// <summary>Whether this entry is pinned to the top of the list.</summary>
    public bool Pinned { get; set; }

    /// <summary>Priority for ordering. Higher values appear first (default 0).</summary>
    public int Priority { get; set; }

    // --- Provenance (optional, set by external tools for traceability) ---

    /// <summary>
    /// Free-form metadata bag for provenance tracking.
    /// Allows external tools to attach arbitrary key-value data
    /// (e.g., orchestrationId, runId, correlationId, environment).
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    // --- Metadata (managed by the backend, not set by external tools) ---

    /// <summary>Current status of the entry in the pipeline.</summary>
    public EntryStatus Status { get; set; } = EntryStatus.Pending;

    /// <summary>When the backend picked up this entry from the inbox.</summary>
    public DateTimeOffset? ReceivedAt { get; set; }

    /// <summary>When the user first viewed this entry.</summary>
    public DateTimeOffset? ViewedAt { get; set; }

    /// <summary>Outcome information, populated when the entry is archived.</summary>
    public EntryOutcome? Outcome { get; set; }
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

public enum EntryStatus
{
    Pending,
    Viewed,
    Archived
}
