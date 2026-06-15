using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class EntrySortingTests
{
    private static Entry Make(string id, int priority = 0, bool pinned = false,
        Severity severity = Severity.Medium, DateTimeOffset? created = null, string title = "title") =>
        new()
        {
            Id = id,
            Type = "t",
            Source = "s",
            Title = title,
            Priority = priority,
            Pinned = pinned,
            Severity = severity,
            CreatedAt = created ?? DateTimeOffset.UnixEpoch,
        };

    [Theory]
    [InlineData("created", EntrySortField.Created)]
    [InlineData("date", EntrySortField.Created)]
    [InlineData("priority", EntrySortField.Priority)]
    [InlineData("severity", EntrySortField.Severity)]
    [InlineData("title", EntrySortField.Title)]
    [InlineData("outcome", EntrySortField.Outcome)]
    public void TryParseField_Known(string value, EntrySortField expected)
    {
        Assert.Equal(expected, EntrySorting.TryParseField(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData(null)]
    public void TryParseField_Unknown_ReturnsNull(string? value)
    {
        Assert.Null(EntrySorting.TryParseField(value));
    }

    [Theory]
    [InlineData("asc", SortDirection.Ascending)]
    [InlineData("desc", SortDirection.Descending)]
    [InlineData("weird", SortDirection.Descending)]
    public void ParseDirection_Works(string value, SortDirection expected)
    {
        Assert.Equal(expected, EntrySorting.ParseDirection(value));
    }

    [Fact]
    public void Sort_ByPriority_RespectsDirection()
    {
        var entries = new[] { Make("a", 1), Make("b", 3), Make("c", 2) };

        var desc = EntrySorting.Sort(entries, EntrySortField.Priority, SortDirection.Descending, pinnedFirst: false);
        Assert.Equal(["b", "c", "a"], desc.Select(e => e.Id));

        var asc = EntrySorting.Sort(entries, EntrySortField.Priority, SortDirection.Ascending, pinnedFirst: false);
        Assert.Equal(["a", "c", "b"], asc.Select(e => e.Id));
    }

    [Fact]
    public void Sort_ByCreated_Ascending()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var entries = new[]
        {
            Make("old", created: t0),
            Make("new", created: t0.AddDays(2)),
            Make("mid", created: t0.AddDays(1)),
        };

        var asc = EntrySorting.Sort(entries, EntrySortField.Created, SortDirection.Ascending, pinnedFirst: false);
        Assert.Equal(["old", "mid", "new"], asc.Select(e => e.Id));
    }

    [Fact]
    public void Sort_PinnedFirst_FloatsPinnedRegardlessOfField()
    {
        // 'low' has the lowest priority but is pinned, so it must come first.
        var entries = new[]
        {
            Make("high", priority: 9, pinned: false),
            Make("low", priority: 1, pinned: true),
        };

        var sorted = EntrySorting.Sort(entries, EntrySortField.Priority, SortDirection.Descending, pinnedFirst: true);
        Assert.Equal(["low", "high"], sorted.Select(e => e.Id));
    }

    [Fact]
    public void Sort_IsDeterministic_OnEqualKeys()
    {
        var entries = new[] { Make("b", 5), Make("a", 5), Make("c", 5) };
        var sorted = EntrySorting.Sort(entries, EntrySortField.Priority, SortDirection.Descending, pinnedFirst: false);
        // Equal priority -> stable id tie-breaker (ordinal ascending).
        Assert.Equal(["a", "b", "c"], sorted.Select(e => e.Id));
    }

    [Fact]
    public void Default_OrdersByPinnedThenPriorityThenSeverityThenCreated()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var entries = new[]
        {
            Make("a", priority: 1, severity: Severity.Low, created: t0),
            Make("pinned", priority: 0, pinned: true, created: t0),
            Make("b", priority: 1, severity: Severity.High, created: t0),
            Make("c", priority: 5, created: t0),
        };

        var sorted = EntrySorting.Default(entries).Select(e => e.Id);

        // pinned first; then priority desc (c=5); within priority 1, severity desc (b High > a Low).
        Assert.Equal(["pinned", "c", "b", "a"], sorted);
    }
}
