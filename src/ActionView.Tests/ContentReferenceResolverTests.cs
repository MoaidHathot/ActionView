using System.Text.Json;
using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class ContentReferenceResolverTests
{
    private static readonly ContentReferenceResolver Resolver = new();

    private static ContentBlock TextBlock(string? id, string text) => new()
    {
        Type = ContentBlockType.Markdown,
        Id = id,
        Body = JsonSerializer.SerializeToElement(text),
    };

    private static Entry SampleEntry() => new()
    {
        Type = "pr-review",
        Source = "test",
        Title = "Refactor MoboBroker",
        Subtitle = "10 comments",
        Tags = ["pr-review", "ZTS"],
        Severity = Severity.High,
        Content =
        [
            new ContentBlock
            {
                Type = ContentBlockType.Section,
                Title = "Review Comments",
                Children = [TextBlock("draft-abc", "Please reconcile drift.")],
            },
        ],
    };

    [Fact]
    public void Resolves_ContentSelf()
    {
        var self = TextBlock(null, "edited comment body");
        var ctx = new ActionContext { Entry = SampleEntry(), SelfBlock = self };
        Assert.Equal("body: edited comment body", Resolver.Resolve("body: {{content.self}}", ctx));
    }

    [Fact]
    public void Resolves_ContentById_DeepWalk()
    {
        var ctx = new ActionContext { Entry = SampleEntry() };
        Assert.Equal("Please reconcile drift.", Resolver.Resolve("{{content.draft-abc}}", ctx));
    }

    [Theory]
    [InlineData("{{entry.title}}", "Refactor MoboBroker")]
    [InlineData("{{entry.subtitle}}", "10 comments")]
    [InlineData("{{entry.type}}", "pr-review")]
    [InlineData("{{entry.severity}}", "high")]
    [InlineData("{{entry.tags}}", "pr-review, ZTS")]
    public void Resolves_EntryFields(string input, string expected)
    {
        var ctx = new ActionContext { Entry = SampleEntry() };
        Assert.Equal(expected, Resolver.Resolve(input, ctx));
    }

    [Fact]
    public void Unknown_References_LeftVerbatim()
    {
        var ctx = new ActionContext { Entry = SampleEntry() };
        Assert.Equal("{{content.missing}}", Resolver.Resolve("{{content.missing}}", ctx));
        Assert.Equal("{{entry.bogus}}", Resolver.Resolve("{{entry.bogus}}", ctx));
    }

    [Fact]
    public void NullContext_ReturnsInputUnchanged()
    {
        Assert.Equal("{{content.self}}", Resolver.Resolve("{{content.self}}", null));
    }

    [Fact]
    public void EditedBody_FlowsIntoReference()
    {
        // The whole point: {{content.self}} expands to the CURRENT (edited) text.
        var block = TextBlock(null, "original");
        var ctx = new ActionContext { Entry = SampleEntry(), SelfBlock = block };
        Assert.Equal("original", Resolver.Resolve("{{content.self}}", ctx));

        block.Edited = new BlockEdit { OriginalText = "original" };
        block.Body = JsonSerializer.SerializeToElement("edited");
        Assert.Equal("edited", Resolver.Resolve("{{content.self}}", ctx));
    }

    [Fact]
    public void ComposesWith_ParametersThenSecrets_InOrder()
    {
        // Mirrors ActionExecutor.ResolveAll: params → content → secrets.
        var paramResolver = new ParameterResolver();
        var secretConfig = new AppConfig { Secrets = { ["TOKEN"] = "s3cr3t" } };
        var secretResolver = new SecretResolver(secretConfig);
        var self = TextBlock(null, "the body");
        var ctx = new ActionContext { Entry = SampleEntry(), SelfBlock = self };

        string ResolveAll(string s) =>
            secretResolver.Resolve(Resolver.Resolve(paramResolver.Resolve(s, new Dictionary<string, string> { ["draftId"] = "d1" }), ctx));

        Assert.Equal("d1 | the body | s3cr3t",
            ResolveAll("{{param.draftId}} | {{content.self}} | {{TOKEN}}"));
    }
}

public class ContentBlockTextTests
{
    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("42", "42")]
    [InlineData("true", "true")]
    public void GetText_FromBody(string bodyJson, string expected)
    {
        var block = new ContentBlock { Type = ContentBlockType.Markdown, Body = JsonDocument.Parse(bodyJson).RootElement.Clone() };
        Assert.Equal(expected, block.GetText());
    }

    [Fact]
    public void GetText_FallsBackToValueThenTitle()
    {
        Assert.Equal("v", new ContentBlock { Type = ContentBlockType.Stat, Value = "v" }.GetText());
        Assert.Equal("t", new ContentBlock { Type = ContentBlockType.Section, Title = "t" }.GetText());
        Assert.Equal(string.Empty, new ContentBlock { Type = ContentBlockType.Divider }.GetText());
    }
}
