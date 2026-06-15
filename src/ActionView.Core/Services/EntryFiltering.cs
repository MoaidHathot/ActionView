using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Shared, testable entry-filtering helpers used by the API, MCP, and CLI so
/// tag-matching and filter semantics stay consistent across them.
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

    /// <summary>Builds a <see cref="FilterCriteria"/> from raw (query-string) values.</summary>
    public static FilterCriteria ParseCriteria(
        string? type, string? severity, string? source, string? tags, string? tagMode, string? search,
        TagMatchMode defaultTagMode, bool includeOutcomeInSearch = false)
    {
        var criteria = new FilterCriteria
        {
            Type = string.IsNullOrWhiteSpace(type) ? null : type.Trim(),
            Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            TagMode = ParseTagMode(tagMode, defaultTagMode),
            IncludeOutcomeInSearch = includeOutcomeInSearch,
        };

        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Severity>(severity, true, out var sev))
            criteria.Severity = sev;

        if (!string.IsNullOrWhiteSpace(tags))
            criteria.Tags = [.. tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        return criteria;
    }

    /// <summary>Builds the filter criteria represented by a saved view.</summary>
    public static FilterCriteria CriteriaForView(SavedView view, TagMatchMode defaultTagMode) => new()
    {
        Type = string.IsNullOrWhiteSpace(view.Type) ? null : view.Type.Trim(),
        Tags = view.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList() ?? [],
        TagMode = view.TagMatch ?? defaultTagMode,
    };

    /// <summary>
    /// Resolves filter criteria honouring an optional saved <paramref name="view"/>
    /// (matched by id or name). When a view matches, it supplies type+tags+mode and
    /// the non-view dimensions (severity/source/search) are layered on top;
    /// otherwise the raw parameters are parsed directly. Unknown views fall back
    /// to the raw parameters so callers stay forgiving.
    /// </summary>
    public static FilterCriteria ResolveCriteria(
        IReadOnlyList<SavedView> views, TagMatchMode defaultTagMode, string? view,
        string? type, string? severity, string? source, string? tags, string? tagMode, string? search,
        bool includeOutcomeInSearch = false)
    {
        SavedView? matched = null;
        if (!string.IsNullOrWhiteSpace(view))
        {
            matched = views.FirstOrDefault(v =>
                v.Id.Equals(view, StringComparison.OrdinalIgnoreCase) ||
                v.Name.Equals(view, StringComparison.OrdinalIgnoreCase));
        }

        if (matched is null)
            return ParseCriteria(type, severity, source, tags, tagMode, search, defaultTagMode, includeOutcomeInSearch);

        var criteria = CriteriaForView(matched, defaultTagMode);
        criteria.IncludeOutcomeInSearch = includeOutcomeInSearch;

        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Severity>(severity, true, out var sev))
            criteria.Severity = sev;
        if (!string.IsNullOrWhiteSpace(source))
            criteria.Source = source.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            criteria.Search = search.Trim();

        return criteria;
    }

    /// <summary>Applies every dimension of <paramref name="criteria"/> to the sequence.</summary>
    public static IEnumerable<Entry> Apply(IEnumerable<Entry> entries, FilterCriteria criteria)
    {
        var query = entries;

        if (!string.IsNullOrWhiteSpace(criteria.Type))
            query = query.Where(e => e.Type.Equals(criteria.Type, StringComparison.OrdinalIgnoreCase));

        if (criteria.Severity.HasValue)
            query = query.Where(e => e.Severity == criteria.Severity.Value);

        if (!string.IsNullOrWhiteSpace(criteria.Source))
            query = query.Where(e => e.Source.Equals(criteria.Source, StringComparison.OrdinalIgnoreCase));

        if (criteria.Tags.Count > 0)
            query = query.Where(e => MatchesTags(e, criteria.Tags, criteria.TagMode));

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var s = criteria.Search;
            query = query.Where(e =>
                e.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (e.Subtitle?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.Source.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.Type.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.Tags.Any(t => t.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                (criteria.IncludeOutcomeInSearch && (e.Outcome?.Action.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)));
        }

        return query;
    }
}
