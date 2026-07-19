using System.Text.Json.Serialization;

namespace ActionView.Core.Models;

/// <summary>
/// Application configuration loaded from actionview.json.
/// </summary>
public sealed class AppConfig
{
    /// <summary>
    /// Root directory for inbox/, active/, archive/, errors/ subdirectories.
    /// If relative, resolved relative to the config file location.
    /// Defaults to ~/.actionview/ if not specified.
    /// </summary>
    public string DataDirectory { get; set; } = DefaultDataDirectory;

    /// <summary>Notification settings.</summary>
    public NotificationConfig Notifications { get; set; } = new();

    /// <summary>Template auto-discovery settings.</summary>
    public TemplatesConfig Templates { get; set; } = new();

    /// <summary>Ingest-time validation settings.</summary>
    public IngestConfig Ingest { get; set; } = new();

    /// <summary>Background action-execution settings (concurrency, timeout, output tail).</summary>
    public ActionsConfig Actions { get; set; } = new();

    /// <summary>
    /// Local-file access settings used by the /api/files endpoint to serve
    /// images and other assets that entries reference via file:// URLs.
    /// Ships locked down by default (empty allowlist = no local files served).
    /// </summary>
    public FileAccessConfig FileAccess { get; set; } = new();

    /// <summary>
    /// Named secrets that map to environment variable names.
    /// Format: "FRIENDLY_NAME": "env:ENV_VAR_NAME"
    /// Used to resolve {{FRIENDLY_NAME}} in action commands.
    /// </summary>
    public Dictionary<string, string> Secrets { get; set; } = new();

    /// <summary>
    /// Saved filter presets ("views") used to group the active feed into lanes
    /// (e.g., Work vs. Personal). Each view stores an optional entry type and/or
    /// a set of tags. The always-present "All" view is synthesized by the client
    /// and is not stored here. Can be edited from the dashboard, which persists
    /// changes back to this file via the /api/views endpoint.
    /// </summary>
    public List<SavedView> Views { get; set; } = new();

    /// <summary>
    /// Default semantics for multi-tag filters when a request/view does not
    /// specify its own. <see cref="TagMatchMode.Any"/> (OR) or
    /// <see cref="TagMatchMode.All"/> (AND). Default: Any.
    /// </summary>
    public TagMatchMode TagMatchMode { get; set; } = TagMatchMode.Any;

    /// <summary>
    /// Default undo window in seconds for actions with undo commands.
    /// Per-action UndoWindowSeconds overrides this value. Default: 10.
    /// </summary>
    public int UndoWindowSeconds { get; set; } = 10;

    /// <summary>
    /// URL the API host listens on. Accepts a full URL such as
    /// "http://localhost:5180" or "http://0.0.0.0:5180".
    /// CLI flags --urls and --port take precedence over this value.
    /// Defaults to "http://localhost:5173".
    /// </summary>
    public string ListenUrl { get; set; } = DefaultListenUrl;

    /// <summary>
    /// When true (default), the API host watches the source actionview.json for
    /// external edits and hot-reloads the runtime-safe slices of config
    /// (<see cref="Views"/>, <see cref="TagMatchMode"/>, <see cref="Notifications"/>,
    /// <see cref="Secrets"/>) without a restart. Startup-bound settings
    /// (DataDirectory, Templates, Ingest, FileAccess, ListenUrl) are never
    /// hot-reloaded. This flag is read once at startup and cannot itself be
    /// hot-reloaded.
    /// </summary>
    public bool WatchConfig { get; set; } = true;

    /// <summary>
    /// Absolute path to the actionview.json this config was loaded from, captured
    /// by <see cref="Services.ConfigLoader"/>. Null when no config file was found
    /// (defaults in use). Used by the views write-back path to update the same
    /// file. Never serialized.
    /// </summary>
    [JsonIgnore]
    public string? SourcePath { get; set; }

    public const string DefaultListenUrl = "http://localhost:5173";

    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".actionview");
}

public sealed class NotificationConfig
{
    /// <summary>Whether notifications are enabled globally.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Palantir Windows toast notification settings.</summary>
    public PalantirConfig Palantir { get; set; } = new();
}

/// <summary>
/// Configuration for Palantir Windows toast notifications.
/// Palantir is invoked via <c>dnx palantir</c> (.NET 10 tool execution).
/// </summary>
public sealed class PalantirConfig
{
    /// <summary>Whether Palantir toast notifications are enabled. Default: true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Toast duration: "short" (~5 seconds) or "long" (~25 seconds).
    /// Null uses Palantir's default (short).
    /// </summary>
    public string? Duration { get; set; }

