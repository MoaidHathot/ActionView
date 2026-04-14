using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Scans an external directory for template JSON files and syncs them into the
/// <see cref="TemplateRegistry"/>. A manifest file tracks which templates were
/// auto-discovered so they can be removed when they disappear from the external
/// directory, without affecting templates registered by other means.
/// </summary>
public sealed class TemplateScanner
{
    private readonly TemplateRegistry _registry;
    private readonly string _manifestPath;
    private readonly ILogger<TemplateScanner> _logger;

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public TemplateScanner(TemplateRegistry registry, string dataDirectory, ILogger<TemplateScanner> logger)
    {
        _registry = registry;
        _logger = logger;
        _manifestPath = Path.Combine(dataDirectory, "templates", ".auto-discovered.json");
    }

    /// <summary>
    /// Scan the external directory and sync templates into the registry.
    /// New templates are registered, changed templates are updated, and templates
    /// that were previously auto-discovered but no longer exist are removed.
    /// Templates registered by other means are never touched.
    /// </summary>
    /// <param name="externalDirectory">Absolute path to the external templates directory.</param>
    /// <param name="recursive">Whether to scan subdirectories.</param>
    public void Scan(string externalDirectory, bool recursive)
    {
        if (!Directory.Exists(externalDirectory))
        {
            _logger.LogWarning(
                "External templates directory does not exist, skipping scan: {Path}",
                externalDirectory);
            return;
        }

        var previouslyManaged = LoadManifest();
        var currentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(externalDirectory, "*.json", searchOption);

        var registered = 0;
        var skipped = 0;

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var template = JsonSerializer.Deserialize<EntryTemplate>(json, JsonReadOptions);

                if (template is null || string.IsNullOrWhiteSpace(template.Type))
                {
                    _logger.LogWarning(
                        "Skipping external template file with missing or empty type: {File}", file);
                    skipped++;
                    continue;
                }

                currentTypes.Add(template.Type);
                _registry.Register(template);
                registered++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load external template from {File}", file);
                skipped++;
            }
        }

        // Remove templates that were previously auto-discovered but are no longer
        // present in the external directory.
        var removed = 0;
        foreach (var type in previouslyManaged)
        {
            if (!currentTypes.Contains(type))
            {
                if (_registry.Remove(type))
                {
                    _logger.LogInformation(
                        "Removed auto-discovered template no longer in external directory: {Type}", type);
                    removed++;
                }
            }
        }

        SaveManifest(currentTypes);

        _logger.LogInformation(
            "External template scan complete: {Registered} registered/updated, {Removed} removed, {Skipped} skipped from {Path}",
            registered, removed, skipped, externalDirectory);
    }

    private HashSet<string> LoadManifest()
    {
        if (!File.Exists(_manifestPath))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(_manifestPath);
            var types = JsonSerializer.Deserialize<List<string>>(json, JsonReadOptions);
            return new HashSet<string>(types ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read auto-discovery manifest, starting fresh");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveManifest(HashSet<string> types)
    {
        try
        {
            var sorted = types.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            var json = JsonSerializer.Serialize(sorted, JsonWriteOptions);
            var directory = Path.GetDirectoryName(_manifestPath);
            if (directory is not null)
                Directory.CreateDirectory(directory);
            File.WriteAllText(_manifestPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write auto-discovery manifest");
        }
    }
}
