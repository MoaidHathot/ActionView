using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Composes filtering and sorting into a single query shared by the active and
/// history surfaces. Pagination (skip/take) is left to the caller so it can be
/// applied after filtering — which is exactly what keeps history filters global
/// rather than limited to the first page.
/// </summary>
public static class EntryQuery
{
    /// <summary>
    /// Active-feed query: filter, then either the canonical default order or an
    /// explicit sort field (with pinned entries floating to the top).
    /// </summary>
    public static List<Entry> RunActive(
        IEnumerable<Entry> entries, FilterCriteria criteria, EntrySortField? sort, SortDirection dir)
    {
        var filtered = EntryFiltering.Apply(entries, criteria);
        return sort is null
            ? EntrySorting.Default(filtered)
            : EntrySorting.Sort(filtered, sort.Value, dir, pinnedFirst: true);
    }

    /// <summary>
    /// History query: filter, then either the default most-recent-outcome order
    /// or an explicit sort field. Pins do not float in history.
    /// </summary>
    public static List<Entry> RunHistory(
        IEnumerable<Entry> entries, FilterCriteria criteria, EntrySortField? sort, SortDirection dir)
    {
        var filtered = EntryFiltering.Apply(entries, criteria);
        return sort is null
            ? filtered.OrderByDescending(e => e.Outcome?.Timestamp ?? e.CreatedAt).ToList()
            : EntrySorting.Sort(filtered, sort.Value, dir, pinnedFirst: false);
    }
}
