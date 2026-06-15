using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class EntryFilteringTests
{
    private static Entry MakeEntry(params string[] tags) =>
        new() { Type = "t", Source = "s", Title = "title", Tags = [.. tags] };

    [Theory]
    [InlineData("all", TagMatchMode.Any, TagMatchMode.All)]
    [InlineData("any", TagMatchMode.All, TagMatchMode.Any)]
    [InlineData("ALL", TagMatchMode.Any, TagMatchMode.All)]
    [InlineData(null, TagMatchMode.All, TagMatchMode.All)]
    [InlineData("bogus", TagMatchMode.Any, TagMatchMode.Any)]
    public void ParseTagMode_Works(string? value, TagMatchMode fallback, TagMatchMode expected)
    {
        Assert.Equal(expected, EntryFiltering.ParseTagMode(value, fallback));
    }

    [Fact]
    public void MatchesTags_EmptyFilter_AlwaysMatches()
    {
        var entry = MakeEntry("work");
        Assert.True(EntryFiltering.MatchesTags(entry, [], TagMatchMode.All));
        Assert.True(EntryFiltering.MatchesTags(entry, [], TagMatchMode.Any));
    }

    [Fact]
    public void MatchesTags_Any_MatchesWhenAnyPresent()
    {
        var entry = MakeEntry("work", "urgent");
        Assert.True(EntryFiltering.MatchesTags(entry, ["personal", "urgent"], TagMatchMode.Any));
        Assert.False(EntryFiltering.MatchesTags(entry, ["personal", "later"], TagMatchMode.Any));
    }

    [Fact]
    public void MatchesTags_All_MatchesOnlyWhenEveryTagPresent()
    {
        var entry = MakeEntry("work", "urgent");
        Assert.True(EntryFiltering.MatchesTags(entry, ["work", "urgent"], TagMatchMode.All));
        Assert.False(EntryFiltering.MatchesTags(entry, ["work", "missing"], TagMatchMode.All));
    }

    [Fact]
    public void MatchesTags_IsCaseInsensitive()
    {
        var entry = MakeEntry("Work", "Urgent");
        Assert.True(EntryFiltering.MatchesTags(entry, ["work", "urgent"], TagMatchMode.All));
    }
}
