using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class ActionAuditLogTests : IDisposable
{
    private readonly string _tempDir;

    public ActionAuditLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_audit_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private ActionAuditLog Create() => new(_tempDir, NullLogger<ActionAuditLog>.Instance);

    private static ActionEvent Event(string entryId, string label, bool success = true, string target = "entry") => new()
    {
        EntryId = entryId,
        EntryTitle = $"Title {entryId}",
        ActionLabel = label,
        Target = target,
        Success = success,
    };

    [Fact]
    public void Append_Then_GetForEntry_ReturnsNewestFirst()
    {
        var log = Create();
        log.Append(Event("e1", "Approve"));
        log.Append(Event("e1", "Submit Review"));
        log.Append(Event("e2", "Open PR"));

        var e1 = log.GetForEntry("e1");
        Assert.Equal(2, e1.Count);
        Assert.Equal("Submit Review", e1[0].ActionLabel); // newest first
        Assert.Equal("Approve", e1[1].ActionLabel);

        var e2 = log.GetForEntry("e2");
        Assert.Single(e2);
        Assert.Equal("Open PR", e2[0].ActionLabel);
    }

    [Fact]
    public void Log_Is_JsonLines_OnePerEvent()
    {
        var log = Create();
        log.Append(Event("e1", "A"));
        log.Append(Event("e1", "B"));

        var lines = File.ReadAllLines(log.LogPath);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, l => Assert.DoesNotContain('\n', l));
        // camelCase serialization
        Assert.Contains("\"actionLabel\"", lines[0]);
    }

    [Fact]
    public void History_Survives_A_Simulated_Delete()
    {
        // The whole point: the audit log is independent of entry lifecycle.
        var log = Create();
        log.Append(Event("gone", "Approve"));
        log.Append(new ActionEvent { EntryId = "gone", ActionLabel = "Deleted", Target = "system", Success = true });

        // Entry itself would be removed from active/archive here; the log persists.
        var history = log.GetForEntry("gone");
        Assert.Equal(2, history.Count);
        Assert.Contains(history, e => e is { Target: "system", ActionLabel: "Deleted" });
    }

    [Fact]
    public void Read_Skips_Malformed_Lines()
    {
        var log = Create();
        log.Append(Event("e1", "Good"));
        File.AppendAllText(log.LogPath, "this is not json" + Environment.NewLine);
        log.Append(Event("e1", "AlsoGood"));

        var history = log.GetForEntry("e1");
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void GetForEntry_RespectsLimit()
    {
        var log = Create();
        for (var i = 0; i < 10; i++) log.Append(Event("e1", $"A{i}"));

        var limited = log.GetForEntry("e1", limit: 3);
        Assert.Equal(3, limited.Count);
        Assert.Equal("A9", limited[0].ActionLabel); // newest
    }

    [Fact]
    public void GetRecent_ReturnsAcrossEntries_NewestFirst()
    {
        var log = Create();
        log.Append(Event("e1", "First"));
        log.Append(Event("e2", "Second"));

        var recent = log.GetRecent();
        Assert.Equal(2, recent.Count);
        Assert.Equal("Second", recent[0].ActionLabel);
    }

    [Fact]
    public void GetForEntry_Empty_WhenNoLog()
    {
        var log = Create();
        Assert.Empty(log.GetForEntry("nope"));
    }
}
