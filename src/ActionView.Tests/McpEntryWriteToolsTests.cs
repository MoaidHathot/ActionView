using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using ActionView.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class McpEntryWriteToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EntryStore _store;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpEntryWriteToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_mcp_write_{Guid.NewGuid():N}");
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

    private Entry IngestEntry(string title = "Test Entry")
    {
        var entry = new Entry
        {
            SchemaVersion = "1",
            Type = "test",
            Source = "unit-test",
            Title = title,
            Severity = Severity.Medium
        };
        return _store.IngestEntry(entry)!;
    }

    // --- add_entry ---

    [Fact]
    public void AddEntry_IngestsValidEntry()
    {
        var entryJson = """{"type":"test","source":"mcp-test","title":"MCP Entry"}""";

        var result = EntryWriteTools.AddEntry(_store, _jsonOptions, entryJson);
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("MCP Entry", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("type").GetString());

        // Verify it's actually in the store
        var id = doc.RootElement.GetProperty("id").GetString()!;
        Assert.NotNull(_store.GetEntry(id));
    }

    [Fact]
    public void AddEntry_ReturnsErrorForInvalidJson()
    {
        var result = EntryWriteTools.AddEntry(_store, _jsonOptions, "not json");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("Invalid JSON", error.GetString());
    }

    [Fact]
    public void AddEntry_ReturnsErrorForMissingRequiredFields()
    {
        var result = EntryWriteTools.AddEntry(_store, _jsonOptions,
            """{"type":"test","source":"","title":""}""");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("required fields", error.GetString());
    }

    // --- dismiss_entry ---

    [Fact]
    public void DismissEntry_ArchivesEntry()
    {
        var entry = IngestEntry("Dismiss Me");

        var result = EntryWriteTools.DismissEntry(_store, _jsonOptions, entry.Id);
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("archived", doc.RootElement.GetProperty("status").GetString());

        // Verify it's no longer active
        Assert.Null(_store.GetEntry(entry.Id));

        // Verify it's in archive
        var archived = _store.GetArchivedEntry(entry.Id);
        Assert.NotNull(archived);
        Assert.Equal("Dismissed", archived.Outcome!.Action);
    }

    [Fact]
    public void DismissEntry_IncludesReason()
    {
        var entry = IngestEntry("Dismiss With Reason");

        EntryWriteTools.DismissEntry(_store, _jsonOptions, entry.Id, reason: "Not relevant");

        var archived = _store.GetArchivedEntry(entry.Id);
        Assert.Equal("Not relevant", archived!.Outcome!.ResultMessage);
    }

    [Fact]
    public void DismissEntry_ReturnsErrorForUnknownId()
    {
        var result = EntryWriteTools.DismissEntry(_store, _jsonOptions, "nonexistent");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    // --- delete_entry ---

    [Fact]
    public void DeleteEntry_RemovesPermanently()
    {
        var entry = IngestEntry("Delete Me");

        var result = EntryWriteTools.DeleteEntry(_store, _jsonOptions, entry.Id);
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("deleted", doc.RootElement.GetProperty("status").GetString());

        Assert.Null(_store.GetEntry(entry.Id));
        Assert.Null(_store.GetArchivedEntry(entry.Id));
    }

    [Fact]
    public void DeleteEntry_ReturnsErrorForUnknownId()
    {
        var result = EntryWriteTools.DeleteEntry(_store, _jsonOptions, "nonexistent");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    // --- pin_entry ---

    [Fact]
    public void PinEntry_TogglesPin()
    {
        var entry = IngestEntry("Pin Me");
        Assert.False(entry.Pinned);

        var result = EntryWriteTools.PinEntry(_store, _jsonOptions, entry.Id);
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("pinned").GetBoolean());

        // Toggle again
        var result2 = EntryWriteTools.PinEntry(_store, _jsonOptions, entry.Id);
        var doc2 = JsonDocument.Parse(result2);
        Assert.False(doc2.RootElement.GetProperty("pinned").GetBoolean());
    }

    [Fact]
    public void PinEntry_ReturnsErrorForUnknownId()
    {
        var result = EntryWriteTools.PinEntry(_store, _jsonOptions, "nonexistent");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
