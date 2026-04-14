using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class TemplateScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dataDir;
    private readonly string _externalDir;
    private readonly string _templatesDir;
    private readonly string _manifestPath;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public TemplateScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_scanner_test_{Guid.NewGuid():N}");
        _dataDir = Path.Combine(_tempDir, "data");
        _externalDir = Path.Combine(_tempDir, "external-templates");
        _templatesDir = Path.Combine(_dataDir, "templates");
        _manifestPath = Path.Combine(_templatesDir, ".auto-discovered.json");

        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_externalDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private TemplateRegistry CreateRegistry() =>
        new(_dataDir, NullLogger<TemplateRegistry>.Instance);

    private TemplateScanner CreateScanner(TemplateRegistry registry) =>
        new(registry, _dataDir, NullLogger<TemplateScanner>.Instance);

    private void WriteExternalTemplate(string type, string? description = null, string? subdir = null)
    {
        var template = new EntryTemplate
        {
            Type = type,
            Description = description ?? $"Template for {type}"
        };
        var json = JsonSerializer.Serialize(template, WriteOptions);

        string dir;
        if (subdir is not null)
        {
            dir = Path.Combine(_externalDir, subdir);
            Directory.CreateDirectory(dir);
        }
        else
        {
            dir = _externalDir;
        }

        File.WriteAllText(Path.Combine(dir, $"{type}.json"), json);
    }

    private void WriteManifest(params string[] types)
    {
        Directory.CreateDirectory(_templatesDir);
        var json = JsonSerializer.Serialize(types.ToList(), WriteOptions);
        File.WriteAllText(_manifestPath, json);
    }

    private List<string>? ReadManifest()
    {
        if (!File.Exists(_manifestPath)) return null;
        var json = File.ReadAllText(_manifestPath);
        return JsonSerializer.Deserialize<List<string>>(json);
    }

    [Fact]
    public void Scan_RegistersNewTemplates()
    {
        WriteExternalTemplate("code-review");
        WriteExternalTemplate("deploy");

        var registry = CreateRegistry();
        var scanner = CreateScanner(registry);

        scanner.Scan(_externalDir, recursive: false);

        Assert.NotNull(registry.GetTemplate("code-review"));
        Assert.NotNull(registry.GetTemplate("deploy"));
        Assert.Equal(2, registry.GetAll().Count);
    }

    [Fact]
    public void Scan_UpdatesExistingTemplates()
    {
        // Register a template first with original description
        var registry = CreateRegistry();
        registry.Register(new EntryTemplate
        {
            Type = "code-review",
            Description = "Original description"
        });

        // Place an updated version in the external directory
        WriteExternalTemplate("code-review", "Updated description");

        var scanner = CreateScanner(registry);
        scanner.Scan(_externalDir, recursive: false);

        var template = registry.GetTemplate("code-review");
        Assert.NotNull(template);
        Assert.Equal("Updated description", template.Description);
    }

    [Fact]
    public void Scan_RemovesStaleManagedTemplates()
    {
        // First scan: register two templates
        WriteExternalTemplate("code-review");
        WriteExternalTemplate("deploy");

        var registry = CreateRegistry();
        var scanner = CreateScanner(registry);
        scanner.Scan(_externalDir, recursive: false);

        Assert.Equal(2, registry.GetAll().Count);

        // Remove "deploy" from external dir
        File.Delete(Path.Combine(_externalDir, "deploy.json"));

        // Second scan: "deploy" should be removed
        scanner.Scan(_externalDir, recursive: false);

        Assert.NotNull(registry.GetTemplate("code-review"));
        Assert.Null(registry.GetTemplate("deploy"));
        Assert.Single(registry.GetAll());
    }

    [Fact]
    public void Scan_DoesNotRemoveNonManagedTemplates()
    {
        // Manually register a template (not through the scanner)
        var registry = CreateRegistry();
        registry.Register(new EntryTemplate
        {
            Type = "manual-template",
            Description = "Registered via CLI"
        });

        // Scan with an empty external directory -- should not touch the manual one
        var scanner = CreateScanner(registry);
        scanner.Scan(_externalDir, recursive: false);

        Assert.NotNull(registry.GetTemplate("manual-template"));
    }

    [Fact]
    public void Scan_WritesManifest()
    {
        WriteExternalTemplate("code-review");
        WriteExternalTemplate("deploy");

        var registry = CreateRegistry();
        var scanner = CreateScanner(registry);
        scanner.Scan(_externalDir, recursive: false);

        var manifest = ReadManifest();
        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Count);
        Assert.Contains("code-review", manifest, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("deploy", manifest, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scan_Recursive_FindsSubdirectoryTemplates()
    {
        WriteExternalTemplate("top-level");
        WriteExternalTemplate("nested", subdir: "subdir");
        WriteExternalTemplate("deep-nested", subdir: Path.Combine("subdir", "deep"));

        var registry = CreateRegistry();
        var scanner = CreateScanner(registry);
        scanner.Scan(_externalDir, recursive: true);

        Assert.NotNull(registry.GetTemplate("top-level"));
        Assert.NotNull(registry.GetTemplate("nested"));
        Assert.NotNull(registry.GetTemplate("deep-nested"));
        Assert.Equal(3, registry.GetAll().Count);
    }

    [Fact]
    public void Scan_NonRecursive_IgnoresSubdirectories()
    {
        WriteExternalTemplate("top-level");
        WriteExternalTemplate("nested", subdir: "subdir");

        var registry = CreateRegistry();
        var scanner = CreateScanner(registry);
        scanner.Scan(_externalDir, recursive: false);

        Assert.NotNull(registry.GetTemplate("top-level"));
        Assert.Null(registry.GetTemplate("nested"));
        Assert.Single(registry.GetAll());
    }

    [Fact]
    public void Scan_SkipsMalformedFiles()
    {
        // Write a valid template
        WriteExternalTemplate("valid-template");

        // Write a malformed JSON file
        File.WriteAllText(Path.Combine(_externalDir, "broken.json"), "{ not valid json!!!");

        // Write a JSON file with no type field
        File.WriteAllText(Path.Combine(_externalDir, "no-type.json"), """{"description": "oops"}""");

        var registry = CreateRegistry();
        var scanner = CreateScanner(registry);
        scanner.Scan(_externalDir, recursive: false);

        // Only the valid template should be registered
        Assert.Single(registry.GetAll());
        Assert.NotNull(registry.GetTemplate("valid-template"));
    }

    [Fact]
    public void Scan_MissingDirectory_LogsWarningAndLeavesManifestUntouched()
    {
        // Pre-existing manifest from a prior scan
        WriteManifest("old-template");

        // Register the template the manifest refers to
        var registry = CreateRegistry();
        registry.Register(new EntryTemplate { Type = "old-template" });

        var scanner = CreateScanner(registry);
        var nonExistentDir = Path.Combine(_tempDir, "does-not-exist");

        scanner.Scan(nonExistentDir, recursive: false);

        // Manifest should be unchanged
        var manifest = ReadManifest();
        Assert.NotNull(manifest);
        Assert.Single(manifest);
        Assert.Contains("old-template", manifest);

        // Template should still exist in registry
        Assert.NotNull(registry.GetTemplate("old-template"));
    }

    [Fact]
    public void Scan_EmptyDirectory_RemovesAllManagedTemplates()
    {
        // First scan: register templates
        WriteExternalTemplate("code-review");
        WriteExternalTemplate("deploy");

        var registry = CreateRegistry();
        var scanner = CreateScanner(registry);
        scanner.Scan(_externalDir, recursive: false);
        Assert.Equal(2, registry.GetAll().Count);

        // Also manually register one (not managed by scanner)
        registry.Register(new EntryTemplate
        {
            Type = "manual-template",
            Description = "Should survive"
        });
        Assert.Equal(3, registry.GetAll().Count);

        // Remove all files from external dir
        foreach (var file in Directory.GetFiles(_externalDir))
            File.Delete(file);

        // Second scan: all managed templates removed, manual one stays
        scanner.Scan(_externalDir, recursive: false);

        Assert.Single(registry.GetAll());
        Assert.NotNull(registry.GetTemplate("manual-template"));
        Assert.Null(registry.GetTemplate("code-review"));
        Assert.Null(registry.GetTemplate("deploy"));

        // Manifest should be empty
        var manifest = ReadManifest();
        Assert.NotNull(manifest);
        Assert.Empty(manifest);
    }
}
