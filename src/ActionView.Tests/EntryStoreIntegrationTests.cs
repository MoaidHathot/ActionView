using System.Text.Json;
using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

/// <summary>
/// Integration tests for the core ActionView pipeline:
/// inbox drop -> pickup -> active -> action/dismiss/delete -> archive
/// </summary>
public class EntryStoreIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EntryStore _store;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public EntryStoreIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_test_{Guid.NewGuid():N}");
        ConfigLoader.EnsureDirectories(_tempDir);
        _store = new EntryStore(_tempDir, NullLogger<EntryStore>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static Entry CreateSampleEntry(string title = "Test Entry", Severity severity = Severity.Medium)
    {
        return new Entry
        {
            SchemaVersion = "1",
            Type = "test",
            Source = "unit-test",
            Title = title,
            Subtitle = "Test subtitle",
            Severity = severity,
            Content =
            [
                new ContentBlock
                {
                    Type = ContentBlockType.Markdown,
                    Body = JsonSerializer.SerializeToElement("This is **test** content.")
                },
                new ContentBlock
                {
                    Type = ContentBlockType.KeyValue,
                    Label = "Details",
                    Pairs = new Dictionary<string, JsonElement>
                    {
                        ["Environment"] = JsonSerializer.SerializeToElement("staging"),
                        ["Version"] = JsonSerializer.SerializeToElement("1.2.3")
                    }
                }
            ],
            Actions =
            [
                new EntryAction
                {
                    Label = "Approve",
                    Style = ActionStyle.Success,
                    OnSuccess = PostActionBehavior.Archive,
                    Command = new ActionCommand
                    {
                        Type = CommandType.Cli,
                        Program = "echo",
                        Args = ["approved"]
                    }
                }
            ]
        };
    }

    private string DropEntryToInbox(Entry entry, string? filename = null)
    {
        var json = JsonSerializer.Serialize(entry, WriteOptions);
        filename ??= $"{Guid.NewGuid():N}.json";
        var filePath = Path.Combine(_tempDir, "inbox", filename);
        File.WriteAllText(filePath, json);
        return filePath;
    }

    // --- Inbox Processing Tests ---

    [Fact]
    public void ProcessInbox_PicksUpValidEntries()
    {
        var entry = CreateSampleEntry("Inbox Test");
        DropEntryToInbox(entry, "test-entry.json");

        var results = _store.ProcessInbox();

        Assert.Single(results);
        Assert.Equal("Inbox Test", results[0].Title);
        Assert.Equal(EntryStatus.Pending, results[0].Status);
        Assert.NotNull(results[0].ReceivedAt);
    }

    [Fact]
    public void ProcessInbox_MovesFileFromInboxToActive()
    {
        DropEntryToInbox(CreateSampleEntry(), "move-test.json");

        _store.ProcessInbox();

        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_tempDir, "inbox"), "*.json"));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_tempDir, "active"), "*.json"));
    }

    [Fact]
    public void ProcessInbox_RejectsInvalidSchemaVersion()
    {
        var entry = CreateSampleEntry();
        entry.SchemaVersion = "999";
        DropEntryToInbox(entry, "bad-version.json");

        var results = _store.ProcessInbox();

        Assert.Empty(results);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_tempDir, "errors"), "*.json"));
    }

    [Fact]
    public void ProcessInbox_RejectsMissingRequiredFields()
    {
        var json = JsonSerializer.Serialize(new { schemaVersion = "1", type = "", source = "x", title = "y" }, WriteOptions);
        File.WriteAllText(Path.Combine(_tempDir, "inbox", "bad-entry.json"), json);

        var results = _store.ProcessInbox();

        Assert.Empty(results);
    }

    [Fact]
    public void ProcessInbox_AssignsIdIfMissing()
    {
        var json = """
        {
            "schemaVersion": "1",
            "type": "test",
            "source": "unit-test",
            "title": "No ID Entry"
        }
        """;
        File.WriteAllText(Path.Combine(_tempDir, "inbox", "no-id.json"), json);

        var results = _store.ProcessInbox();

        Assert.Single(results);
        Assert.False(string.IsNullOrWhiteSpace(results[0].Id));
    }

    [Fact]
    public void ProcessInbox_HandlesMultipleFiles()
    {
        DropEntryToInbox(CreateSampleEntry("Entry 1"), "entry1.json");
        DropEntryToInbox(CreateSampleEntry("Entry 2"), "entry2.json");
        DropEntryToInbox(CreateSampleEntry("Entry 3"), "entry3.json");

        var results = _store.ProcessInbox();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void PickupInboxFile_ReturnsParsedEntry()
    {
        var filePath = DropEntryToInbox(CreateSampleEntry("Pickup Test"));

        var result = _store.PickupInboxFile(filePath);

        Assert.NotNull(result);
        Assert.Equal("Pickup Test", result.Title);
        Assert.Equal("test", result.Type);
        Assert.Equal("unit-test", result.Source);
    }

    // --- Active Entry Tests ---

    [Fact]
    public void GetActiveEntries_ReturnsAllActiveEntries()
    {
        DropEntryToInbox(CreateSampleEntry("A", Severity.Low));
        DropEntryToInbox(CreateSampleEntry("B", Severity.High));
        _store.ProcessInbox();

        var entries = _store.GetActiveEntries();

        Assert.Equal(2, entries.Count);
        // Should be sorted by severity descending
        Assert.Equal(Severity.High, entries[0].Severity);
    }

    [Fact]
    public void GetEntry_ReturnsCorrectEntry()
    {
        DropEntryToInbox(CreateSampleEntry("Find Me"));
        var results = _store.ProcessInbox();
        var id = results[0].Id;

        var entry = _store.GetEntry(id);

        Assert.NotNull(entry);
        Assert.Equal("Find Me", entry.Title);
    }

    [Fact]
    public void GetEntry_ReturnsNullForUnknownId()
    {
        var result = _store.GetEntry("nonexistent-id");
        Assert.Null(result);
    }

    // --- Mark Viewed Tests ---

    [Fact]
    public void MarkViewed_UpdatesStatus()
    {
        DropEntryToInbox(CreateSampleEntry("View Me"));
        var results = _store.ProcessInbox();
        var id = results[0].Id;

        var entry = _store.MarkViewed(id);

        Assert.NotNull(entry);
        Assert.Equal(EntryStatus.Viewed, entry.Status);
        Assert.NotNull(entry.ViewedAt);
    }

    [Fact]
    public void MarkViewed_DoesNotDowngradeFromViewed()
    {
        DropEntryToInbox(CreateSampleEntry("Already Viewed"));
        var results = _store.ProcessInbox();
        var id = results[0].Id;

        _store.MarkViewed(id);
        var viewedAt = _store.GetEntry(id)!.ViewedAt;

        // Second call should not change the timestamp
        _store.MarkViewed(id);
        var entry = _store.GetEntry(id);

        Assert.Equal(viewedAt, entry!.ViewedAt);
    }

    // --- Archive Tests ---

    [Fact]
    public void ArchiveEntry_MovesToArchive()
    {
        DropEntryToInbox(CreateSampleEntry("Archive Me"));
        var results = _store.ProcessInbox();
        var id = results[0].Id;

        var outcome = new EntryOutcome
        {
            Action = "Approved",
            Success = true,
            ResultMessage = "Test approval"
        };

        var archived = _store.ArchiveEntry(id, outcome);

        Assert.NotNull(archived);
        Assert.Equal(EntryStatus.Archived, archived.Status);
        Assert.Equal("Approved", archived.Outcome!.Action);

        // Should no longer be in active
        Assert.Null(_store.GetEntry(id));

        // Should be in archive directory
        Assert.True(File.Exists(Path.Combine(_tempDir, "archive", $"{id}.json")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "active", $"{id}.json")));
    }

    [Fact]
    public void ArchiveEntry_ReturnsNullForUnknownId()
    {
        var result = _store.ArchiveEntry("nonexistent", new EntryOutcome { Action = "test" });
        Assert.Null(result);
    }

    // --- Delete Tests ---

    [Fact]
    public void DeleteEntry_RemovesPermanently()
    {
        DropEntryToInbox(CreateSampleEntry("Delete Me"));
        var results = _store.ProcessInbox();
        var id = results[0].Id;

        var deleted = _store.DeleteEntry(id);

        Assert.True(deleted);
        Assert.Null(_store.GetEntry(id));
        Assert.False(File.Exists(Path.Combine(_tempDir, "active", $"{id}.json")));
        // Should NOT be in archive
        Assert.False(File.Exists(Path.Combine(_tempDir, "archive", $"{id}.json")));
    }

    [Fact]
    public void DeleteEntry_ReturnsFalseForUnknownId()
    {
        Assert.False(_store.DeleteEntry("nonexistent"));
    }

    // --- History / Archive Retrieval Tests ---

    [Fact]
    public void GetArchivedEntries_ReturnsArchivedItems()
    {
        DropEntryToInbox(CreateSampleEntry("History 1"));
        DropEntryToInbox(CreateSampleEntry("History 2"));
        var results = _store.ProcessInbox();

        foreach (var entry in results)
        {
            _store.ArchiveEntry(entry.Id, new EntryOutcome
            {
                Action = "Dismissed",
                Success = true
            });
        }

        var archived = _store.GetArchivedEntries();

        Assert.Equal(2, archived.Count);
    }

    [Fact]
    public void GetArchivedEntries_FiltersByType()
    {
        var entry1 = CreateSampleEntry("Type A");
        entry1.Type = "alpha";
        var entry2 = CreateSampleEntry("Type B");
        entry2.Type = "beta";

        DropEntryToInbox(entry1, "a.json");
        DropEntryToInbox(entry2, "b.json");
        var results = _store.ProcessInbox();

        foreach (var e in results)
            _store.ArchiveEntry(e.Id, new EntryOutcome { Action = "test" });

        var filtered = _store.GetArchivedEntries(type: "alpha");

        Assert.Single(filtered);
        Assert.Equal("alpha", filtered[0].Type);
    }

    [Fact]
    public void GetArchivedEntry_ReturnsSingleEntry()
    {
        DropEntryToInbox(CreateSampleEntry("Find Archived"));
        var results = _store.ProcessInbox();
        var id = results[0].Id;
        _store.ArchiveEntry(id, new EntryOutcome { Action = "test" });

        var entry = _store.GetArchivedEntry(id);

        Assert.NotNull(entry);
        Assert.Equal("Find Archived", entry.Title);
    }

    // --- Stats Tests ---

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        DropEntryToInbox(CreateSampleEntry("Stat 1", Severity.High));
        DropEntryToInbox(CreateSampleEntry("Stat 2", Severity.Low));
        DropEntryToInbox(CreateSampleEntry("Stat 3", Severity.High));
        _store.ProcessInbox();

        // View one
        var entries = _store.GetActiveEntries();
        _store.MarkViewed(entries[0].Id);

        var stats = _store.GetStats();

        Assert.Equal(3, stats.TotalPending + stats.TotalViewed);
        Assert.True(stats.TotalViewed >= 1);
        Assert.True(stats.CountByType.ContainsKey("test"));
        Assert.Equal(3, stats.CountByType["test"]);
    }

    // --- Full Lifecycle Test ---

    [Fact]
    public void FullLifecycle_InboxToArchive()
    {
        // 1. Drop entry to inbox
        var original = CreateSampleEntry("Lifecycle Test", Severity.Critical);
        DropEntryToInbox(original, "lifecycle.json");

        // 2. Process inbox
        var pickupResults = _store.ProcessInbox();
        Assert.Single(pickupResults);
        var entry = pickupResults[0];
        Assert.Equal(EntryStatus.Pending, entry.Status);

        // 3. View entry
        _store.MarkViewed(entry.Id);
        var viewed = _store.GetEntry(entry.Id);
        Assert.NotNull(viewed);
        Assert.Equal(EntryStatus.Viewed, viewed.Status);

        // 4. Archive with outcome
        var archived = _store.ArchiveEntry(entry.Id, new EntryOutcome
        {
            Action = "Approve",
            Success = true,
            ResultMessage = "Approved via test"
        });
        Assert.NotNull(archived);
        Assert.Equal(EntryStatus.Archived, archived.Status);
        Assert.Equal("Approve", archived.Outcome!.Action);

        // 5. Verify removed from active, available in archive
        Assert.Empty(_store.GetActiveEntries());
        var fromArchive = _store.GetArchivedEntry(archived.Id);
        Assert.NotNull(fromArchive);
        Assert.Equal("Lifecycle Test", fromArchive.Title);
    }
}
