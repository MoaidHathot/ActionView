using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class ViewStoreTests : IDisposable
{
    private readonly string _tempDir;

    public ViewStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_views_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static ViewStore CreateStore(AppConfig config) =>
        new(config, NullLogger<ViewStore>.Instance);

    [Fact]
    public void SaveViews_PersistsToConfigFile_AndPreservesOtherKeys()
    {
        var configPath = Path.Combine(_tempDir, "actionview.json");
        File.WriteAllText(configPath, """
        {
          "dataDirectory": "data",
          "secrets": { "TOKEN": "abc" },
          "views": []
        }
        """);

        var config = new AppConfig { SourcePath = configPath };
        var store = CreateStore(config);

        var saved = store.SaveViews([
            new SavedView { Name = "Work", Tags = ["work"] },
            new SavedView { Name = "Deploys", Type = "deploy" },
        ]);

        Assert.Equal(2, saved.Count);
        Assert.Equal(2, config.Views.Count); // in-memory updated

        // File round-trips and preserves the other config keys.
        var reloaded = ConfigLoader.Load(explicitPath: configPath);
        Assert.Equal(2, reloaded.Views.Count);
        Assert.Contains(reloaded.Views, v => v.Name == "Work" && v.Tags.Contains("work"));
        Assert.Contains(reloaded.Views, v => v.Name == "Deploys" && v.Type == "deploy");
        Assert.Equal("abc", reloaded.Secrets["TOKEN"]);
    }

    [Fact]
    public void SaveViews_DerivesIds_DedupesTags_DropsBlankNames()
    {
        var configPath = Path.Combine(_tempDir, "actionview.json");
        File.WriteAllText(configPath, "{}");
        var config = new AppConfig { SourcePath = configPath };
        var store = CreateStore(config);

        var saved = store.SaveViews([
            new SavedView { Name = "  My Work  ", Tags = ["a", "A", " a ", "b"] },
            new SavedView { Name = "   " }, // dropped (blank name)
        ]);

        Assert.Single(saved);
        Assert.Equal("my-work", saved[0].Id);
        Assert.Equal("My Work", saved[0].Name);
        Assert.Equal(["a", "b"], saved[0].Tags);
    }

    [Fact]
    public void SaveViews_MakesDuplicateIdsUnique()
    {
        var configPath = Path.Combine(_tempDir, "actionview.json");
        File.WriteAllText(configPath, "{}");
        var config = new AppConfig { SourcePath = configPath };
        var store = CreateStore(config);

        var saved = store.SaveViews([
            new SavedView { Name = "Work" },
            new SavedView { Name = "Work" },
        ]);

        Assert.Equal(2, saved.Count);
        Assert.Equal("work", saved[0].Id);
        Assert.Equal("work-2", saved[1].Id);
    }

    [Fact]
    public void SaveViews_CreatesConfigFile_WhenMissing()
    {
        var configPath = Path.Combine(_tempDir, "new-config.json");
        Assert.False(File.Exists(configPath));

        var config = new AppConfig { SourcePath = configPath };
        var store = CreateStore(config);

        store.SaveViews([new SavedView { Name = "Personal", Tags = ["personal"] }]);

        Assert.True(File.Exists(configPath));
        var reloaded = ConfigLoader.Load(explicitPath: configPath);
        Assert.Single(reloaded.Views);
        Assert.Equal("Personal", reloaded.Views[0].Name);
    }
}
