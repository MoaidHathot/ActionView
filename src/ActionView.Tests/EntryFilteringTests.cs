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

    private static Entry Rich(string id, string type, string source, Severity sev, string title, params string[] tags) =>
        new() { Id = id, Type = type, Source = source, Severity = sev, Title = title, Tags = [.. tags] };

    [Fact]
    public void Apply_FiltersByEveryDimension()
    {
        var entries = new[]
        {
            Rich("1", "deploy", "ci", Severity.High, "Deploy prod", "work"),
            Rich("2", "pr-review", "gh", Severity.Low, "Review PR", "work", "urgent"),
            Rich("3", "deploy", "ci", Severity.Medium, "Deploy staging", "personal"),
        };

        Assert.Equal(["1", "3"], EntryFiltering.Apply(entries, new FilterCriteria { Type = "deploy" }).Select(e => e.Id));
        Assert.Equal(["2"], EntryFiltering.Apply(entries, new FilterCriteria { Severity = Severity.Low }).Select(e => e.Id));
        Assert.Equal(["2"], EntryFiltering.Apply(entries, new FilterCriteria { Source = "gh" }).Select(e => e.Id));
        Assert.Equal(["2"], EntryFiltering.Apply(entries, new FilterCriteria { Tags = ["work", "urgent"], TagMode = TagMatchMode.All }).Select(e => e.Id));
        Assert.Equal(["3"], EntryFiltering.Apply(entries, new FilterCriteria { Search = "staging" }).Select(e => e.Id));
    }

    [Fact]
    public void Apply_Search_IncludesOutcomeOnlyWhenEnabled()
    {
        var entry = new Entry { Id = "1", Type = "t", Source = "s", Title = "x", Outcome = new EntryOutcome { Action = "Approved" } };

        Assert.Empty(EntryFiltering.Apply([entry], new FilterCriteria { Search = "approve", IncludeOutcomeInSearch = false }));
        Assert.Single(EntryFiltering.Apply([entry], new FilterCriteria { Search = "approve", IncludeOutcomeInSearch = true }));
    }

    [Fact]
    public void ParseCriteria_ParsesTagsSeverityAndMode()
    {
        var c = EntryFiltering.ParseCriteria("deploy", "high", "ci", "a, b ,", "all", "term", TagMatchMode.Any);

        Assert.Equal("deploy", c.Type);
        Assert.Equal(Severity.High, c.Severity);
        Assert.Equal("ci", c.Source);
        Assert.Equal(["a", "b"], c.Tags);
        Assert.Equal(TagMatchMode.All, c.TagMode);
        Assert.Equal("term", c.Search);
    }

    [Fact]
    public void ParseCriteria_UsesDefaultTagMode_WhenUnspecified()
    {
        var c = EntryFiltering.ParseCriteria(null, null, null, "x", null, null, TagMatchMode.All);
        Assert.Equal(TagMatchMode.All, c.TagMode);
    }

    [Fact]
    public void CriteriaForView_MapsTypeTagsAndMatch()
    {
        var view = new SavedView { Name = "Work", Type = "deploy", Tags = ["work", "urgent"], TagMatch = TagMatchMode.All };
        var c = EntryFiltering.CriteriaForView(view, TagMatchMode.Any);

        Assert.Equal("deploy", c.Type);
        Assert.Equal(["work", "urgent"], c.Tags);
        Assert.Equal(TagMatchMode.All, c.TagMode);
    }

    [Fact]
    public void CriteriaForView_FallsBackToDefaultMode_WhenViewHasNoMatch()
    {
        var c = EntryFiltering.CriteriaForView(new SavedView { Name = "Work", Tags = ["work"] }, TagMatchMode.All);
        Assert.Equal(TagMatchMode.All, c.TagMode);
    }

    [Fact]
    public void ResolveCriteria_WithKnownView_UsesViewAndLayersSeverity()
    {
        var views = new List<SavedView> { new() { Id = "work", Name = "Work", Tags = ["work"], TagMatch = TagMatchMode.All } };

        var c = EntryFiltering.ResolveCriteria(
            views, TagMatchMode.Any, "work",
            type: null, severity: "high", source: null, tags: null, tagMode: null, search: null);

        Assert.Equal(["work"], c.Tags);
        Assert.Equal(TagMatchMode.All, c.TagMode);
        Assert.Equal(Severity.High, c.Severity);
    }

    [Fact]
    public void ResolveCriteria_UnknownView_FallsBackToRawParams()
    {
        var c = EntryFiltering.ResolveCriteria(
            [], TagMatchMode.Any, "missing",
            type: "deploy", severity: null, source: null, tags: "a", tagMode: "all", search: null);

        Assert.Equal("deploy", c.Type);
        Assert.Equal(["a"], c.Tags);
        Assert.Equal(TagMatchMode.All, c.TagMode);
    }

    [Fact]
    public void ResolveCriteria_MatchesViewByName_CaseInsensitive()
    {
        var views = new List<SavedView> { new() { Id = "work", Name = "Work", Tags = ["work"] } };

        var c = EntryFiltering.ResolveCriteria(
            views, TagMatchMode.Any, "WORK",
            type: null, severity: null, source: null, tags: null, tagMode: null, search: null);

        Assert.Equal(["work"], c.Tags);
    }
}
