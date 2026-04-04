using System.ComponentModel;
using System.Text.Json;
using ActionView.Core.Services;
using ModelContextProtocol.Server;

namespace ActionView.Mcp.Tools;

[McpServerToolType]
public sealed class TemplateReadTools
{
    [McpServerTool(Name = "list_templates", ReadOnly = true), Description(
        "List all registered entry type templates. " +
        "Templates define the canonical shape for entry types and are used for normalization.")]
    public static string ListTemplates(
        TemplateRegistry registry,
        JsonSerializerOptions jsonOptions)
    {
        var templates = registry.GetAll();
        var summaries = templates.Select(t => new
        {
            type = t.Type,
            description = t.Description,
            contentBlocks = t.ContentTemplate.Count,
            expectedActions = t.ExpectedActions.Count
        }).ToList();

        return JsonSerializer.Serialize(new { count = summaries.Count, templates = summaries }, jsonOptions);
    }

    [McpServerTool(Name = "get_template", ReadOnly = true), Description(
        "Get the full definition of an entry type template. " +
        "Returns the complete template JSON including defaults, content template blocks, key aliases, and expected actions.")]
    public static string GetTemplate(
        TemplateRegistry registry,
        JsonSerializerOptions jsonOptions,
        [Description("The entry type name (e.g., 'pr-review', 'deploy', 'incident')")] string type)
    {
        var template = registry.GetTemplate(type);
        if (template is null)
            return JsonSerializer.Serialize(new { error = $"No template found for type: {type}" }, jsonOptions);

        return TemplateRegistry.ToJson(template);
    }
}
