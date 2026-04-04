using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Watches the inbox directory for new JSON files using FileSystemWatcher.
/// When a new file appears, it is picked up by the EntryStore.
/// Raises an event so the API layer can push notifications.
/// </summary>
public sealed class InboxWatcher : IDisposable
{
    private readonly EntryStore _entryStore;
    private readonly ILogger<InboxWatcher> _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly string _inboxPath;

    /// <summary>
    /// Raised when one or more new entries are picked up from the inbox.
    /// </summary>
    public event Action<List<Entry>>? EntriesReceived;

    public InboxWatcher(string dataDirectory, EntryStore entryStore, ILogger<InboxWatcher> logger)
    {
        _entryStore = entryStore;
        _logger = logger;
        _inboxPath = Path.Combine(dataDirectory, "inbox");

        Directory.CreateDirectory(_inboxPath);

        _watcher = new FileSystemWatcher(_inboxPath, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = false // Started explicitly
        };

        _watcher.Created += OnFileCreated;
        _watcher.Error += OnWatcherError;
    }

    /// <summary>Start watching the inbox directory.</summary>
    public void Start()
    {
        // First, process any files already in the inbox
        var existing = _entryStore.ProcessInbox();
        if (existing.Count > 0)
        {
            _logger.LogInformation("Processed {Count} existing inbox entries on startup", existing.Count);
            EntriesReceived?.Invoke(existing);
        }

        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("Watching inbox directory: {Path}", _inboxPath);
    }

    /// <summary>Stop watching the inbox directory.</summary>
    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
        _logger.LogInformation("Stopped watching inbox directory");
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        // Small delay to ensure the file is fully written
        Task.Delay(200).ContinueWith(_ =>
        {
            try
            {
                // Retry a few times in case the file is still being written
                Entry? entry = null;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        if (!File.Exists(e.FullPath)) return;
                        entry = _entryStore.PickupInboxFile(e.FullPath);
                        break;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        Thread.Sleep(300);
                    }
                }

                if (entry is not null)
                {
                    _logger.LogInformation("New entry from inbox: {Id} - {Title}", entry.Id, entry.Title);
                    EntriesReceived?.Invoke([entry]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing new inbox file: {Path}", e.FullPath);
            }
        });
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error");

        // Attempt to restart the watcher
        try
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.EnableRaisingEvents = true;
            _logger.LogInformation("FileSystemWatcher restarted after error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart FileSystemWatcher");
        }
    }

    public void Dispose()
    {
        _watcher.Created -= OnFileCreated;
        _watcher.Error -= OnWatcherError;
        _watcher.Dispose();
    }
}
