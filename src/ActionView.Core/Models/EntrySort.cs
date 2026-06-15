namespace ActionView.Core.Models;

/// <summary>Field an entry list can be ordered by.</summary>
public enum EntrySortField
{
    /// <summary>Creation timestamp (<see cref="Entry.CreatedAt"/>).</summary>
    Created,

    /// <summary>Numeric priority weight.</summary>
    Priority,

    /// <summary>Severity (low &lt; medium &lt; high &lt; critical).</summary>
    Severity,

    /// <summary>Title, compared case-insensitively.</summary>
    Title,

    /// <summary>Outcome timestamp, falling back to creation time (history).</summary>
    Outcome,
}

/// <summary>Sort direction.</summary>
public enum SortDirection
{
    Ascending,
    Descending,
}
