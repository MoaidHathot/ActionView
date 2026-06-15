namespace ActionView.Core.Models;

/// <summary>
/// A parsed set of entry filters shared by the API, MCP, and CLI so filtering
/// semantics stay identical across every surface.
/// </summary>
public sealed class FilterCriteria
{
    /// <summary>Entry type ("template") to match, case-insensitive. Null = any.</summary>
    public string? Type { get; set; }

    /// <summary>Exact severity to match. Null = any.</summary>
    public Severity? Severity { get; set; }

    /// <summary>Producing source/system to match, case-insensitive. Null = any.</summary>
    public string? Source { get; set; }

    /// <summary>Tags to match, combined per <see cref="TagMode"/>. Empty = no tag constraint.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>How <see cref="Tags"/> combine (Any/OR or All/AND).</summary>
    public TagMatchMode TagMode { get; set; } = TagMatchMode.Any;

    /// <summary>Free-text search across title/subtitle/source/type/tags. Null/empty = no search.</summary>
    public string? Search { get; set; }

    /// <summary>When true, search also matches the archived outcome action (history).</summary>
    public bool IncludeOutcomeInSearch { get; set; }
}
