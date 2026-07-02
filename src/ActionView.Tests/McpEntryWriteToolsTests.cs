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
    private readonly EntryValidator _validator;
    private readonly AppConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpEntryWriteToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_mcp_write_{Guid.NewGuid():N}");
        ConfigLoader.EnsureDirectories(_tempDir);
        var registry = new TemplateRegistry(_tempDir, NullLogger<TemplateRegistry>.Instance);
        var normalizer = new EntryNormalizer(registry, NullLogger<EntryNormalizer>.Instance);
        _validator = new EntryValidator(normalizer);
        _config = new AppConfig { DataDirectory = _tempDir };
        _store = new EntryStore(_tempDir, NullLogger<EntryStore>.Instance, normalizer, _validator);
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

        var result = EntryWriteTools.AddEntry(_store, _validator, _config, _jsonOptions, entryJson);
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("MCP Entry", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("type").GetString());

        // Verify it's actually in the store
        var id = doc.RootElement.GetProperty("id").GetString()!;
        Assert.NotNull(_store.GetEntry(id));
    }

    [Fact]
    public void AddEntry_ReturnsValidationFailedForInvalidJson()
    {
        var result = EntryWriteTools.AddEntry(_store, _validator, _config, _jsonOptions, "not json");
        var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("validation_failed", doc.RootElement.GetProperty("error").GetString());

        var errors = doc.RootElement.GetProperty("validation").GetProperty("errors");
        Assert.Equal("json.parse", errors[0].GetProperty("code").GetString());
    }

    [Fact]
    public void AddEntry_ReturnsValidationFailedForMissingRequiredFields()
    {
        var result = EntryWriteTools.AddEntry(_store, _validator, _config, _jsonOptions,
            """{"type":"test","source":"","title":""}""");
        var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("validation_failed", doc.RootElement.GetProperty("error").GetString());

        // Empty required fields are caught by the schema (minLength) with precise paths.
        var errors = doc.RootElement.GetProperty("validation").GetProperty("errors");
        var paths = errors.EnumerateArray()
            .Select(e => e.GetProperty("path").GetString())
            .ToList();
        Assert.Contains("/source", paths);
        Assert.Contains("/title", paths);
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

    // --- update_entry ---

    [Fact]
    public void UpdateEntry_AppliesSuppliedFields()
    {
        var entry = IngestEntry("Original Title");

        var result = EntryWriteTools.UpdateEntry(_store, _jsonOptions, entry.Id,
            """{"title":"New Title","severity":"high","tags":["urgent","prod"]}""");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("New Title", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("high", doc.RootElement.GetProperty("severity").GetString());

        var fields = doc.RootElement.GetProperty("fieldsUpdated").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains("title", fields);
        Assert.Contains("severity", fields);
        Assert.Contains("tags", fields);
        Assert.DoesNotContain("subtitle", fields);

        // Verify state in store
        var stored = _store.GetEntry(entry.Id);
        Assert.NotNull(stored);
        Assert.Equal("New Title", stored!.Title);
        Assert.Equal(Severity.High, stored.Severity);
        Assert.Equal(new[] { "urgent", "prod" }, stored.Tags);
    }

    [Fact]
    public void UpdateEntry_OmittedFieldsAreLeftAlone()
    {
        var entry = IngestEntry("Keep Me");

        var result = EntryWriteTools.UpdateEntry(_store, _jsonOptions, entry.Id,
            """{"priority":5}""");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

        var fields = doc.RootElement.GetProperty("fieldsUpdated").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Single(fields);
        Assert.Contains("priority", fields);

        var stored = _store.GetEntry(entry.Id)!;
        Assert.Equal("Keep Me", stored.Title);          // unchanged
        Assert.Equal(Severity.Medium, stored.Severity); // unchanged
        Assert.Equal(5, stored.Priority);               // changed
    }

    [Fact]
    public void UpdateEntry_ExplicitNullLeavesFieldAlone()
    {
        // "null" in the JSON should be treated identically to "field omitted".
        var entry = IngestEntry("Has Title");

        var result = EntryWriteTools.UpdateEntry(_store, _jsonOptions, entry.Id,
            """{"title":null,"severity":"low"}""");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

        var fields = doc.RootElement.GetProperty("fieldsUpdated").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.DoesNotContain("title", fields);
        Assert.Contains("severity", fields);

        var stored = _store.GetEntry(entry.Id)!;
        Assert.Equal("Has Title", stored.Title);       // null didn't clobber
        Assert.Equal(Severity.Low, stored.Severity);
    }

    [Fact]
    public void UpdateEntry_ReturnsErrorForUnknownId()
    {
        var result = EntryWriteTools.UpdateEntry(_store, _jsonOptions, "nonexistent",
            """{"title":"x"}""");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("not found", error.GetString());
    }

    [Fact]
    public void UpdateEntry_ReturnsErrorForInvalidJson()
    {
        var entry = IngestEntry();

        var result = EntryWriteTools.UpdateEntry(_store, _jsonOptions, entry.Id, "not json");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("Invalid update JSON", error.GetString());
    }

    [Fact]
    public void UpdateEntry_CannotUpdateArchivedEntry()
    {
        var entry = IngestEntry("Will Be Archived");
        _store.ArchiveEntry(entry.Id, new EntryOutcome { Action = "Dismissed", Success = true });

        var result = EntryWriteTools.UpdateEntry(_store, _jsonOptions, entry.Id,
            """{"title":"x"}""");
        var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
