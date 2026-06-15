using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Manages reading, writing, and moving Entry JSON files across
/// the inbox/active/archive/errors directory structure.
/// </summary>
public sealed class EntryStore : IDisposable
{
    private readonly string _dataDirectory;
    private readonly ILogger<EntryStore> _logger;
    private readonly EntryNormalizer? _normalizer;
    private readonly ConcurrentDictionary<string, Entry> _activeCache = new();
    private readonly ConcurrentDictionary<string, byte> _internalDeletions = new();
    private FileSystemWatcher? _activeWatcher;

    /// <summary>Raised when entries are removed from active by an external process.</summary>
    public event Action<List<string>>? EntriesExternallyDeleted;

    /// <summary>Raised when entries are added to active by an external process.</summary>
    public event Action<List<Entry>>? EntriesExternallyAdded;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private string InboxDir => Path.Combine(_dataDirectory, "inbox");
    private string ActiveDir => Path.Combine(_dataDirectory, "active");
    private string ArchiveDir => Path.Combine(_dataDirectory, "archive");
    private string ErrorsDir => Path.Combine(_dataDirectory, "errors");

    public EntryStore(string dataDirectory, ILogger<EntryStore> logger, EntryNormalizer? normalizer = null)
    {
        _dataDirectory = dataDirectory;
        _logger = logger;
        _normalizer = normalizer;
        LoadActiveEntries();
    }

