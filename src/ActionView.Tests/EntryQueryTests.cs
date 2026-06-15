using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class EntryQueryTests
{
    private static Entry Make(
        string id, string type = "t", int priority = 0, bool pinned = false,
        Severity severity = Severity.Medium, DateTimeOffset? created = null,
        IEnumerable<string>? tags = null, DateTimeOffset? outcomeAt = null) =>
        new()
        {
            Id = id,
            Type = type,
            Source = "s",
            Title = id,
            Priority = priority,
            Pinned = pinned,
            Severity = severity,
            CreatedAt = created ?? DateTimeOffset.UnixEpoch,
            Tags = tags?.ToList() ?? [],
            Outcome = outcomeAt is null ? null : new EntryOutcome { Action = "Done", Timestamp = outcomeAt.Value },
        };

    [Fact]
    public void RunActive_DefaultOrder_UsesCanonicalSort()
    {
        var entries = new[] { Make("a", priority: 1), Make("pinned", pinned: true), Make("b", priority: 5) };

        var result = EntryQuery.RunActive(entries, new FilterCriteria(), null, SortDirection.Descending);

        Assert.Equal(["pinned", "b", "a"], result.Select(e => e.Id));
    }

    [Fact]
    public void RunActive_ExplicitSort_OverridesDefault_ButPinnedStillFloats()
    {
        var entries = new[] { Make("low", priority: 1, pinned: true), Make("high", priority: 9) };

        var result = EntryQuery.RunActive(entries, new FilterCriteria(), EntrySortField.Priority, SortDirection.Descending);

        Assert.Equal(["low", "high"], result.Select(e => e.Id));
    }

    [Fact]
    public void RunActive_AppliesFilterBeforeSort()
    {
        var entries = new[] { Make("keep", tags: ["work"]), Make("drop", tags: ["personal"]) };
        var criteria = new FilterCriteria { Tags = ["work"], TagMode = TagMatchMode.Any };

        var result = EntryQuery.RunActive(entries, criteria, null, SortDirection.Descending);

        Assert.Equal(["keep"], result.Select(e => e.Id));
    }

    [Fact]
    public void RunHistory_DefaultOrder_IsMostRecentOutcomeFirst()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var entries = new[]
        {
            Make("old", outcomeAt: t0.AddMinutes(1)),
            Make("new", outcomeAt: t0.AddMinutes(5)),
            Make("mid", outcomeAt: t0.AddMinutes(3)),
        };

        var result = EntryQuery.RunHistory(entries, new FilterCriteria(), null, SortDirection.Descending);

        Assert.Equal(["new", "mid", "old"], result.Select(e => e.Id));
    }

    [Fact]
    public void RunHistory_FilterAppliesGlobally_NotJustFirstPage()
    {
        // 60 archived entries, newest-outcome first. Only the OLDEST carries the
        // target tag, so it lands on "page 2". Filtering must happen before
        // pagination, so the caller's first page of 50 still surfaces it.
        var t0 = DateTimeOffset.UnixEpoch;
        var entries = Enumerable.Range(0, 60)
            .Select(i => Make($"e{i:D2}", outcomeAt: t0.AddMinutes(i), tags: i == 0 ? ["target"] : null))
            .ToList();

        var criteria = new FilterCriteria { Tags = ["target"], TagMode = TagMatchMode.Any };
        var filtered = EntryQuery.RunHistory(entries, criteria, null, SortDirection.Descending);

        var firstPage = filtered.Skip(0).Take(50).ToList();
        Assert.Single(firstPage);
        Assert.Equal("e00", firstPage[0].Id);
    }
}
