using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using ActionView.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

/// <summary>Tests for the read-only MCP validate_entry tool (the retry oracle over MCP).</summary>
public class McpValidateEntryToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EntryValidator _validator;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpValidateEntryToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_mcp_validate_{Guid.NewGuid():N}");
        ConfigLoader.EnsureDirectories(_tempDir);
        var registry = new TemplateRegistry(_tempDir, NullLogger<TemplateRegistry>.Instance);
        var normalizer = new EntryNormalizer(registry, NullLogger<EntryNormalizer>.Instance);
        _validator = new EntryValidator(normalizer);
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
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void ValidateEntry_ValidEntry_ReturnsOk()
    {
        var result = EntryReadTools.ValidateEntry(
            _validator, _jsonOptions, """{"type":"t","source":"s","title":"Hi"}""");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Empty(doc.RootElement.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void ValidateEntry_BadEntry_ReportsStructuredErrors()
    {
        var result = EntryReadTools.ValidateEntry(
            _validator, _jsonOptions,
            """{"type":"t","source":"s","title":"Hi","severity":"urgent"}""");
        var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        var errors = doc.RootElement.GetProperty("errors");
        Assert.Contains(errors.EnumerateArray(), e => e.GetProperty("code").GetString() == "schema.enum");
    }

    [Fact]
    public void ValidateEntry_DoesNotPersistAnything()
    {
        EntryReadTools.ValidateEntry(_validator, _jsonOptions, """{"type":"t","source":"s","title":"Hi"}""");

        var activeDir = Path.Combine(_tempDir, "active");
        Assert.Empty(Directory.EnumerateFiles(activeDir));
    }
}
