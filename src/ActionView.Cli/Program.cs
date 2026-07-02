using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ActionView.Cli;
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

var setOption = new Option<string[]>(
    "--set",
    "Set a top-level field before submitting, as key=value (repeatable). Dotted keys and JSON " +
    "values are supported, e.g. --set priority=5 --set groupId=ci-1847 --set 'tags=[\"work\"]'")
{
    AllowMultipleArgumentsPerToken = false
};
addCommand.AddOption(setOption);

var groupIdOption = new Option<string?>("--group-id", "Shortcut for --set groupId=<value>");
addCommand.AddOption(groupIdOption);
var groupLabelOption = new Option<string?>("--group-label", "Shortcut for --set groupLabel=<value>");
addCommand.AddOption(groupLabelOption);
var priorityOption = new Option<int?>("--priority", "Shortcut for --set priority=<value>");
addCommand.AddOption(priorityOption);
var pinOption = new Option<bool>("--pin", "Shortcut for --set pinned=true");
addCommand.AddOption(pinOption);
var waitOption = new Option<bool>(
    "--wait",
    "Validate synchronously before writing to the inbox; fail fast with a precise report " +
    "(non-zero exit) instead of discovering errors later in errors/");
addCommand.AddOption(waitOption);
var addStrictOption = new Option<bool>(
    "--strict",
    "With --wait, also treat warnings (e.g. a missing required content block) as failures");
addCommand.AddOption(addStrictOption);

addCommand.SetHandler((InvocationContext ctx) =>
{
    var file = ctx.ParseResult.GetValueForOption(fileOption);
    var inlineJson = ctx.ParseResult.GetValueForOption(jsonOption);
    var configPath = ctx.ParseResult.GetValueForOption(configOption);
    var sets = ctx.ParseResult.GetValueForOption(setOption) ?? [];
    var groupId = ctx.ParseResult.GetValueForOption(groupIdOption);
    var groupLabel = ctx.ParseResult.GetValueForOption(groupLabelOption);
    var priority = ctx.ParseResult.GetValueForOption(priorityOption);
    var pin = ctx.ParseResult.GetValueForOption(pinOption);
    var wait = ctx.ParseResult.GetValueForOption(waitOption);
    var strict = ctx.ParseResult.GetValueForOption(addStrictOption);

    string json;
    string sourceName;

    // --file - means read from stdin explicitly
    if (file is not null && file.Name == "-")
    {
        var stdinContent = ReadStdin();
        if (string.IsNullOrWhiteSpace(stdinContent))
        {
            Console.Error.WriteLine("Error: --file - specified but no data received on stdin.");
            ctx.ExitCode = 1;
            return;
        }
        json = stdinContent;
        sourceName = "stdin.json";
    }
    else if (file is not null && inlineJson is not null)
    {
        Console.Error.WriteLine("Error: Provide either --file, --json, or pipe via stdin, not multiple.");
        ctx.ExitCode = 1;
        return;
    }
    else if (file is not null)
    {
        if (!file.Exists)
        {
            Console.Error.WriteLine($"File not found: {file.FullName}");
            ctx.ExitCode = 1;
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
            ctx.ExitCode = 1;
            return;
        }
    }

    // Apply --set / shortcut mutations to the raw JSON before validating/writing.
    var mutations = new List<(string Key, JsonNode? Value)>();
    foreach (var s in sets)
    {
        var eq = s.IndexOf('=');
        if (eq <= 0)
        {
            Console.Error.WriteLine($"Error: --set expects key=value, got '{s}'.");
            ctx.ExitCode = 1;
            return;
        }
        mutations.Add((s[..eq], ParseCliValue(s[(eq + 1)..])));
    }

    // --group-id / --group-label are string options with a defaulted-empty value in many
    // callers. Two hazards to defend against:
    //   1. Empty value  -> omit the field entirely (never inject a meaningless "").
    //   2. A shell that drops an empty "" argument collapses `--group-id "" --wait` into
    //      `--group-id --wait`, so the parser swallows the *next flag* as the value. Reject
    //      flag-looking values loudly instead of silently corrupting the entry.
    if (!TryAddStringFlag("--group-id", "groupId", groupId, mutations, ctx)) return;
    if (!TryAddStringFlag("--group-label", "groupLabel", groupLabel, mutations, ctx)) return;
    if (priority is not null) mutations.Add(("priority", JsonValue.Create(priority.Value)));
    if (pin) mutations.Add(("pinned", JsonValue.Create(true)));

    if (mutations.Count > 0)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject
                ?? throw new JsonException("Entry JSON must be a top-level object.");
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error: Invalid JSON - {ex.Message}");
            ctx.ExitCode = 1;
            return;
        }

        foreach (var (key, value) in mutations)
            ApplySet(root, key, value);

        json = root.ToJsonString(jsonWriteOptions);
    }

    var config = ConfigLoader.Load(configPath);
    var inboxDir = Path.Combine(config.DataDirectory, "inbox");

    // --wait / --strict: validate synchronously and fail fast with a precise, structured
    // report before touching the inbox (runs before deserialization so schema problems such
    // as a bad enum surface as clean diagnostics rather than a raw binder exception).
    if (wait || strict)
    {
        var validator = CreateValidator(config);
        var result = validator.Validate(json, new EntryValidationOptions { Strict = strict });
        if (!result.Ok)
        {
            Console.Error.WriteLine("Validation failed:");
            Console.Error.WriteLine(EntryValidator.FormatDiagnostics(result));
            ctx.ExitCode = 1;
            return;
        }
        if (result.Warnings.Count > 0)
        {
            Console.Error.WriteLine("Validation warnings:");
            Console.Error.WriteLine(EntryValidator.FormatDiagnostics(result));
        }
    }

    Entry? entry;
    try
    {
        entry = JsonSerializer.Deserialize<Entry>(json, jsonReadOptions);
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"Error: Invalid JSON - {ex.Message}");
        ctx.ExitCode = 1;
        return;
    }

    if (entry is null)
    {
        Console.Error.WriteLine("Error: Input does not contain a valid entry JSON.");
        ctx.ExitCode = 1;
        return;
    }

    if (string.IsNullOrWhiteSpace(entry.Type) ||
        string.IsNullOrWhiteSpace(entry.Source) ||
        string.IsNullOrWhiteSpace(entry.Title))
    {
        Console.Error.WriteLine("Error: Entry is missing required fields (type, source, title).");
        ctx.ExitCode = 1;
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
});

