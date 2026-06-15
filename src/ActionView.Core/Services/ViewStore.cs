using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Reads and persists saved <see cref="SavedView"/> presets.
///
/// Views live in actionview.json under the <c>views</c> key. This store keeps
/// the in-memory <see cref="AppConfig.Views"/> authoritative for the running
/// process and writes changes back to the same config file the config was
/// loaded from, preserving all other keys (the file is read-modify-written as a
/// JSON object, then swapped in atomically).
/// </summary>
public sealed partial class ViewStore
{
    private readonly AppConfig _config;
    private readonly ILogger<ViewStore> _logger;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ViewStore(AppConfig config, ILogger<ViewStore> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>Returns the currently configured views (excludes the synthetic "All" view).</summary>
    public IReadOnlyList<SavedView> GetViews()
    {
        lock (_lock)
        {
            return _config.Views.ToList();
        }
    }

    /// <summary>
    /// Replaces the full set of saved views, then persists them back to the
    /// config file. Input is normalized (trimmed names, derived/unique ids,
    /// de-duplicated tags). Returns the normalized, stored list.
    /// </summary>
    public IReadOnlyList<SavedView> SaveViews(IEnumerable<SavedView> views)
    {
        var normalized = Normalize(views);
        lock (_lock)
        {
            _config.Views = normalized;
            Persist(normalized);
            return normalized.ToList();
        }
    }

    private void Persist(List<SavedView> views)
    {
        var path = _config.SourcePath
            ?? Path.Combine(Directory.GetCurrentDirectory(), "actionview.json");

        JsonObject root;
        if (File.Exists(path))
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(path), documentOptions: ParseOptions);
                root = node as JsonObject ?? new JsonObject();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse existing config at {Path}; rewriting with views only.", path);
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        root["views"] = JsonSerializer.SerializeToNode(views, SerializeOptions);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Atomic write: serialize to a temp file alongside the target, then swap.
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, root.ToJsonString(SerializeOptions));
        File.Move(tempPath, path, overwrite: true);

        _config.SourcePath = path;
        _logger.LogInformation("Persisted {Count} view(s) to {Path}.", views.Count, path);
    }

    private static List<SavedView> Normalize(IEnumerable<SavedView> views)
    {
        var result = new List<SavedView>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in views)
        {
            if (view is null) continue;

            var name = view.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var id = string.IsNullOrWhiteSpace(view.Id) ? Slugify(name) : Slugify(view.Id);
            if (string.IsNullOrWhiteSpace(id)) id = "view";

            // Guarantee uniqueness so client keys/active-state stay stable.
            var baseId = id;
            var suffix = 2;
            while (!seenIds.Add(id))
                id = $"{baseId}-{suffix++}";

            var tags = (view.Tags ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.Add(new SavedView
            {
                Id = id,
                Name = name,
                Icon = string.IsNullOrWhiteSpace(view.Icon) ? null : view.Icon.Trim(),
                Type = string.IsNullOrWhiteSpace(view.Type) ? null : view.Type.Trim(),
                Tags = tags,
                TagMatch = view.TagMatch,
            });
        }

        return result;
    }

    private static string Slugify(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var slug = SlugCharsRegex().Replace(lowered, "-").Trim('-');
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugCharsRegex();
}
