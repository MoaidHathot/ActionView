using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// JSON options matching EntryStore conventions
var jsonReadOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};

var jsonWriteOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};

// Root command
var rootCommand = new RootCommand("ActionView CLI - manage the ActionView review queue");

var configOption = new Option<string?>(
    "--config",
    "Path to actionview.json configuration file");
rootCommand.AddGlobalOption(configOption);

// Helper to resolve an entry file by full or partial ID
static string? ResolveEntryFile(string activeDir, string id)
{
    var filePath = Path.Combine(activeDir, $"{id}.json");
    if (File.Exists(filePath)) return filePath;

    // Try partial match
    if (!Directory.Exists(activeDir)) return null;

    var candidates = Directory.EnumerateFiles(activeDir, "*.json")
        .Where(f => Path.GetFileNameWithoutExtension(f)!
            .StartsWith(id, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (candidates.Count == 1) return candidates[0];

    if (candidates.Count > 1)
    {
        Console.Error.WriteLine($"Ambiguous ID '{id}'. Matches:");
        foreach (var c in candidates)
            Console.Error.WriteLine($"  {Path.GetFileNameWithoutExtension(c)}");
    }
    else
    {
        Console.Error.WriteLine($"Entry not found: {id}");
    }

    return null;
}

// Helper to read JSON from stdin when piped
static string? ReadStdin()
{
    if (!Console.IsInputRedirected)
        return null;

    return Console.In.ReadToEnd();
}

// ================================================================
// add command
// ================================================================
var addCommand = new Command("add", "Add an entry to the inbox");
var fileOption = new Option<FileInfo?>(
    "--file",
    "Path to the entry JSON file (use '-' to read from stdin)");
fileOption.AddAlias("-f");
addCommand.AddOption(fileOption);

var jsonOption = new Option<string?>(
    "--json",
    "Inline entry JSON string");
jsonOption.AddAlias("-j");
addCommand.AddOption(jsonOption);

addCommand.SetHandler((FileInfo? file, string? inlineJson, string? configPath) =>
{
    string json;
    string sourceName;

    // --file - means read from stdin explicitly
    if (file is not null && file.Name == "-")
    {
        var stdinContent = ReadStdin();
        if (string.IsNullOrWhiteSpace(stdinContent))
        {
            Console.Error.WriteLine("Error: --file - specified but no data received on stdin.");
            return;
        }
        json = stdinContent;
        sourceName = "stdin.json";
    }
    else if (file is not null && inlineJson is not null)
    {
        Console.Error.WriteLine("Error: Provide either --file, --json, or pipe via stdin, not multiple.");
        return;
    }
    else if (file is not null)
    {
        if (!file.Exists)
        {
            Console.Error.WriteLine($"File not found: {file.FullName}");
            return;
        }
        json = File.ReadAllText(file.FullName);
        sourceName = Path.GetFileName(file.Name);
    }
    else if (inlineJson is not null)
    {
        json = inlineJson;
        sourceName = "inline.json";
    }
    else
    {
        // Auto-detect: try reading from stdin if piped
        var stdinContent = ReadStdin();
        if (!string.IsNullOrWhiteSpace(stdinContent))
        {
            json = stdinContent;
            sourceName = "stdin.json";
        }
        else
        {
            Console.Error.WriteLine("Error: Provide input via --file, --json, or pipe JSON through stdin.");
            Console.Error.WriteLine("  Examples:");
            Console.Error.WriteLine("    actionview add --file entry.json");
            Console.Error.WriteLine("    actionview add --json '{...}'");
            Console.Error.WriteLine("    cat entry.json | actionview add");
            Console.Error.WriteLine("    actionview add --file -  < entry.json");
            return;
        }
    }

    var config = ConfigLoader.Load(configPath);
    var inboxDir = Path.Combine(config.DataDirectory, "inbox");

    try
    {
        var entry = JsonSerializer.Deserialize<Entry>(json, jsonReadOptions);

        if (entry is null)
        {
            Console.Error.WriteLine("Error: Input does not contain a valid entry JSON.");
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.Type) ||
            string.IsNullOrWhiteSpace(entry.Source) ||
            string.IsNullOrWhiteSpace(entry.Title))
        {
            Console.Error.WriteLine("Error: Entry is missing required fields (type, source, title).");
            return;
        }

        var destName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{sourceName}";
        var destPath = Path.Combine(inboxDir, destName);
        File.WriteAllText(destPath, json);

        Console.WriteLine($"Added to inbox: {destName}");
        Console.WriteLine($"  Title:    {entry.Title}");
        Console.WriteLine($"  Type:     {entry.Type}");
        Console.WriteLine($"  Source:   {entry.Source}");
        Console.WriteLine($"  Severity: {entry.Severity}");
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"Error: Invalid JSON - {ex.Message}");
    }
}, fileOption, jsonOption, configOption);

