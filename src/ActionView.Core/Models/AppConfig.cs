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
    /// <summary>Whether Windows toast notifications are enabled.</summary>
    public bool Enabled { get; set; } = true;
}