    /// <summary>
    /// Starts a FileSystemWatcher on the active directory to detect
    /// external changes (e.g. CLI deletes/dismisses).
    /// </summary>
    public void StartWatchingActive()
    {
        Directory.CreateDirectory(ActiveDir);

        _activeWatcher = new FileSystemWatcher(ActiveDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _activeWatcher.Deleted += OnActiveFileDeleted;
        _activeWatcher.Created += OnActiveFileCreated;
        _activeWatcher.Error += (_, e) =>
            _logger.LogError(e.GetException(), "Active directory watcher error");

        _logger.LogInformation("Watching active directory for external changes: {Path}", ActiveDir);
    }

    private void OnActiveFileDeleted(object sender, FileSystemEventArgs e)
    {
        var id = Path.GetFileNameWithoutExtension(e.Name);
        if (string.IsNullOrEmpty(id)) return;

        // If this deletion was triggered by our own code, ignore it
        if (_internalDeletions.TryRemove(id, out _)) return;

        // Only raise event if we had it cached (i.e. it was removed externally)
        if (_activeCache.TryRemove(id, out _))
        {
            _logger.LogInformation("Detected external deletion of active entry: {Id}", id);
            EntriesExternallyDeleted?.Invoke([id]);
        }
    }

    private void OnActiveFileCreated(object sender, FileSystemEventArgs e)
    {
        var id = Path.GetFileNameWithoutExtension(e.Name);
        if (string.IsNullOrEmpty(id)) return;

        // If we already have it cached, this was an internal operation
        if (_activeCache.ContainsKey(id)) return;

        // Small delay to ensure the file is fully written
        Task.Delay(200).ContinueWith(_ =>
        {
            try
            {
                if (!File.Exists(e.FullPath)) return;

                Entry? entry = null;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var json = File.ReadAllText(e.FullPath);
                        entry = JsonSerializer.Deserialize<Entry>(json, ReadOptions);
                        break;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        Thread.Sleep(300);
                    }
                }

                if (entry is not null && _activeCache.TryAdd(entry.Id, entry))
                {
                    _logger.LogInformation("Detected externally added active entry: {Id}", entry.Id);
                    EntriesExternallyAdded?.Invoke([entry]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing externally created active file: {Path}", e.FullPath);
            }
        });
    }

    /// <summary>
    /// Loads all active entries from disk into the in-memory cache.
    /// Called on startup and after inbox processing.
    /// </summary>
    private void LoadActiveEntries()
    {
        _activeCache.Clear();

        if (!Directory.Exists(ActiveDir)) return;

        foreach (var file in Directory.EnumerateFiles(ActiveDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<Entry>(json, ReadOptions);
                if (entry is not null)
                {
                    _activeCache[entry.Id] = entry;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load active entry from {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} active entries", _activeCache.Count);
    }

    /// <summary>
    /// Processes all files in the inbox: validates, assigns metadata, moves to active.
    /// Returns the list of newly activated entries.
    /// </summary>
    public List<Entry> ProcessInbox()
    {
        var newEntries = new List<Entry>();

        if (!Directory.Exists(InboxDir)) return newEntries;

        foreach (var file in Directory.EnumerateFiles(InboxDir, "*.json"))
        {
            try
            {
                var entry = PickupInboxFile(file);
                if (entry is not null)
                {
                    newEntries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inbox file {File}", file);
                MoveToErrors(file, ex.Message);
            }
        }

        return newEntries;
    }

    /// <summary>
    /// Picks up a single inbox file: validates, enriches, and moves to active.
    /// </summary>
    public Entry? PickupInboxFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var entry = JsonSerializer.Deserialize<Entry>(json, ReadOptions);

        if (entry is null)
        {
            MoveToErrors(filePath, "Failed to deserialize entry JSON");
            return null;
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(entry.Type) ||
            string.IsNullOrWhiteSpace(entry.Source) ||
            string.IsNullOrWhiteSpace(entry.Title))
        {
            MoveToErrors(filePath, "Missing required fields: type, source, or title");
            return null;
        }

        // Validate schema version
        if (entry.SchemaVersion != "1")
        {
            MoveToErrors(filePath, $"Unsupported schema version: {entry.SchemaVersion}. Expected: 1");
            return null;
        }

        // Enrich with backend metadata
        if (string.IsNullOrWhiteSpace(entry.Id))
            entry.Id = Guid.NewGuid().ToString("N");

        entry.Status = EntryStatus.Pending;
        entry.ReceivedAt = DateTimeOffset.UtcNow;

        // Apply template normalization
        _normalizer?.Normalize(entry);

        // Write to active directory
        var activeFilePath = Path.Combine(ActiveDir, $"{entry.Id}.json");
        var enrichedJson = JsonSerializer.Serialize(entry, WriteOptions);
        File.WriteAllText(activeFilePath, enrichedJson);

        // Remove from inbox
        File.Delete(filePath);

        // Cache it
        _activeCache[entry.Id] = entry;

        _logger.LogInformation("Picked up entry {Id}: {Title}", entry.Id, entry.Title);
        return entry;
    }

    /// <summary>
    /// Ingests an entry directly (from webhook/API), without going through inbox files.
    /// Validates, enriches, normalizes, and persists.
    /// </summary>
    public Entry? IngestEntry(Entry entry)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(entry.Type) ||
            string.IsNullOrWhiteSpace(entry.Source) ||
            string.IsNullOrWhiteSpace(entry.Title))
        {
            return null;
        }

        // Validate schema version
        if (!string.IsNullOrWhiteSpace(entry.SchemaVersion) && entry.SchemaVersion != "1")
        {
            return null;
        }

        entry.SchemaVersion = "1";

        // Enrich with backend metadata
        if (string.IsNullOrWhiteSpace(entry.Id))
            entry.Id = Guid.NewGuid().ToString("N");

        entry.Status = EntryStatus.Pending;
        entry.ReceivedAt = DateTimeOffset.UtcNow;

        // Apply template normalization
        _normalizer?.Normalize(entry);

        // Write to active directory
        var activeFilePath = Path.Combine(ActiveDir, $"{entry.Id}.json");
        var enrichedJson = JsonSerializer.Serialize(entry, WriteOptions);
        File.WriteAllText(activeFilePath, enrichedJson);

        // Cache it
        _activeCache[entry.Id] = entry;

        _logger.LogInformation("Ingested entry {Id}: {Title}", entry.Id, entry.Title);
        return entry;
    }

    /// <summary>
    /// Updates a mutable entry in place. Only active entries can be updated.
    /// </summary>
    public Entry? UpdateEntry(string id, Action<Entry> applyUpdates)
    {
        if (!_activeCache.TryGetValue(id, out var entry)) return null;

        applyUpdates(entry);
        SaveActiveEntry(entry);

        _logger.LogInformation("Updated entry {Id}: {Title}", id, entry.Title);
        return entry;
    }

    /// <summary>
    /// Moves an archived entry back to active (for undo/rollback).
    /// </summary>
    public Entry? UnarchiveEntry(string id)
    {
        var archivePath = Path.Combine(ArchiveDir, $"{id}.json");
        if (!File.Exists(archivePath)) return null;

        try
        {
            var json = File.ReadAllText(archivePath);
            var entry = JsonSerializer.Deserialize<Entry>(json, ReadOptions);
            if (entry is null) return null;

            // Reset status
            entry.Status = EntryStatus.Viewed;
            entry.Outcome = null;

            // Write to active
            var activePath = Path.Combine(ActiveDir, $"{entry.Id}.json");
            var updatedJson = JsonSerializer.Serialize(entry, WriteOptions);
            File.WriteAllText(activePath, updatedJson);

            // Remove from archive
            File.Delete(archivePath);

            // Cache it
            _activeCache[entry.Id] = entry;

            _logger.LogInformation("Unarchived entry {Id}: {Title}", id, entry.Title);
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unarchive entry {Id}", id);
            return null;
        }
    }

    /// <summary>Toggle the pinned state of an entry.</summary>
    public Entry? TogglePin(string id)
    {
        if (!_activeCache.TryGetValue(id, out var entry)) return null;

        entry.Pinned = !entry.Pinned;
        SaveActiveEntry(entry);

        _logger.LogInformation("Toggled pin for entry {Id}: Pinned={Pinned}", id, entry.Pinned);
        return entry;
    }

    /// <summary>Get all active (non-archived) entries.</summary>
    public IReadOnlyList<Entry> GetActiveEntries()
    {
        return _activeCache.Values
            .OrderByDescending(e => e.Pinned)
            .ThenByDescending(e => e.Priority)
            .ThenByDescending(e => e.Severity)
            .ThenByDescending(e => e.CreatedAt)
            .ToList();
    }

    /// <summary>Get a single active entry by ID.</summary>
    public Entry? GetEntry(string id)
    {
        return _activeCache.TryGetValue(id, out var entry) ? entry : null;
    }

    /// <summary>Mark an entry as viewed.</summary>
    public Entry? MarkViewed(string id)
    {
        if (!_activeCache.TryGetValue(id, out var entry)) return null;
        if (entry.Status == EntryStatus.Pending)
        {
            entry.Status = EntryStatus.Viewed;
            entry.ViewedAt = DateTimeOffset.UtcNow;
            SaveActiveEntry(entry);
        }
        return entry;
    }

    /// <summary>Archive an entry with an outcome.</summary>
    public Entry? ArchiveEntry(string id, EntryOutcome outcome)
    {
        if (!_activeCache.TryRemove(id, out var entry)) return null;

        entry.Status = EntryStatus.Archived;
        entry.Outcome = outcome;

        // Write to archive
        var archiveFilePath = Path.Combine(ArchiveDir, $"{entry.Id}.json");
        var json = JsonSerializer.Serialize(entry, WriteOptions);
        File.WriteAllText(archiveFilePath, json);

        // Mark as internal so the watcher ignores it
        _internalDeletions[entry.Id] = 0;

        // Remove from active
        var activeFilePath = Path.Combine(ActiveDir, $"{entry.Id}.json");
        if (File.Exists(activeFilePath))
            File.Delete(activeFilePath);

        _logger.LogInformation("Archived entry {Id}: {Action}", id, outcome.Action);
        return entry;
    }

    /// <summary>Permanently delete an entry.</summary>
    public bool DeleteEntry(string id)
    {
        if (!_activeCache.TryRemove(id, out _)) return false;

        // Mark as internal so the watcher ignores it
        _internalDeletions[id] = 0;

        var filePath = Path.Combine(ActiveDir, $"{id}.json");
        if (File.Exists(filePath))
            File.Delete(filePath);

        _logger.LogInformation("Deleted entry {Id}", id);
        return true;
    }

    /// <summary>Get archived entries, optionally filtered by type and re-sorted.</summary>
    public List<Entry> GetArchivedEntries(
        string? type = null, int limit = 50, int offset = 0,
        EntrySortField? sortField = null, SortDirection sortDir = SortDirection.Descending)
    {
        if (!Directory.Exists(ArchiveDir)) return [];

        var entries = new List<Entry>();
        foreach (var file in Directory.EnumerateFiles(ArchiveDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<Entry>(json, ReadOptions);
                if (entry is not null)
                {
                    if (type is null || entry.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                        entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read archive file {File}", file);
            }
        }

        // Default history order is most-recent-outcome first; an explicit sort
        // field overrides it. Sorting happens before pagination so the chosen
        // order is global, not just within a page.
        var sorted = sortField is null
            ? entries.OrderByDescending(e => e.Outcome?.Timestamp ?? e.CreatedAt).ToList()
            : EntrySorting.Sort(entries, sortField.Value, sortDir, pinnedFirst: false);

        return sorted
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>Get a single archived entry by ID.</summary>
    public Entry? GetArchivedEntry(string id)
    {
        var filePath = Path.Combine(ArchiveDir, $"{id}.json");
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Entry>(json, ReadOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Get dashboard statistics.</summary>
    public DashboardStats GetStats()
    {
        var entries = _activeCache.Values.ToList();
        return new DashboardStats
        {
            TotalPending = entries.Count(e => e.Status == EntryStatus.Pending),
            TotalViewed = entries.Count(e => e.Status == EntryStatus.Viewed),
            CountByType = entries.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count()),
            CountBySeverity = entries.GroupBy(e => e.Severity.ToString().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private void SaveActiveEntry(Entry entry)
    {
        var filePath = Path.Combine(ActiveDir, $"{entry.Id}.json");
        var json = JsonSerializer.Serialize(entry, WriteOptions);
        File.WriteAllText(filePath, json);
    }

    private void MoveToErrors(string filePath, string errorMessage)
    {
        var fileName = Path.GetFileName(filePath);
        var errorFilePath = Path.Combine(ErrorsDir, fileName);
        var errorInfoPath = Path.Combine(ErrorsDir, Path.ChangeExtension(fileName, ".error.txt"));

        try
        {
            if (File.Exists(errorFilePath))
                File.Delete(errorFilePath);

            File.Move(filePath, errorFilePath);
            File.WriteAllText(errorInfoPath, $"[{DateTimeOffset.UtcNow:o}] {errorMessage}");
            _logger.LogWarning("Moved invalid entry to errors: {File} - {Error}", fileName, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move file to errors directory: {File}", filePath);
        }
    }

    public void Dispose()
    {
        if (_activeWatcher is not null)
        {
            _activeWatcher.Deleted -= OnActiveFileDeleted;
            _activeWatcher.Created -= OnActiveFileCreated;
            _activeWatcher.Dispose();
            _activeWatcher = null;
        }
    }
}
