using System.Text.Json;
using ActionView.Core.Models;

namespace ActionView.Tests;

public sealed class ContentBlockSerializationTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public void ImageBlock_RoundTrip()
    {
        var json = """{ "type": "image", "url": "https://x", "alt": "A", "maxWidth": 320, "imageAnnotations": [ { "shape": "arrow", "x": 10, "y": 20, "level": "warning" } ] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.Image, block!.Type);
        Assert.Equal(320, block.MaxWidth);
        Assert.Single(block.ImageAnnotations!);
    }

    [Fact]
    public void DiffBlock_RoundTrip()
    {
        var json = """{ "type": "diff", "oldFilename": "a.cs", "mode": "unified", "body": "x" }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.Diff, block!.Type);
        Assert.Equal("unified", block.Mode);
    }

    [Fact]
    public void VideoBlock_RoundTrip()
    {
        var json = """{ "type": "video", "provider": "youtube", "videoId": "abc", "startTime": 165, "chapters": [ { "at": 0, "label": "Intro" } ] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.Video, block!.Type);
        Assert.Equal(165, block.StartTime);
        Assert.Single(block.Chapters!);
    }

    [Fact]
    public void GalleryBlock_RoundTrip()
    {
        var json = """{ "type": "gallery", "images": [ { "url": "a.jpg" }, { "url": "b.jpg" } ] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.Gallery, block!.Type);
        Assert.Equal(2, block.Images!.Count);
    }

    [Fact]
    public void TimelineBlock_RoundTrip()
    {
        var json = """{ "type": "timeline", "events": [ { "at": "12:00", "label": "fired", "level": "warning" } ] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.Timeline, block!.Type);
        Assert.Single(block.Events!);
    }

    [Fact]
    public void StatBlock_RoundTrip()
    {
        var json = """{ "type": "stat", "value": "2.3", "unit": "%", "trend": "up", "sparkline": [1.6, 1.9, 2.3] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.Stat, block!.Type);
        Assert.Equal("up", block.Trend);
        Assert.Equal(3, block.Sparkline!.Count);
    }

    [Fact]
    public void TableBlock_RichCells_RoundTrip()
    {
        var json = """{ "type": "table", "columns": ["A","B"], "sortable": true, "rows": [["x",{"type":"status","level":"success","label":"OK"}]] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.True(block!.Sortable);
        Assert.Equal("OK", block.Rows![0][1].GetProperty("label").GetString());
    }

    [Fact]
    public void KeyValueBlock_RichValues_RoundTrip()
    {
        var json = """{ "type": "keyValue", "pairs": { "Branch": "main", "Commit": { "type": "copy", "value": "abc" } } }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal("main", block!.Pairs!["Branch"].GetString());
        Assert.Equal("copy", block.Pairs["Commit"].GetProperty("type").GetString());
    }

    [Fact]
    public void LinkBlock_MultipleLinks_RoundTrip()
    {
        var json = """{ "type": "link", "links": [ { "url": "https://x", "label": "L", "icon": "pr", "body": "d" } ] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Single(block!.Links!);
        Assert.Equal("d", block.Links![0].Body);
    }

    [Fact]
    public void CodeBlock_Annotations_RoundTrip()
    {
        var json = """{ "type": "code", "body": "x", "showLineNumbers": false, "annotations": [ { "line": 5, "level": "warning", "body": "T" } ] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.False(block!.ShowLineNumbers);
        Assert.Single(block.Annotations!);
    }

    [Fact]
    public void SectionBlock_DefaultCollapsed_RoundTrip()
    {
        var json = """{ "type": "section", "title": "S", "badge": "11", "defaultCollapsed": true, "content": [] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.True(block!.DefaultCollapsed);
        Assert.Equal("11", block.Badge);
    }

    [Fact]
    public void AlertBlock_Dismissible_RoundTrip()
    {
        var json = """{ "type": "alert", "level": "warning", "body": "x", "dismissible": true }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.True(block!.Dismissible);
        Assert.Equal(AlertLevel.Warning, block.Level);
    }

    [Fact]
    public void FileBlock_RoundTrip()
    {
        var json = """{ "type": "file", "url": "file:///C:/x.zip", "filename": "x.zip", "fileSize": 100 }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.File, block!.Type);
        Assert.Equal(100, block.FileSize);
    }

    [Fact]
    public void BeforeAfterBlock_RoundTrip()
    {
        var json = """{ "type": "beforeAfter", "beforeUrl": "b.png", "afterUrl": "a.png" }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.BeforeAfter, block!.Type);
        Assert.Equal("b.png", block.BeforeUrl);
    }

    [Fact]
    public void ChartBlock_RoundTrip()
    {
        var json = """{ "type": "chart", "chartType": "line", "xAxis": ["a","b"], "series": [ { "name": "east", "data": [1, 2] } ] }""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Json);
        Assert.Equal(ContentBlockType.Chart, block!.Type);
        Assert.Single(block.Series!);
    }
}
