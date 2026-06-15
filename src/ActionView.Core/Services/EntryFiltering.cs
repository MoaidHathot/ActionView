using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Shared, testable entry-filtering helpers used by the API and history
/// endpoints so tag-matching semantics stay consistent across them.
/// </summary>
public static class EntryFiltering
{
    /// <summary>Parses a tag-mode string ("all"/"any") into a <see cref="TagMatchMode"/>.</summary>
    public static TagMatchMode ParseTagMode(string? value, TagMatchMode fallback) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "all" => TagMatchMode.All,
            "any" => TagMatchMode.Any,
            _ => fallback,
        };

    /// <summary>
    /// Returns true when <paramref name="entry"/> matches <paramref name="tags"/>
    /// under the given <paramref name="mode"/>. An empty tag set always matches.
    /// </summary>
    public static bool MatchesTags(Entry entry, IReadOnlyCollection<string> tags, TagMatchMode mode)
    {
        if (tags.Count == 0) return true;

        return mode == TagMatchMode.All
            ? tags.All(t => entry.Tags.Contains(t, StringComparer.OrdinalIgnoreCase))
            : tags.Any(t => entry.Tags.Contains(t, StringComparer.OrdinalIgnoreCase));
    }
}
