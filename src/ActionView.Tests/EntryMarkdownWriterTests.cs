using System.Text.Json;
using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public sealed class EntryMarkdownWriterTests
{
    private static Entry SampleEntry(params ContentBlock[] blocks) => new()
    {
        Id = "x",
        SchemaVersion = "1",
        Type = "test",
        Source = "tests",
        Title = "Sample",
        Subtitle = "sub",
        Severity = Severity.Medium,
        Content = blocks.ToList(),
    };

    [Fact]
    public void Header_IncludesTitleSubtitleTypeSourceSeverity()
    {
        var md = EntryMarkdownWriter.ToMarkdown(SampleEntry());
        Assert.Contains("# Sample", md);
        Assert.Contains("*sub*", md);
        Assert.Contains("- **Type:** test", md);
        Assert.Contains("- **Source:** tests", md);
        Assert.Contains("- **Severity:** Medium", md);
    }

    [Fact]
    public void MarkdownBlock_RendersBody()
    {
        var block = new ContentBlock { Type = ContentBlockType.Markdown, Body = JsonSerializer.SerializeToElement("hello **world**") };
        var md = EntryMarkdownWriter.ToMarkdown(SampleEntry(block));
        Assert.Contains("hello **world**", md);
    }

    [Fact]
    public void CodeBlock_FencedWithLanguage()
    {
        var block = new ContentBlock { Type = ContentBlockType.Code, Language = "csharp", Body = JsonSerializer.SerializeToElement("var x = 1;") };
        var md = EntryMarkdownWriter.ToMarkdown(SampleEntry(block));
        Assert.Contains("```csharp", md);
        Assert.Contains("var x = 1;", md);
        Assert.Contains("```", md);
    }

    [Fact]
    public void TableBlock_StringCells_RoundTrip()
    {
        var block = new ContentBlock
        {
            Type = ContentBlockType.Table,
            Columns = new() { "Name", "Status" },
            Rows = new()
            {
                new() { JsonSerializer.SerializeToElement("a"), JsonSerializer.SerializeToElement("ok") },
            },
        };
        var md = EntryMarkdownWriter.ToMarkdown(SampleEntry(block));
        Assert.Contains("| Name | Status |", md);
        Assert.Contains("| --- | --- |", md);
        Assert.Contains("| a | ok |", md);
    }

    [Fact]
    public void TableBlock_RichStatusCell_RendersBold()
    {
        var statusCell = JsonSerializer.SerializeToElement(new { type = "status", level = "success", label = "Passed" });
        var block = new ContentBlock
        {
            Type = ContentBlockType.Table,
            Columns = new() { "Test", "Status" },
            Rows = new()
            {
                new() { JsonSerializer.SerializeToElement("Auth"), statusCell },
            },
        };
        var md = EntryMarkdownWriter.ToMarkdown(SampleEntry(block));
        Assert.Contains("**success: Passed**", md);
    }

    [Fact]
    public void LinkBlock_MultipleLinks_RenderAsBulletList()
    {
        var block = new ContentBlock
        {
            Type = ContentBlockType.Link,
            Links = new()
            {
                new LinkItem { Url = "https://x", Label = "X", Body = "the X" },
                new LinkItem { Url = "https://y", Label = "Y" },
            },
        };
        var md = EntryMarkdownWriter.ToMarkdown(SampleEntry(block));
        Assert.Contains("- [X](https://x)", md);
        Assert.Contains("the X", md);
        Assert.Contains("- [Y](https://y)", md);
    }

    [Fact]
    public void TimelineBlock_RendersEvents()
    {
        var block = new ContentBlock
        {
            Type = ContentBlockType.Timeline,
            Events = new()
            {
                new TimelineEvent { At = "12:00", Label = "fired", Body = "Error rate spiked." },
                new TimelineEvent { At = "12:30", Label = "resolved" },
            },
        };
        var md = EntryMarkdownWriter.ToMarkdown(SampleEntry(block));
        Assert.Contains("**12:00**", md);
        Assert.Contains("fired", md);
        Assert.Contains("Error rate spiked.", md);
        Assert.Contains("resolved", md);
    }

    [Fact]
    public void ChartAndDiagram_RenderAsPlaceholders()
    {
        var chart = new ContentBlock { Type = ContentBlockType.Chart, ChartType = "line", Series = new() { new ChartSeries { Name = "n", Data = new() { 1.0 } } } };
        var diagram = new ContentBlock { Type = ContentBlockType.Diagram, Body = JsonSerializer.SerializeToElement("flowchart LR\nA --> B") };
        var md = EntryMarkdownWriter.ToMarkdown(SampleEntry(chart, diagram));
        Assert.Contains("*[chart: line, 1 series]*", md);
        Assert.Contains("```mermaid", md);
        Assert.Contains("flowchart LR", md);
    }

    [Fact]
    public void HtmlExport_WrapsMarkdownInPre()
    {
        var entry = SampleEntry(new ContentBlock { Type = ContentBlockType.Markdown, Body = JsonSerializer.SerializeToElement("hi") });
        var md = EntryMarkdownWriter.ToMarkdown(entry);
        var html = EntryMarkdownWriter.ToHtml(entry, md);
        Assert.Contains("<!doctype html>", html);
        Assert.Contains("<title>Sample</title>", html);
        Assert.Contains("<pre class=\"av-export\">", html);
    }
}
