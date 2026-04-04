using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Services;
using ActionView.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class McpTemplateReadToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TemplateRegistry _registry;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpTemplateReadToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_mcp_tplread_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "templates"));
        _registry = new TemplateRegistry(_tempDir, NullLogger<TemplateRegistry>.Instance);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public void Dispose()
    {
        _registry.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void ListTemplates_ReturnsAllTemplates()
    {
        _registry.Register("""{"type":"alpha","description":"Alpha template"}""");
        _registry.Register("""{"type":"beta","description":"Beta template"}""");

        var result = TemplateReadTools.ListTemplates(_registry, _jsonOptions);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("templates").GetArrayLength());
    }

    [Fact]
    public void ListTemplates_ReturnsEmptyWhenNone()
    {
        var result = TemplateReadTools.ListTemplates(_registry, _jsonOptions);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(0, doc.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public void ListTemplates_IncludesSummaryFields()
    {
        _registry.Register("""{"type":"test","description":"Test template","contentTemplate":[{"type":"markdown"}],"expectedActions":[{"label":"Approve"}]}""");

        var result = TemplateReadTools.ListTemplates(_registry, _jsonOptions);
        var doc = JsonDocument.Parse(result);

        var template = doc.RootElement.GetProperty("templates")[0];
        Assert.Equal("test", template.GetProperty("type").GetString());
        Assert.Equal("Test template", template.GetProperty("description").GetString());
        Assert.Equal(1, template.GetProperty("contentBlocks").GetInt32());
        Assert.Equal(1, template.GetProperty("expectedActions").GetInt32());
    }

    [Fact]
    public void GetTemplate_ReturnsFullDefinition()
    {
        _registry.Register("""{"type":"pr-review","description":"PR reviews"}""");

        var result = TemplateReadTools.GetTemplate(_registry, _jsonOptions, "pr-review");
        var doc = JsonDocument.Parse(result);

        Assert.Equal("pr-review", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("PR reviews", doc.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public void GetTemplate_ReturnsErrorForUnknownType()
    {
        var result = TemplateReadTools.GetTemplate(_registry, _jsonOptions, "nonexistent");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