rootCommand.AddCommand(addCommand);

// ================================================================
// list command
// ================================================================
var listCommand = new Command("list", "List active entries");
var typeFilter = new Option<string?>("--type", "Filter by entry type");
var severityFilter = new Option<string?>("--severity", "Filter by severity (low, medium, high, critical)");
var sourceFilter = new Option<string?>("--source", "Filter by source");
var searchFilter = new Option<string?>("--search", "Search in title, subtitle, source, tags");
var tagsFilter = new Option<string?>("--tags", "Filter by tags (comma-separated)");
var tagModeFilter = new Option<string?>("--tag-mode", "Tag match mode: any (OR) or all (AND)");
var sortOption = new Option<string?>("--sort", "Sort field: created, priority, severity, title");
var dirOption = new Option<string?>("--dir", "Sort direction: asc or desc");
var viewOption = new Option<string?>("--view", "Apply a saved view by id or name");
listCommand.AddOption(typeFilter);
listCommand.AddOption(severityFilter);
listCommand.AddOption(sourceFilter);
listCommand.AddOption(searchFilter);
listCommand.AddOption(tagsFilter);
listCommand.AddOption(tagModeFilter);
listCommand.AddOption(sortOption);
listCommand.AddOption(dirOption);
listCommand.AddOption(viewOption);

