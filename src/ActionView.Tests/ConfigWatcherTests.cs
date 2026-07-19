using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class ConfigWatcherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigWatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_configwatcher_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "actionview.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private AppConfig Load(string json)
    {
        File.WriteAllText(_configPath, json);
        return ConfigLoader.Load(explicitPath: _configPath);
    }

    private (ConfigWatcher watcher, ViewStore store) CreateWatcher(AppConfig config)
    {
        var store = new ViewStore(config, NullLogger<ViewStore>.Instance);
        var watcher = new ConfigWatcher(config, store, NullLogger<ConfigWatcher>.Instance);
        return (watcher, store);
    }

    [Fact]
    public void ReloadFromDisk_AppliesViews_TagMatch_Notifications_Secrets()
    {
        var config = Load("""
        {
          "dataDirectory": "data",
          "tagMatchMode": "any",
          "notifications": { "enabled": true },
          "secrets": {},
          "views": []
        }
        """);
        var (watcher, _) = CreateWatcher(config);

        var raised = 0;
        watcher.ConfigChanged += () => raised++;

        // External edit to the config file.
        File.WriteAllText(_configPath, """
        {
          "dataDirectory": "data",
          "tagMatchMode": "all",
          "notifications": { "enabled": false },
          "secrets": { "TOKEN": "abc" },
          "views": [ { "name": "Work", "tags": ["work"] } ]
        }
        """);

        var changed = watcher.ReloadFromDisk();

        Assert.True(changed);
        Assert.Equal(1, raised);
        Assert.Equal(TagMatchMode.All, config.TagMatchMode);
        Assert.False(config.Notifications.Enabled);
        Assert.Equal("abc", config.Secrets["TOKEN"]);
        var view = Assert.Single(config.Views);
        Assert.Equal("work", view.Id);       // normalized id
        Assert.Equal("Work", view.Name);
    }

    [Fact]
    public void ReloadFromDisk_DoesNotTouchFileAccess()
    {
        var config = Load("""
        {
          "dataDirectory": "data",
          "fileAccess": { "allowedRoots": ["initial"] },
          "views": []
        }
        """);
        var (watcher, _) = CreateWatcher(config);

        // Capture the resolved (absolute) roots the loader produced.
        var originalRoots = config.FileAccess.AllowedRoots.ToList();
        Assert.Single(originalRoots);

        File.WriteAllText(_configPath, """
        {
          "dataDirectory": "data",
          "fileAccess": { "allowedRoots": ["changed"] },
          "views": [ { "name": "Work" } ]
        }
        """);

        var changed = watcher.ReloadFromDisk();

        Assert.True(changed); // views changed, so a reload happened
        // FileAccess is a security boundary: it must NOT hot-reload.
        Assert.Equal(originalRoots, config.FileAccess.AllowedRoots);
        Assert.DoesNotContain(config.FileAccess.AllowedRoots, r => r.Contains("changed"));
    }

    [Fact]
    public void ReloadFromDisk_UnchangedContent_IsNoOp_AndRaisesNothing()
    {
        var config = Load("""
        {
          "dataDirectory": "data",
          "tagMatchMode": "all",
          "views": [ { "id": "work", "name": "Work", "tags": ["work"] } ]
        }
        """);
        var (watcher, _) = CreateWatcher(config);

        var raised = 0;
        watcher.ConfigChanged += () => raised++;

        // No edit — reloading identical content must be a no-op.
        var changed = watcher.ReloadFromDisk();

        Assert.False(changed);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ReloadFromDisk_AfterSaveViews_IsNoOp_NoSelfWriteEcho()
    {
        var config = Load("""{ "dataDirectory": "data", "views": [] }""");
        var (watcher, store) = CreateWatcher(config);

        var raised = 0;
        watcher.ConfigChanged += () => raised++;

        // A UI save writes normalized views to the same file the watcher observes.
        store.SaveViews([new SavedView { Name = "Work", Tags = ["work"] }]);

        // The watcher would fire; reloading must recognise its own write and no-op.
        var changed = watcher.ReloadFromDisk();

        Assert.False(changed);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ReloadFromDisk_SecretsAreLive_ForSecretResolver()
    {
        var config = Load("""
        {
          "dataDirectory": "data",
          "secrets": { "TOKEN": "literal-old" },
          "views": []
        }
        """);
        var (watcher, _) = CreateWatcher(config);

        // A resolver constructed once (as the DI singleton would be).
        var resolver = new SecretResolver(config);
        Assert.Equal("literal-old", resolver.Resolve("{{TOKEN}}"));

        File.WriteAllText(_configPath, """
        {
          "dataDirectory": "data",
          "secrets": { "TOKEN": "literal-new" },
          "views": []
        }
        """);

        var changed = watcher.ReloadFromDisk();

        Assert.True(changed);
        // The pre-existing resolver must see the new value without being rebuilt.
        Assert.Equal("literal-new", resolver.Resolve("{{TOKEN}}"));
    }

    [Fact]
    public void ReloadFromDisk_NoSourceFile_ReturnsFalse()
    {
        var config = new AppConfig { SourcePath = null };
        var (watcher, _) = CreateWatcher(config);

        Assert.False(watcher.ReloadFromDisk());
    }
}