rootCommand.AddCommand(addCommand);

// ================================================================
// list command
// ================================================================
var listCommand = new Command("list", "List active entries");
var typeFilter = new Option<string?>("--type", "Filter by entry type");
var severityFilter = new Option<string?>("--severity", "Filter by severity (low, medium, high, critical)");
var sourceFilter = new Option<string?>("--source", "Filter by source");
var searchFilter = new Option<string?>("--search", "Search in title, subtitle, source, tags");
listCommand.AddOption(typeFilter);
listCommand.AddOption(severityFilter);
listCommand.AddOption(sourceFilter);
listCommand.AddOption(searchFilter);

listCommand.SetHandler((string? configPath, string? type, string? severity, string? source, string? search) =>
{
    var config = ConfigLoader.Load(configPath);
    var activeDir = Path.Combine(config.DataDirectory, "active");

    if (!Directory.Exists(activeDir))
    {
        Console.WriteLine("No active entries.");
        return;
    }

    var entries = new List<Entry>();
    foreach (var file in Directory.EnumerateFiles(activeDir, "*.json"))
    {
        try
        {
            var json = File.ReadAllText(file);
            var entry = JsonSerializer.Deserialize<Entry>(json, jsonReadOptions);
            if (entry is not null)
                entries.Add(entry);
        }
        catch
        {
            // Skip malformed files
        }
    }

    // Apply filters
    if (!string.IsNullOrWhiteSpace(type))
        entries = entries.Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

    if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Severity>(severity, true, out var sev))
        entries = entries.Where(e => e.Severity == sev).ToList();

    if (!string.IsNullOrWhiteSpace(source))
        entries = entries.Where(e => e.Source.Equals(source, StringComparison.OrdinalIgnoreCase)).ToList();

    if (!string.IsNullOrWhiteSpace(search))
    {
        entries = entries.Where(e =>
            e.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (e.Subtitle?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
            e.Source.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            e.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    // Sort: pinned first, then priority, then severity desc, then created desc
    entries = entries
        .OrderByDescending(e => e.Pinned)
        .ThenByDescending(e => e.Priority)
        .ThenByDescending(e => e.Severity)
        .ThenByDescending(e => e.CreatedAt)
        .ToList();

    if (entries.Count == 0)
    {
        Console.WriteLine("No active entries.");
        return;
    }

    Console.WriteLine($"{"ID",-34} {"Sev",-10} {"Status",-8} {"Type",-16} {"Title"}");
    Console.WriteLine(new string('-', 100));

    foreach (var entry in entries)
    {
        var id = entry.Id.Length > 32 ? entry.Id[..32] : entry.Id;
        var title = entry.Title.Length > 40 ? entry.Title[..37] + "..." : entry.Title;
        var pin = entry.Pinned ? "*" : " ";
        Console.WriteLine($"{pin}{id,-33} {entry.Severity,-10} {entry.Status,-8} {entry.Type,-16} {title}");
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {entries.Count} active entries");
}, configOption, typeFilter, severityFilter, sourceFilter, searchFilter);

rootCommand.AddCommand(listCommand);

// ================================================================
// dismiss command
// ================================================================
var dismissCommand = new Command("dismiss", "Dismiss an active entry (moves to archive)");
var idArgument = new Argument<string>("id", "The entry ID to dismiss");
dismissCommand.AddArgument(idArgument);

dismissCommand.SetHandler((string id, string? configPath) =>
{
    var config = ConfigLoader.Load(configPath);
    var activeDir = Path.Combine(config.DataDirectory, "active");
    var archiveDir = Path.Combine(config.DataDirectory, "archive");

    var filePath = ResolveEntryFile(activeDir, id);
    if (filePath is null) return;

    try
    {
        var json = File.ReadAllText(filePath);
        var entry = JsonSerializer.Deserialize<Entry>(json, jsonReadOptions);

        if (entry is null)
        {
            Console.Error.WriteLine("Error: Could not deserialize entry.");
            return;
        }

        entry.Status = EntryStatus.Archived;
        entry.Outcome = new EntryOutcome
        {
            Action = "Dismissed",
            Success = true,
            ResultMessage = "Dismissed via CLI"
        };

        var archivePath = Path.Combine(archiveDir, $"{entry.Id}.json");
        var updatedJson = JsonSerializer.Serialize(entry, jsonWriteOptions);
        File.WriteAllText(archivePath, updatedJson);

        File.Delete(filePath);

        Console.WriteLine($"Dismissed: {entry.Title}");
        Console.WriteLine($"  Archived to: {archivePath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error dismissing entry: {ex.Message}");
    }
}, idArgument, configOption);

rootCommand.AddCommand(dismissCommand);

// ================================================================
// delete command
// ================================================================
var deleteCommand = new Command("delete", "Permanently delete an active entry");
var deleteIdArgument = new Argument<string>("id", "The entry ID to delete");
deleteCommand.AddArgument(deleteIdArgument);

var forceOption = new Option<bool>("--force", "Skip confirmation prompt");
forceOption.AddAlias("-f");
deleteCommand.AddOption(forceOption);

deleteCommand.SetHandler((string id, string? configPath, bool force) =>
{
    var config = ConfigLoader.Load(configPath);
    var activeDir = Path.Combine(config.DataDirectory, "active");

    var filePath = ResolveEntryFile(activeDir, id);
    if (filePath is null) return;

    if (!force)
    {
        var json = File.ReadAllText(filePath);
        var entry = JsonSerializer.Deserialize<Entry>(json, jsonReadOptions);
        Console.Write($"Delete \"{entry?.Title}\" permanently? [y/N] ");
        var response = Console.ReadLine();
        if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Cancelled.");
            return;
        }
    }

    File.Delete(filePath);
    Console.WriteLine($"Deleted entry: {id}");
}, deleteIdArgument, configOption, forceOption);

rootCommand.AddCommand(deleteCommand);

// ================================================================
// pin command
// ================================================================
var pinCommand = new Command("pin", "Toggle pin on an active entry");
var pinIdArgument = new Argument<string>("id", "The entry ID to pin/unpin");
pinCommand.AddArgument(pinIdArgument);

pinCommand.SetHandler((string id, string? configPath) =>
{
    var config = ConfigLoader.Load(configPath);
    var activeDir = Path.Combine(config.DataDirectory, "active");

    var filePath = ResolveEntryFile(activeDir, id);
    if (filePath is null) return;

    try
    {
        var json = File.ReadAllText(filePath);
        var entry = JsonSerializer.Deserialize<Entry>(json, jsonReadOptions);
        if (entry is null)
        {
            Console.Error.WriteLine("Error: Could not deserialize entry.");
            return;
        }

        entry.Pinned = !entry.Pinned;

        var updatedJson = JsonSerializer.Serialize(entry, jsonWriteOptions);
        File.WriteAllText(filePath, updatedJson);

        Console.WriteLine($"{(entry.Pinned ? "Pinned" : "Unpinned")}: {entry.Title}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error toggling pin: {ex.Message}");
    }
}, pinIdArgument, configOption);

rootCommand.AddCommand(pinCommand);

// ================================================================
// schema command
// ================================================================
var schemaCommand = new Command("schema", "Print the entry JSON schema (for LLM consumption or validation)");

schemaCommand.SetHandler(() =>
{
    using var stream = typeof(Program).Assembly.GetManifestResourceStream("entry.v1.schema.json");
    if (stream is null)
    {
        Console.Error.WriteLine("Error: Embedded schema resource not found.");
        return;
    }

    using var reader = new StreamReader(stream);
    Console.Write(reader.ReadToEnd());
});

rootCommand.AddCommand(schemaCommand);

// ================================================================
// template command group
// ================================================================
var templateCommand = new Command("template", "Manage entry type templates for normalization");

// template list
var templateListCommand = new Command("list", "List all registered templates");
templateListCommand.SetHandler((string? configPath) =>
{
    var config = ConfigLoader.Load(configPath);
    var registry = new TemplateRegistry(config.DataDirectory, NullLogger<TemplateRegistry>.Instance);

    var templates = registry.GetAll();
    if (templates.Count == 0)
    {
        Console.WriteLine("No templates registered.");
        Console.WriteLine($"  Templates directory: {Path.Combine(config.DataDirectory, "templates")}");
        return;
    }

    Console.WriteLine($"{"Type",-20} {"Description"}");
    Console.WriteLine(new string('-', 60));

    foreach (var template in templates)
    {
        Console.WriteLine($"{template.Type,-20} {template.Description ?? "(no description)"}");
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {templates.Count} templates");
}, configOption);
templateCommand.AddCommand(templateListCommand);

// template show <type>
var templateShowCommand = new Command("show", "Show a template's full definition");
var templateTypeArg = new Argument<string>("type", "The entry type name");
templateShowCommand.AddArgument(templateTypeArg);
templateShowCommand.SetHandler((string type, string? configPath) =>
{
    var config = ConfigLoader.Load(configPath);
    var registry = new TemplateRegistry(config.DataDirectory, NullLogger<TemplateRegistry>.Instance);

    var template = registry.GetTemplate(type);
    if (template is null)
    {
        Console.Error.WriteLine($"No template found for type: {type}");
        return;
    }

    Console.Write(TemplateRegistry.ToJson(template));
}, templateTypeArg, configOption);
templateCommand.AddCommand(templateShowCommand);

// template register --file | --json
var templateRegisterCommand = new Command("register", "Register a new entry type template");
var templateFileOption = new Option<FileInfo?>("--file", "Path to the template JSON file (use '-' to read from stdin)");
templateFileOption.AddAlias("-f");
templateRegisterCommand.AddOption(templateFileOption);

var templateJsonOption = new Option<string?>("--json", "Inline template JSON string");
templateJsonOption.AddAlias("-j");
templateRegisterCommand.AddOption(templateJsonOption);

templateRegisterCommand.SetHandler((FileInfo? file, string? inlineJson, string? configPath) =>
{
    string json;

    // --file - means read from stdin explicitly
    if (file is not null && file.Name == "-")
    {
        var stdinContent = ReadStdin();
        if (string.IsNullOrWhiteSpace(stdinContent))
        {
            Console.Error.WriteLine("Error: --file - specified but no data received on stdin.");
            return;
        }
        json = stdinContent;
    }
    else if (file is not null)
    {
        if (!file.Exists)
        {
            Console.Error.WriteLine($"File not found: {file.FullName}");
            return;
        }
        json = File.ReadAllText(file.FullName);
    }
    else if (inlineJson is not null)
    {
        json = inlineJson;
    }
    else
    {
        // Auto-detect: try reading from stdin if piped
        var stdinContent = ReadStdin();
        if (!string.IsNullOrWhiteSpace(stdinContent))
        {
            json = stdinContent;
        }
        else
        {
            Console.Error.WriteLine("Error: Provide input via --file, --json, or pipe JSON through stdin.");
            Console.Error.WriteLine("  Examples:");
            Console.Error.WriteLine("    actionview template register --file template.json");
            Console.Error.WriteLine("    actionview template register --json '{...}'");
            Console.Error.WriteLine("    cat template.json | actionview template register");
            Console.Error.WriteLine("    actionview template register --file -  < template.json");
            return;
        }
    }

    var config = ConfigLoader.Load(configPath);
    var registry = new TemplateRegistry(config.DataDirectory, NullLogger<TemplateRegistry>.Instance);

    try
    {
        var template = registry.Register(json);
        Console.WriteLine($"Registered template for type: {template.Type}");
        if (template.Description is not null)
            Console.WriteLine($"  Description: {template.Description}");
        Console.WriteLine($"  Content template blocks: {template.ContentTemplate.Count}");
        Console.WriteLine($"  Expected actions: {template.ExpectedActions.Count}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error registering template: {ex.Message}");
    }
}, templateFileOption, templateJsonOption, configOption);
templateCommand.AddCommand(templateRegisterCommand);

// template remove <type>
var templateRemoveCommand = new Command("remove", "Remove a registered template");
var templateRemoveTypeArg = new Argument<string>("type", "The entry type to remove");
templateRemoveCommand.AddArgument(templateRemoveTypeArg);
templateRemoveCommand.SetHandler((string type, string? configPath) =>
{
    var config = ConfigLoader.Load(configPath);
    var registry = new TemplateRegistry(config.DataDirectory, NullLogger<TemplateRegistry>.Instance);

    if (registry.Remove(type))
    {
        Console.WriteLine($"Removed template for type: {type}");
    }
    else
    {
        Console.Error.WriteLine($"No template found for type: {type}");
    }
}, templateRemoveTypeArg, configOption);
templateCommand.AddCommand(templateRemoveCommand);

rootCommand.AddCommand(templateCommand);

// ================================================================
// stats command
// ================================================================
var statsCommand = new Command("stats", "Show dashboard statistics");

statsCommand.SetHandler((string? configPath) =>
{
    var config = ConfigLoader.Load(configPath);
    var activeDir = Path.Combine(config.DataDirectory, "active");
    var archiveDir = Path.Combine(config.DataDirectory, "archive");
    var inboxDir = Path.Combine(config.DataDirectory, "inbox");
    var templatesDir = Path.Combine(config.DataDirectory, "templates");

    var activeCount = Directory.Exists(activeDir) ? Directory.EnumerateFiles(activeDir, "*.json").Count() : 0;
    var archiveCount = Directory.Exists(archiveDir) ? Directory.EnumerateFiles(archiveDir, "*.json").Count() : 0;
    var inboxCount = Directory.Exists(inboxDir) ? Directory.EnumerateFiles(inboxDir, "*.json").Count() : 0;
    var templateCount = Directory.Exists(templatesDir) ? Directory.EnumerateFiles(templatesDir, "*.json").Count() : 0;

    Console.WriteLine("ActionView Statistics");
    Console.WriteLine(new string('-', 30));
    Console.WriteLine($"  Inbox:     {inboxCount}");
    Console.WriteLine($"  Active:    {activeCount}");
    Console.WriteLine($"  Archived:  {archiveCount}");
    Console.WriteLine($"  Templates: {templateCount}");
    Console.WriteLine();
    Console.WriteLine($"  Data dir: {config.DataDirectory}");

    if (activeCount > 0)
    {
        var entries = new List<Entry>();
        foreach (var file in Directory.EnumerateFiles(activeDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<Entry>(json, jsonReadOptions);
                if (entry is not null) entries.Add(entry);
            }
            catch { }
        }

        var byType = entries.GroupBy(e => e.Type).OrderByDescending(g => g.Count());
        var bySeverity = entries.GroupBy(e => e.Severity).OrderByDescending(g => g.Key);
        var pinnedCount = entries.Count(e => e.Pinned);

        Console.WriteLine();
        Console.WriteLine("  By Type:");
        foreach (var g in byType)
            Console.WriteLine($"    {g.Key,-20} {g.Count()}");

        Console.WriteLine();
        Console.WriteLine("  By Severity:");
        foreach (var g in bySeverity)
            Console.WriteLine($"    {g.Key,-20} {g.Count()}");

        if (pinnedCount > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Pinned: {pinnedCount}");
        }
    }
}, configOption);

rootCommand.AddCommand(statsCommand);

return await rootCommand.InvokeAsync(args);
