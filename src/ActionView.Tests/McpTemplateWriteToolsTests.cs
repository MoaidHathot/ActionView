using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Services;
using ActionView.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class McpTemplateWriteToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TemplateRegistry _registry;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpTemplateWriteToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_mcp_tplwrite_{Guid.NewGuid():N}");
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
    public void RegisterTemplate_RegistersSuccessfully()
    {
        var templateJson = """{"type":"test","description":"A test template","contentTemplate":[{"type":"markdown"}]}""";

        var result = TemplateWriteTools.RegisterTemplate(_registry, _jsonOptions, templateJson);
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("test", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("contentBlocks").GetInt32());

        // Verify it's actually registered
        Assert.NotNull(_registry.GetTemplate("test"));
    }

    [Fact]
    public void RegisterTemplate_OverwritesExisting()
    {
        _registry.Register("""{"type":"test","description":"Original"}""");

        var result = TemplateWriteTools.RegisterTemplate(_registry, _jsonOptions,
            """{"type":"test","description":"Updated"}""");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Updated", _registry.GetTemplate("test")!.Description);
    }

    [Fact]
    public void RegisterTemplate_ReturnsErrorForInvalidJson()
    {
        var result = TemplateWriteTools.RegisterTemplate(_registry, _jsonOptions, "not json");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void RemoveTemplate_RemovesSuccessfully()
    {
        _registry.Register("""{"type":"removable","description":"To be removed"}""");

        var result = TemplateWriteTools.RemoveTemplate(_registry, _jsonOptions, "removable");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("removed", doc.RootElement.GetProperty("status").GetString());

        // Verify it's gone
        Assert.Null(_registry.GetTemplate("removable"));
    }

    [Fact]
    public void RemoveTemplate_ReturnsErrorForUnknownType()
    {
        var result = TemplateWriteTools.RemoveTemplate(_registry, _jsonOptions, "nonexistent");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
