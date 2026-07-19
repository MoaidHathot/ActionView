using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Watches the source actionview.json for external edits and hot-reloads the
/// runtime-safe slices of configuration into the live singleton
/// <see cref="AppConfig"/> without a restart.
///
/// Only sections whose consumers read them live (or that we can swap safely)
/// are reloaded:
/// <list type="bullet">
///   <item><see cref="AppConfig.Views"/> — endpoints/<see cref="ViewStore"/> read live.</item>
///   <item><see cref="AppConfig.TagMatchMode"/> — endpoints read live.</item>
///   <item><see cref="AppConfig.Notifications"/> — <see cref="ToastNotifier"/> derefs from the config root live.</item>
///   <item><see cref="AppConfig.Secrets"/> — <see cref="SecretResolver"/> reads the config root live.</item>
/// </list>
///
/// Startup-bound settings (DataDirectory, Templates, Ingest, FileAccess,
/// ListenUrl) are deliberately NOT touched: they are resolved once and bound
/// into constructed services (file watchers, path resolvers) at startup, and
/// FileAccess is a security boundary that should not widen on a stray file edit.
///
/// The watcher never writes the config file. It re-reads through
/// <see cref="ConfigLoader.Load(string?, string?)"/> so load-time path
/// resolution and JSON parsing stay identical to startup, then applies only the
/// safe slices. A before/after comparison of the safe slice suppresses the echo
/// caused by <see cref="ViewStore.SaveViews"/> writing the file itself, and
/// keeps a hand-edited (non-normalized) file from looping.
/// </summary>
public sealed class ConfigWatcher : IDisposable
{
    private readonly AppConfig _config;
    private readonly ViewStore _viewStore;
    private readonly ILogger<ConfigWatcher> _logger;
    private readonly object _lock = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;

    /// <summary>
    /// Raised after a reload that actually changed one of the safe slices.
    /// Wired to a SignalR broadcast so connected dashboards re-fetch.
    /// </summary>
    public event Action? ConfigChanged;

    private static readonly JsonSerializerOptions CompareOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public ConfigWatcher(AppConfig config, ViewStore viewStore, ILogger<ConfigWatcher> logger)
    {
        _config = config;
        _viewStore = viewStore;
        _logger = logger;
    }

    /// <summary>
    /// Start watching the config file's directory for edits. No-op when the
    /// config was loaded from defaults (no source file on disk).
    /// </summary>
    public void StartWatching()
    {
        var path = _config.SourcePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogInformation("Config hot-reload disabled: no source config file on disk.");
            return;
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            _logger.LogWarning("Config hot-reload disabled: could not resolve directory for {Path}.", path);
            return;
        }

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        _watcher.Created += (_, _) => ScheduleReload();
        _watcher.Changed += (_, _) => ScheduleReload();
        _watcher.Renamed += (_, _) => ScheduleReload();
        _watcher.Error += (_, e) =>
            _logger.LogError(e.GetException(), "Config file watcher error");

        _logger.LogInformation("Watching config file for hot-reload: {Path}", path);
    }

    /// <summary>
    /// Debounce watcher events: wait 300ms after the last event before reloading,
    /// so an editor's multi-write save (or the atomic temp+rename swap) triggers
    /// only one reload.
    /// </summary>
    private void ScheduleReload()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            try
            {
                ReloadFromDisk();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Config hot-reload failed; keeping previous config in memory.");
            }
        }, null, 300, Timeout.Infinite);
    }

    /// <summary>
    /// Re-reads the source config file and applies the runtime-safe slices to the
    /// live <see cref="AppConfig"/>. Returns true when something actually changed
    /// (and <see cref="ConfigChanged"/> was raised); false on a no-op (unchanged
    /// content, including the self-write echo) or when there is no source file.
    /// </summary>
    public bool ReloadFromDisk()
    {
        var path = _config.SourcePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        // Re-read through the same loader used at startup so comment/enum parsing
        // and relative-path resolution stay identical.
        AppConfig fresh;
        try
        {
            fresh = ConfigLoader.Load(explicitPath: path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse {Path} during hot-reload; keeping previous config.", path);
            return false;
        }

        // Normalize the incoming views the same way SaveViews does, so the
        // comparison is stable (a hand-edited file missing ids won't loop).
        var freshViews = ViewStore.Normalize(fresh.Views);

        lock (_lock)
        {
            var before = Snapshot(_config.Views, _config.TagMatchMode, _config.Notifications, _config.Secrets);
            var after = Snapshot(freshViews, fresh.TagMatchMode, fresh.Notifications, fresh.Secrets);
            if (string.Equals(before, after, StringComparison.Ordinal))
                return false;

            // Apply only the safe slices. Views go through ViewStore so the
            // update is taken under its lock (concurrent with GET/PUT /api/views).
            _viewStore.SetViewsFromReload(fresh.Views);
            _config.TagMatchMode = fresh.TagMatchMode;
            _config.Notifications = fresh.Notifications; // ToastNotifier derefs the root live.
            _config.Secrets = fresh.Secrets;             // SecretResolver reads the root live.
        }

        _logger.LogInformation("Hot-reloaded config from {Path} (views/tagMatchMode/notifications/secrets).", path);
        ConfigChanged?.Invoke();
        return true;
    }

    private static string Snapshot(
        IReadOnlyList<SavedView> views,
        TagMatchMode tagMatchMode,
        NotificationConfig notifications,
        Dictionary<string, string> secrets)
    {
        return JsonSerializer.Serialize(
            new
            {
                views,
                tagMatchMode,
                notifications,
                secrets,
            },
            CompareOptions);
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _watcher?.Dispose();
        _watcher = null;
    }
}