    /// <summary>Attribution text shown at the bottom of the toast.</summary>
    public string? Attribution { get; set; } = "ActionView";

    /// <summary>
    /// URL to open when the toast body is clicked.
    /// Defaults to the ActionView dashboard URL.
    /// </summary>
    public string? LaunchUrl { get; set; } = "http://localhost:5173";

    /// <summary>Path or URL to an image used as the toast app logo.</summary>
    public string? Image { get; set; }

    /// <summary>
    /// Audio sound name (e.g., "default", "im", "mail", "reminder", "sms").
    /// Null uses the system default notification sound.
    /// </summary>
    public string? Audio { get; set; }

    /// <summary>Suppress audio on toast notifications.</summary>
    public bool Silent { get; set; }

    /// <summary>
    /// Toast scenario: "default", "alarm", "reminder", or "incomingCall".
    /// Null uses Palantir's default.
    /// </summary>
    public string? Scenario { get; set; }

    /// <summary>
    /// Path or URL to a hero image displayed at the top of the toast.
    /// </summary>
    public string? HeroImage { get; set; }
}

/// <summary>
/// Configuration for external template auto-discovery.
/// </summary>
public sealed class TemplatesConfig
{
    /// <summary>
    /// External directory to scan for template JSON files on startup.
    /// If relative, resolved against the config file location.
    /// Templates found here are auto-registered and tracked via a manifest;
    /// removing a template from this directory will remove it from the registry
    /// on next startup, but templates registered by other means are never touched.
    /// </summary>
    public string? ExternalDirectory { get; set; }

    /// <summary>
    /// Whether to scan subdirectories of ExternalDirectory recursively.
    /// Default: false (only top-level .json files are scanned).
    /// </summary>
    public bool Recursive { get; set; }
}

/// <summary>
/// Ingest-time validation settings.
/// </summary>
public sealed class IngestConfig
{    /// <summary>
    /// When true, entries that fail JSON-Schema validation or produce normalization
    /// warnings (e.g. a missing required content block, a disallowed tag) are rejected
    /// at ingest and routed to <c>errors/</c> with a precise reason, instead of shipping
    /// with only a logged warning.
    ///
    /// Default: false. The non-destructive default preserves ActionView's promise never
    /// to silently drop a human-review item; strict producers (or the emit → validate →
    /// fix loop) opt in per submission, per type (template <c>strict</c>), or globally here.
    /// </summary>
    public bool Strict { get; set; }
}

/// <summary>
/// Settings for background action execution (jobs).
/// </summary>
public sealed class ActionsConfig
{
    /// <summary>Maximum number of action jobs allowed to run at once. Excess jobs queue as pending. Default 4.</summary>
    public int MaxConcurrentJobs { get; set; } = 4;

    /// <summary>
    /// Default per-job timeout in seconds. When &gt; 0, a job that exceeds this is
    /// cancelled (process tree killed) and marked failed. 0 means no timeout. Default 0.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; }

    /// <summary>Maximum number of streamed output lines retained on a job (rolling tail). Default 200.</summary>
    public int OutputTailLines { get; set; } = 200;
}

/// <summary>
/// Controls which local files the /api/files endpoint will serve.
///
/// The endpoint exists so that entries can reference local images
/// (e.g. <c>file:///C:/path/to/frame.jpg</c> in markdown). Browsers
/// refuse to load <c>file://</c> URLs from an <c>http://</c> origin,
/// so the client rewrites them to <c>/api/files?path=...</c>, which
/// this configuration gates.
///
/// Ships locked down: an empty <see cref="AllowedRoots"/> list means
/// the endpoint serves nothing. Add absolute directory paths to opt
/// individual trees in.
/// </summary>
public sealed class FileAccessConfig
{
    /// <summary>
    /// Absolute directory paths whose contents may be served.
    /// A requested path is served only if, after canonicalisation
    /// (full path, link target resolution), it lies underneath one
    /// of these roots. Paths in this list that are not absolute are
    /// resolved relative to the config file location, the same way
    /// <see cref="AppConfig.DataDirectory"/> is resolved.
    /// </summary>
    public List<string> AllowedRoots { get; set; } = new();

    /// <summary>
    /// Maximum file size in bytes that the endpoint will return.
    /// Files larger than this are rejected with HTTP 413.
    /// Defaults to 20 MiB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 20L * 1024 * 1024;
}
