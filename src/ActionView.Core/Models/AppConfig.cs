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

    /// <summary>
    /// Named secrets that map to environment variable names.
    /// Format: "FRIENDLY_NAME": "env:ENV_VAR_NAME"
    /// Used to resolve {{FRIENDLY_NAME}} in action commands.
    /// </summary>
    public Dictionary<string, string> Secrets { get; set; } = new();

    /// <summary>
    /// Default undo window in seconds for actions with undo commands.
    /// Per-action UndoWindowSeconds overrides this value. Default: 10.
    /// </summary>
    public int UndoWindowSeconds { get; set; } = 10;

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