// Uses an InvocationContext handler because the option count exceeds the
// strongly-typed SetHandler overloads.
listCommand.SetHandler((InvocationContext ctx) =>
{
    var configPath = ctx.ParseResult.GetValueForOption(configOption);
    var type = ctx.ParseResult.GetValueForOption(typeFilter);
    var severity = ctx.ParseResult.GetValueForOption(severityFilter);
    var source = ctx.ParseResult.GetValueForOption(sourceFilter);
    var search = ctx.ParseResult.GetValueForOption(searchFilter);
    var tags = ctx.ParseResult.GetValueForOption(tagsFilter);
    var tagMode = ctx.ParseResult.GetValueForOption(tagModeFilter);
    var sortField = ctx.ParseResult.GetValueForOption(sortOption);
    var sortDir = ctx.ParseResult.GetValueForOption(dirOption);
    var view = ctx.ParseResult.GetValueForOption(viewOption);

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

    var criteria = EntryFiltering.ResolveCriteria(
        config.Views, config.TagMatchMode, view,
        type, severity, source, tags, tagMode, search);

    var results = EntryQuery.RunActive(
        entries, criteria,
        EntrySorting.TryParseField(sortField), EntrySorting.ParseDirection(sortDir));

    if (results.Count == 0)
    {
        Console.WriteLine("No active entries.");
        return;
    }

    Console.WriteLine($"{"ID",-34} {"Sev",-10} {"Status",-8} {"Type",-16} {"Title"}");
    Console.WriteLine(new string('-', 100));

    foreach (var entry in results)
    {
        var id = entry.Id.Length > 32 ? entry.Id[..32] : entry.Id;
        var title = entry.Title.Length > 40 ? entry.Title[..37] + "..." : entry.Title;
        var pin = entry.Pinned ? "*" : " ";
        Console.WriteLine($"{pin}{id,-33} {entry.Severity,-10} {entry.Status,-8} {entry.Type,-16} {title}");
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {results.Count} active entries");
});

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
    Console.Write(EntrySchemaProvider.RawJson);
});

rootCommand.AddCommand(schemaCommand);

// ================================================================
// validate command
// ================================================================
var validateCommand = new Command("validate",
    "Validate an entry JSON against the schema and its type template without adding it. " +
    "Prints a { ok, errors[], warnings[] } report and exits non-zero on failure.");

var validateFileOption = new Option<FileInfo?>("--file", "Path to the entry JSON file (use '-' to read from stdin)");
validateFileOption.AddAlias("-f");
validateCommand.AddOption(validateFileOption);

var validateJsonOption = new Option<string?>("--json", "Inline entry JSON string");
validateJsonOption.AddAlias("-j");
validateCommand.AddOption(validateJsonOption);

var validateTypeOption = new Option<string?>("--type", "Override the entry type before validating (selects which template applies)");
validateCommand.AddOption(validateTypeOption);

var validateStrictOption = new Option<bool>("--strict", "Treat warnings (e.g. a missing required content block) as errors");
validateCommand.AddOption(validateStrictOption);

