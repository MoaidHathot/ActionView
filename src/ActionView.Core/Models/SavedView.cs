namespace ActionView.Core.Models;

/// <summary>
/// A named, saved filter preset ("view") used to group the active entry feed
/// into lanes such as Work vs. Personal. A view is just a stored
/// <see cref="EntryFilters"/>-style selection: an optional entry
/// <see cref="Type"/> (the "template") and/or a set of <see cref="Tags"/>.
///
/// Views are configured in actionview.json under the <c>views</c> key and can
/// also be created/edited from the dashboard UI. The always-present "All" view
/// (no filter, shows everything) is synthesized by the client and is never
/// persisted here.
/// </summary>
public sealed class SavedView
{
    /// <summary>Stable identifier (slug). Auto-derived from <see cref="Name"/> when omitted.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable label shown on the view pill (e.g., "Work").</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional entry type ("template") to filter by. When set, only entries of
    /// this type are shown. Null/empty means the view does not constrain by type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Tags to filter by, matched with OR semantics (an entry is included if it
    /// carries any of these tags). Empty means the view does not constrain by tag.
    /// </summary>
    public List<string> Tags { get; set; } = [];
}
