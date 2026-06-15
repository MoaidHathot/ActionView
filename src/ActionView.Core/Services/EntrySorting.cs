using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Shared, testable entry-ordering helpers. Used by the API/history endpoints to
/// honour a user-selected sort field + direction while keeping a deterministic
/// tie-breaker.
/// </summary>
public static class EntrySorting
{
    /// <summary>Parses a sort-field string. Returns null for unknown/empty values.</summary>
    public static EntrySortField? TryParseField(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "created" or "createdat" or "date" or "time" => EntrySortField.Created,
            "priority" => EntrySortField.Priority,
            "severity" => EntrySortField.Severity,
            "title" => EntrySortField.Title,
            "outcome" or "outcometime" => EntrySortField.Outcome,
            _ => null,
        };

    /// <summary>Parses a direction string ("asc"/"desc"); falls back when unknown.</summary>
    public static SortDirection ParseDirection(string? value, SortDirection fallback = SortDirection.Descending) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "asc" or "ascending" => SortDirection.Ascending,
            "desc" or "descending" => SortDirection.Descending,
            _ => fallback,
        };

    /// <summary>
    /// Orders entries by <paramref name="field"/>/<paramref name="dir"/>. When
    /// <paramref name="pinnedFirst"/> is true, pinned entries float to the top
    /// regardless of the chosen field. A stable id tie-breaker keeps ordering
    /// deterministic across equal keys.
    /// </summary>
    public static List<Entry> Sort(
        IEnumerable<Entry> entries, EntrySortField field, SortDirection dir, bool pinnedFirst)
    {
        var asc = dir == SortDirection.Ascending;

        IOrderedEnumerable<Entry> ordered = pinnedFirst
            ? ApplyField(entries.OrderByDescending(e => e.Pinned), field, asc)
            : ApplyField(entries, field, asc);

        return ordered.ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
    }

    // Starts a fresh ordering from an unordered sequence.
    private static IOrderedEnumerable<Entry> ApplyField(IEnumerable<Entry> source, EntrySortField field, bool asc) =>
        field switch
        {
            EntrySortField.Priority => asc
                ? source.OrderBy(e => e.Priority)
                : source.OrderByDescending(e => e.Priority),
            EntrySortField.Severity => asc
                ? source.OrderBy(e => e.Severity)
                : source.OrderByDescending(e => e.Severity),
            EntrySortField.Title => asc
                ? source.OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(e => e.Title, StringComparer.OrdinalIgnoreCase),
            EntrySortField.Outcome => asc
                ? source.OrderBy(e => e.Outcome?.Timestamp ?? e.CreatedAt)
                : source.OrderByDescending(e => e.Outcome?.Timestamp ?? e.CreatedAt),
            _ => asc
                ? source.OrderBy(e => e.CreatedAt)
                : source.OrderByDescending(e => e.CreatedAt),
        };

    // Chains the field ordering onto an existing ordering (e.g. after pinned-first).
    private static IOrderedEnumerable<Entry> ApplyField(IOrderedEnumerable<Entry> source, EntrySortField field, bool asc) =>
        field switch
        {
            EntrySortField.Priority => asc
                ? source.ThenBy(e => e.Priority)
                : source.ThenByDescending(e => e.Priority),
            EntrySortField.Severity => asc
                ? source.ThenBy(e => e.Severity)
                : source.ThenByDescending(e => e.Severity),
            EntrySortField.Title => asc
                ? source.ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                : source.ThenByDescending(e => e.Title, StringComparer.OrdinalIgnoreCase),
            EntrySortField.Outcome => asc
                ? source.ThenBy(e => e.Outcome?.Timestamp ?? e.CreatedAt)
                : source.ThenByDescending(e => e.Outcome?.Timestamp ?? e.CreatedAt),
            _ => asc
                ? source.ThenBy(e => e.CreatedAt)
                : source.ThenByDescending(e => e.CreatedAt),
        };
}