validateCommand.SetHandler((InvocationContext ctx) =>
{
    var file = ctx.ParseResult.GetValueForOption(validateFileOption);
    var inlineJson = ctx.ParseResult.GetValueForOption(validateJsonOption);
    var typeOverride = ctx.ParseResult.GetValueForOption(validateTypeOption);
    var strict = ctx.ParseResult.GetValueForOption(validateStrictOption);
    var configPath = ctx.ParseResult.GetValueForOption(configOption);

    string json;
    if (file is not null && file.Name == "-")
    {
        var stdin = ReadStdin();
        if (string.IsNullOrWhiteSpace(stdin))
        {
            Console.Error.WriteLine("Error: --file - specified but no data received on stdin.");
            ctx.ExitCode = 2;
            return;
        }
        json = stdin;
    }
    else if (file is not null && inlineJson is not null)
    {
        Console.Error.WriteLine("Error: Provide either --file, --json, or pipe via stdin, not multiple.");
        ctx.ExitCode = 2;
        return;
    }
    else if (file is not null)
    {
        if (!file.Exists)
        {
            Console.Error.WriteLine($"File not found: {file.FullName}");
            ctx.ExitCode = 2;
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
        var stdin = ReadStdin();
        if (string.IsNullOrWhiteSpace(stdin))
        {
            Console.Error.WriteLine("Error: Provide input via --file, --json, or pipe JSON through stdin.");
            ctx.ExitCode = 2;
            return;
        }
        json = stdin;
    }

    if (typeOverride is not null)
    {
        try
        {
            var root = JsonNode.Parse(json) as JsonObject
                ?? throw new JsonException("Entry JSON must be a top-level object.");
            root["type"] = JsonValue.Create(typeOverride);
            json = root.ToJsonString(jsonWriteOptions);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error: Invalid JSON - {ex.Message}");
            ctx.ExitCode = 2;
            return;
        }
    }

    var config = ConfigLoader.Load(configPath);
    var validator = CreateValidator(config);
    var result = validator.Validate(json, new EntryValidationOptions { Strict = strict });

    Console.WriteLine(JsonSerializer.Serialize(result, jsonWriteOptions));
    ctx.ExitCode = result.Ok ? 0 : 1;
});

rootCommand.AddCommand(validateCommand);

// ================================================================
// template command group
// ================================================================
var templateCommand = new Command("template", "Manage entry type templates for normalization");

// template list
var templateListCommand = new Command("list", "List all registered templates");
templateListCommand.SetHandler((string? configPath) =>
{
    var config = ConfigLoader.Load(configPath);
    var registry = CreateRegistry(config);

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
    var registry = CreateRegistry(config);

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
    var registry = CreateRegistry(config);

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
    var registry = CreateRegistry(config);

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

// --- Helpers ---

/// <summary>
/// Create a TemplateRegistry and run the external template scan if configured.
/// </summary>
static TemplateRegistry CreateRegistry(AppConfig config)
{
    var registry = new TemplateRegistry(config.DataDirectory, NullLogger<TemplateRegistry>.Instance);
    if (config.Templates.ExternalDirectory is not null)
    {
        var scanner = new TemplateScanner(registry, config.DataDirectory, NullLogger<TemplateScanner>.Instance);
        scanner.Scan(config.Templates.ExternalDirectory, config.Templates.Recursive);
    }
    return registry;
}

/// <summary>
/// Build an in-process EntryValidator (schema + template normalization) for the
/// `validate` command and `add --wait`, without needing the server running.
/// </summary>
static EntryValidator CreateValidator(AppConfig config)
{
    var registry = CreateRegistry(config);
    var normalizer = new EntryNormalizer(registry, NullLogger<EntryNormalizer>.Instance);
    return new EntryValidator(normalizer);
}

/// <summary>
/// Records a string shortcut option (e.g. --group-id) as a mutation, defensively:
/// an empty/whitespace value is omitted (never injected as ""), and a flag-looking value
/// (leading '-') is rejected — it almost always means the shell dropped an empty argument
/// and the parser swallowed the following flag (e.g. `--group-id "" --wait` -> the value
/// "--wait"). Returns false and sets a non-zero exit code when it rejects the value.
/// </summary>
static bool TryAddStringFlag(
    string flag, string field, string? value,
    List<(string Key, JsonNode? Value)> mutations, InvocationContext ctx)
{
    switch (CliArg.ClassifyStringFlag(value, out var cleaned))
    {
        case StringFlagDisposition.Omit:
            return true;

        case StringFlagDisposition.Reject:
            Console.Error.WriteLine(
                $"Error: {flag} received '{value}', which looks like a flag. Its value was probably " +
                $"dropped by the shell (e.g. an empty \"\" argument). Omit {flag} when you have no value, " +
                $"or pass it via --set {field}=<value>.");
            ctx.ExitCode = 1;
            return false;

        default:
            mutations.Add((field, JsonValue.Create(cleaned)));
            return true;
    }
}

/// <summary>
/// Set a (possibly dotted) key on a JSON object, creating intermediate objects as needed.
/// </summary>
static void ApplySet(JsonObject root, string dottedKey, JsonNode? value)
{
    var parts = dottedKey.Split('.');
    var current = root;
    for (var i = 0; i < parts.Length - 1; i++)
    {
        if (current[parts[i]] is JsonObject child)
        {
            current = child;
        }
        else
        {
            var created = new JsonObject();
            current[parts[i]] = created;
            current = created;
        }
    }
    current[parts[^1]] = value;
}

/// <summary>
/// Parse a --set value: try JSON (numbers, booleans, arrays, objects, quoted strings),
/// falling back to a bare string when it is not valid JSON (e.g. --set groupId=ci-1847).
/// </summary>
static JsonNode? ParseCliValue(string raw)
{
    try
    {
        return JsonNode.Parse(raw);
    }
    catch (JsonException)
    {
        return JsonValue.Create(raw);
    }
}
