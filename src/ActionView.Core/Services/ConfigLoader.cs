using System.Text.Json;
using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Loads and resolves ActionView configuration from actionview.json.
/// Resolution order: CLI arg > env var > XDG config > appsettings > current directory.
/// </summary>
public sealed class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Load configuration using the layered precedence:
    /// 1. Explicit path (CLI --config arg)
    /// 2. ACTIONVIEW_CONFIG environment variable
    /// 3. $XDG_CONFIG_HOME/actionview/actionview.json
    /// 4. appsettings path (ActionView:ConfigPath)
    /// 5. ./actionview.json in current directory
    /// </summary>
    public static AppConfig Load(string? explicitPath = null, string? appsettingsPath = null)
    {
        var configPath = ResolveConfigPath(explicitPath, appsettingsPath);

        if (configPath is null || !File.Exists(configPath))
        {
            // No config file found; use defaults
            var defaultConfig = new AppConfig();
            EnsureDirectories(defaultConfig.DataDirectory);
            return defaultConfig;
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();

        // Remember where this config came from so the views write-back path can
        // update the same file in place.
        config.SourcePath = Path.GetFullPath(configPath);

        // Resolve relative paths against config file location
        var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;

        if (!Path.IsPathRooted(config.DataDirectory))
        {
            config.DataDirectory = Path.GetFullPath(Path.Combine(configDir, config.DataDirectory));
        }

        if (config.Templates.ExternalDirectory is not null
            && !Path.IsPathRooted(config.Templates.ExternalDirectory))
        {
            config.Templates.ExternalDirectory =
                Path.GetFullPath(Path.Combine(configDir, config.Templates.ExternalDirectory));
        }

        // Resolve relative paths in FileAccess.AllowedRoots against the config
        // file location, the same way DataDirectory and ExternalDirectory are.
        // Empty / whitespace entries are dropped.
        var resolvedRoots = new List<string>(config.FileAccess.AllowedRoots.Count);
        foreach (var root in config.FileAccess.AllowedRoots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var resolved = Path.IsPathRooted(root)
                ? Path.GetFullPath(root)
                : Path.GetFullPath(Path.Combine(configDir, root));
            resolvedRoots.Add(resolved);
        }
        config.FileAccess.AllowedRoots = resolvedRoots;

        EnsureDirectories(config.DataDirectory);
        return config;
    }

    private static string? ResolveConfigPath(string? explicitPath, string? appsettingsPath)
    {
        // 1. Explicit path (CLI --config)
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return Path.GetFullPath(explicitPath);

        // 2. Environment variable
        var envPath = Environment.GetEnvironmentVariable("ACTIONVIEW_CONFIG");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return Path.GetFullPath(envPath);

        // 3. XDG_CONFIG_HOME/actionview/actionview.json
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
        {
            var xdgPath = Path.Combine(xdgConfigHome, "actionview", "actionview.json");
            if (File.Exists(xdgPath))
                return Path.GetFullPath(xdgPath);
        }

        // 4. appsettings-provided path
        if (!string.IsNullOrWhiteSpace(appsettingsPath) && File.Exists(appsettingsPath))
            return Path.GetFullPath(appsettingsPath);

        // 5. Current directory
        var localPath = Path.Combine(Directory.GetCurrentDirectory(), "actionview.json");
        if (File.Exists(localPath))
            return localPath;

        return null;
    }

    /// <summary>
    /// Ensures the data directory structure exists.
    /// </summary>
    public static void EnsureDirectories(string dataDirectory)
    {
        Directory.CreateDirectory(Path.Combine(dataDirectory, "inbox"));
        Directory.CreateDirectory(Path.Combine(dataDirectory, "active"));
        Directory.CreateDirectory(Path.Combine(dataDirectory, "archive"));
        Directory.CreateDirectory(Path.Combine(dataDirectory, "errors"));
    }
}
