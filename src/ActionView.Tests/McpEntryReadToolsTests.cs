using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using ActionView.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class McpEntryReadToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EntryStore _store;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly AppConfig _config = new();

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public McpEntryReadToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_mcp_read_{Guid.NewGuid():N}");
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

    private Entry IngestEntry(string title = "Test Entry", string type = "test", Severity severity = Severity.Medium)
    {
        var entry = new Entry
        {
            SchemaVersion = "1",
            Type = type,
            Source = "unit-test",
            Title = title,
            Severity = severity
        };
        return _store.IngestEntry(entry)!;
    }

    [Fact]
    public void ListEntries_ReturnsAllActiveEntries()
    {
        IngestEntry("Entry 1");
        IngestEntry("Entry 2");

        var result = EntryReadTools.ListEntries(_store, _config, _jsonOptions);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public void ListEntries_FiltersByType()
    {
        IngestEntry("PR Review", type: "pr-review");
        IngestEntry("Deploy", type: "deploy");

        var result = EntryReadTools.ListEntries(_store, _config, _jsonOptions, type: "pr-review");
        var doc = JsonDocument.Parse(result);

        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal("PR Review", entries[0].GetProperty("title").GetString());
    }

    [Fact]
    public void ListEntries_FiltersBySeverity()
    {
        IngestEntry("Low Entry", severity: Severity.Low);
        IngestEntry("High Entry", severity: Severity.High);

        var result = EntryReadTools.ListEntries(_store, _config, _jsonOptions, severity: "high");
        var doc = JsonDocument.Parse(result);

        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal("High Entry", entries[0].GetProperty("title").GetString());
    }

    [Fact]
    public void ListEntries_FiltersBySource()
    {
        IngestEntry("Entry 1");

        var result = EntryReadTools.ListEntries(_store, _config, _jsonOptions, source: "unit-test");
        var doc = JsonDocument.Parse(result);

        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public void ListEntries_SearchesByTitle()
    {
        IngestEntry("Important PR Review");
        IngestEntry("Deploy Ready");

        var result = EntryReadTools.ListEntries(_store, _config, _jsonOptions, search: "PR Review");
        var doc = JsonDocument.Parse(result);

        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public void ListEntries_ReturnsEmptyWhenNoEntries()
    {
        var result = EntryReadTools.ListEntries(_store, _config, _jsonOptions);
        var doc = JsonDocument.Parse(result);

        Assert.Equal(0, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public void GetEntry_ReturnsEntryById()
    {
        var entry = IngestEntry("Find Me");

        var result = EntryReadTools.GetEntry(_store, _jsonOptions, entry.Id);
        var doc = JsonDocument.Parse(result);

        Assert.Equal("Find Me", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(entry.Id, doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void GetEntry_SupportsPartialIdMatch()
    {
        var entry = IngestEntry("Partial Match");
        var partialId = entry.Id[..8];

        var result = EntryReadTools.GetEntry(_store, _jsonOptions, partialId);
        var doc = JsonDocument.Parse(result);

        Assert.Equal("Partial Match", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public void GetEntry_ReturnsErrorForUnknownId()
    {
        var result = EntryReadTools.GetEntry(_store, _jsonOptions, "nonexistent");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
