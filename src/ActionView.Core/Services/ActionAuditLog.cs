using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Append-only audit log of action executions, stored as JSON Lines at
/// <c>&lt;data&gt;/history/actions.jsonl</c> (one <see cref="ActionEvent"/> per
/// line). This is deliberately independent of an entry's active/archive/deleted
/// lifecycle so the "what happened to this entry" history survives dismiss and
/// permanent delete.
///
/// The file-based, append-only shape matches the rest of ActionView's storage
/// (inbox/active/archive). If richer querying is ever needed the same public
/// surface can be re-backed by SQLite without touching callers.
/// </summary>
public sealed class ActionAuditLog
{
    private readonly string _logPath;
    private readonly ILogger<ActionAuditLog> _logger;
    private readonly object _writeLock = new();

    private static readonly JsonSerializerOptions LineOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false, // one event per line
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public ActionAuditLog(string dataDirectory, ILogger<ActionAuditLog> logger)
    {
        _logPath = Path.Combine(dataDirectory, "history", "actions.jsonl");
        _logger = logger;
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
    }

    /// <summary>Absolute path to the JSON-Lines log file.</summary>
    public string LogPath => _logPath;

    /// <summary>Appends one event. Never throws to the caller — auditing must not break an action.</summary>
    public void Append(ActionEvent ev)
    {
        try
        {
            var line = JsonSerializer.Serialize(ev, LineOptions);
            lock (_writeLock)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append action audit event for entry {EntryId}", ev.EntryId);
        }
    }

    /// <summary>Returns events for a single entry, newest first (up to <paramref name="limit"/>).</summary>
    public IReadOnlyList<ActionEvent> GetForEntry(string entryId, int limit = 200)
        => Read(ev => string.Equals(ev.EntryId, entryId, StringComparison.Ordinal), limit);

    /// <summary>Returns the most recent events across all entries, newest first.</summary>
    public IReadOnlyList<ActionEvent> GetRecent(int limit = 200)
        => Read(_ => true, limit);

    private IReadOnlyList<ActionEvent> Read(Func<ActionEvent, bool> predicate, int limit)
    {
        if (!File.Exists(_logPath))
            return [];

        var results = new List<ActionEvent>();
        try
        {
            // Read tolerantly: skip malformed lines rather than failing the whole read.
            using var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ActionEvent? ev;
                try { ev = JsonSerializer.Deserialize<ActionEvent>(line, LineOptions); }
                catch { continue; }
                if (ev is not null && predicate(ev)) results.Add(ev);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read action audit log at {Path}", _logPath);
            return results;
        }

        results.Reverse(); // newest first
        return limit > 0 && results.Count > limit ? results.GetRange(0, limit) : results;
    }
}
