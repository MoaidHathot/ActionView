using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Loads and manages entry type templates from the templates/ directory.
/// Templates define the canonical shape for each entry type, enabling
/// normalization of AI-generated entries for consistency.
/// </summary>
public sealed class TemplateRegistry : IDisposable
{
    private readonly string _templatesDirectory;
    private readonly ILogger<TemplateRegistry> _logger;
    private readonly Dictionary<string, EntryTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public TemplateRegistry(string dataDirectory, ILogger<TemplateRegistry> logger)
    {
        _templatesDirectory = Path.Combine(dataDirectory, "templates");
        _logger = logger;
        Directory.CreateDirectory(_templatesDirectory);
        LoadAll();
    }

    /// <summary>Start watching the templates directory for changes.</summary>
    public void StartWatching()
    {
        _watcher = new FileSystemWatcher(_templatesDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, _) => ScheduleReload();
        _watcher.Changed += (_, _) => ScheduleReload();
        _watcher.Deleted += (_, _) => ScheduleReload();
        _watcher.Renamed += (_, _) => ScheduleReload();
        _watcher.Error += (_, e) =>
            _logger.LogError(e.GetException(), "Template directory watcher error");

        _logger.LogInformation("Watching templates directory: {Path}", _templatesDirectory);
    }

    /// <summary>
    /// Debounce watcher events: wait 300ms after the last event before reloading,
    /// so a batch of writes (e.g. from TemplateScanner) triggers only one reload.
    /// </summary>
    private void ScheduleReload()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ => LoadAll(), null, 300, Timeout.Infinite);
    }

    /// <summary>Get a template by entry type. Returns null if no template is registered.</summary>
    public EntryTemplate? GetTemplate(string type)
    {
        return _templates.TryGetValue(type, out var template) ? template : null;
    }

    /// <summary>Get all registered templates.</summary>
    public IReadOnlyList<EntryTemplate> GetAll()
    {
        return _templates.Values.ToList();
    }

    /// <summary>Get all registered template type names.</summary>
    public IReadOnlyList<string> GetRegisteredTypes()
    {
        return _templates.Keys.ToList();
    }

    /// <summary>
    /// Register a new template from a JSON string. Saves to disk and loads into registry.
    /// </summary>
    public EntryTemplate Register(string json)
    {
        var template = JsonSerializer.Deserialize<EntryTemplate>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize template JSON");

        if (string.IsNullOrWhiteSpace(template.Type))
            throw new InvalidOperationException("Template must have a non-empty 'type' field");

        var filePath = Path.Combine(_templatesDirectory, $"{template.Type}.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(template, WriteOptions));

        _templates[template.Type] = template;
        _logger.LogInformation("Registered template for type: {Type}", template.Type);

        return template;
    }

    /// <summary>
    /// Register a template from an EntryTemplate object. Saves to disk and loads into registry.
    /// </summary>
    public void Register(EntryTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Type))
            throw new InvalidOperationException("Template must have a non-empty 'type' field");

        var filePath = Path.Combine(_templatesDirectory, $"{template.Type}.json");
        var json = JsonSerializer.Serialize(template, WriteOptions);
        File.WriteAllText(filePath, json);

        _templates[template.Type] = template;
        _logger.LogInformation("Registered template for type: {Type}", template.Type);
    }

    /// <summary>Remove a template by type name.</summary>
    public bool Remove(string type)
    {
        if (!_templates.Remove(type))
            return false;

        var filePath = Path.Combine(_templatesDirectory, $"{type}.json");
        if (File.Exists(filePath))
            File.Delete(filePath);

        _logger.LogInformation("Removed template for type: {Type}", type);
        return true;
    }

    /// <summary>Serialize a template to JSON string.</summary>
    public static string ToJson(EntryTemplate template)
    {
        return JsonSerializer.Serialize(template, WriteOptions);
    }

    private void LoadAll()
    {
        _templates.Clear();

        if (!Directory.Exists(_templatesDirectory)) return;

        foreach (var file in Directory.EnumerateFiles(_templatesDirectory, "*.json"))
        {
            // Skip dotfiles (e.g. .auto-discovered.json manifest)
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith('.'))
                continue;

            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var template = JsonSerializer.Deserialize<EntryTemplate>(json, JsonOptions);
                if (template is not null && !string.IsNullOrWhiteSpace(template.Type))
                {
                    _templates[template.Type] = template;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load template from {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} entry templates", _templates.Count);
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _watcher?.Dispose();
        _watcher = null;
    }
}
