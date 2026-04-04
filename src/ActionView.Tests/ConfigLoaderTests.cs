using System.Text.Json;
using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_config_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Load_WithNoConfig_UsesDefaults()
    {
        var prevActionView = Environment.GetEnvironmentVariable("ACTIONVIEW_CONFIG");
        var prevXdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("ACTIONVIEW_CONFIG", null);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
        try
        {
            var config = ConfigLoader.Load();

            Assert.Equal(AppConfig.DefaultDataDirectory, config.DataDirectory);
            Assert.True(config.Notifications.Enabled);
            Assert.Empty(config.Secrets);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACTIONVIEW_CONFIG", prevActionView);
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", prevXdg);
        }
    }

    [Fact]
    public void Load_WithExplicitPath_LoadsConfig()
    {
        var configPath = Path.Combine(_tempDir, "test-config.json");
        var appConfig = new AppConfig
        {
            DataDirectory = Path.Combine(_tempDir, "data"),
            Notifications = new NotificationConfig { Enabled = false },
            Secrets = new Dictionary<string, string>
            {
                ["TOKEN"] = "my-token"
            }
        };
        var json = JsonSerializer.Serialize(appConfig, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        File.WriteAllText(configPath, json);

        var loaded = ConfigLoader.Load(explicitPath: configPath);

        Assert.Equal(Path.Combine(_tempDir, "data"), loaded.DataDirectory);
        Assert.False(loaded.Notifications.Enabled);
        Assert.Equal("my-token", loaded.Secrets["TOKEN"]);
    }

    [Fact]
    public void Load_WithRelativeDataDir_ResolvesAgainstConfigLocation()
    {
        var configPath = Path.Combine(_tempDir, "relative-config.json");
        var json = """{"dataDirectory": "mydata"}""";
        File.WriteAllText(configPath, json);

        var loaded = ConfigLoader.Load(explicitPath: configPath);

        var expected = Path.GetFullPath(Path.Combine(_tempDir, "mydata"));
        Assert.Equal(expected, loaded.DataDirectory);
    }

    [Fact]
    public void Load_FromEnvVar_LoadsConfig()
    {
        var configPath = Path.Combine(_tempDir, "env-config.json");
        var json = """{"notifications": {"enabled": false}}""";
        File.WriteAllText(configPath, json);

        Environment.SetEnvironmentVariable("ACTIONVIEW_CONFIG", configPath);
        try
        {
            var loaded = ConfigLoader.Load();
            Assert.False(loaded.Notifications.Enabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACTIONVIEW_CONFIG", null);
        }
    }

    [Fact]
    public void EnsureDirectories_CreatesAllSubdirectories()
    {
        var dataDir = Path.Combine(_tempDir, "ensure-test");

        ConfigLoader.EnsureDirectories(dataDir);

        Assert.True(Directory.Exists(Path.Combine(dataDir, "inbox")));
        Assert.True(Directory.Exists(Path.Combine(dataDir, "active")));
        Assert.True(Directory.Exists(Path.Combine(dataDir, "archive")));
        Assert.True(Directory.Exists(Path.Combine(dataDir, "errors")));
    }

    [Fact]
    public void Load_ExplicitPathTakesPrecedenceOverEnvVar()
    {
        var envConfigPath = Path.Combine(_tempDir, "env.json");
        File.WriteAllText(envConfigPath, """{"notifications": {"enabled": false}}""");

        var explicitConfigPath = Path.Combine(_tempDir, "explicit.json");
        File.WriteAllText(explicitConfigPath, """{"notifications": {"enabled": true}}""");

        Environment.SetEnvironmentVariable("ACTIONVIEW_CONFIG", envConfigPath);
        try
        {
            var loaded = ConfigLoader.Load(explicitPath: explicitConfigPath);
            Assert.True(loaded.Notifications.Enabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACTIONVIEW_CONFIG", null);
        }
    }

    [Fact]
    public void Load_FromXdgConfigHome_LoadsConfig()
    {
        var xdgDir = Path.Combine(_tempDir, "xdg-config");
        var actionViewDir = Path.Combine(xdgDir, "actionview");
        Directory.CreateDirectory(actionViewDir);
        var configPath = Path.Combine(actionViewDir, "actionview.json");
        File.WriteAllText(configPath, """{"notifications": {"enabled": false}}""");

        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", xdgDir);
        try
        {
            var loaded = ConfigLoader.Load();
            Assert.False(loaded.Notifications.Enabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
        }
    }

    [Fact]
    public void Load_EnvVarTakesPrecedenceOverXdgConfigHome()
    {
        // Set up XDG config
        var xdgDir = Path.Combine(_tempDir, "xdg-config");
        var actionViewDir = Path.Combine(xdgDir, "actionview");
        Directory.CreateDirectory(actionViewDir);
        var xdgConfigPath = Path.Combine(actionViewDir, "actionview.json");
        File.WriteAllText(xdgConfigPath, """{"notifications": {"enabled": false}}""");

        // Set up ACTIONVIEW_CONFIG env var config
        var envConfigPath = Path.Combine(_tempDir, "env.json");
        File.WriteAllText(envConfigPath, """{"notifications": {"enabled": true}}""");

        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", xdgDir);
        Environment.SetEnvironmentVariable("ACTIONVIEW_CONFIG", envConfigPath);
        try
        {
            var loaded = ConfigLoader.Load();
            Assert.True(loaded.Notifications.Enabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
            Environment.SetEnvironmentVariable("ACTIONVIEW_CONFIG", null);
        }
    }

    [Fact]
    public void Load_XdgConfigHomeTakesPrecedenceOverAppsettings()
    {
        // Set up XDG config
        var xdgDir = Path.Combine(_tempDir, "xdg-config");
        var actionViewDir = Path.Combine(xdgDir, "actionview");
        Directory.CreateDirectory(actionViewDir);
        var xdgConfigPath = Path.Combine(actionViewDir, "actionview.json");
        File.WriteAllText(xdgConfigPath, """{"notifications": {"enabled": true}}""");

        // Set up appsettings config
        var appsettingsConfigPath = Path.Combine(_tempDir, "appsettings-config.json");
        File.WriteAllText(appsettingsConfigPath, """{"notifications": {"enabled": false}}""");

        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", xdgDir);
        try
        {
            var loaded = ConfigLoader.Load(appsettingsPath: appsettingsConfigPath);
            Assert.True(loaded.Notifications.Enabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
        }
    }
}
