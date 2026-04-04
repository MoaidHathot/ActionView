using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using ActionView.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class McpStatsToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EntryStore _store;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpStatsToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_mcp_stats_{Guid.NewGuid():N}");
        ConfigLoader.EnsureDirectories(_tempDir);
        _store = new EntryStore(_tempDir, NullLogger<EntryStore>.Instance);
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
        _store.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        _store.IngestEntry(new Entry { Type = "test", Source = "s", Title = "A", Severity = Severity.High });
        _store.IngestEntry(new Entry { Type = "test", Source = "s", Title = "B", Severity = Severity.Low });
        _store.IngestEntry(new Entry { Type = "deploy", Source = "s", Title = "C", Severity = Severity.High });

        var result = StatsTools.GetStats(_store, _jsonOptions);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(3, doc.RootElement.GetProperty("totalPending").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("totalViewed").GetInt32());
        Assert.True(doc.RootElement.GetProperty("countByType").TryGetProperty("test", out var testCount));
        Assert.Equal(2, testCount.GetInt32());
    }

    [Fact]
    public void GetStats_ReturnsEmptyForNoEntries()
    {
        var result = StatsTools.GetStats(_store, _jsonOptions);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(0, doc.RootElement.GetProperty("totalPending").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("totalViewed").GetInt32());
    }

    [Fact]
    public void GetSchema_ReturnsValidJson()
    {
        var result = StatsTools.GetSchema();

        // Should be valid JSON
        var doc = JsonDocument.Parse(result);

        // Should look like a JSON Schema
        Assert.True(
            doc.RootElement.TryGetProperty("$schema", out _) ||
            doc.RootElement.TryGetProperty("type", out _) ||
            doc.RootElement.TryGetProperty("properties", out _),
            "Result should look like a JSON schema");
    }
}
