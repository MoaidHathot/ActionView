using System.ComponentModel;
using System.Text.Json;
using ActionView.Core.Services;
using ModelContextProtocol.Server;

namespace ActionView.Mcp.Tools;

[McpServerToolType]
public sealed class TemplateWriteTools
{
    [McpServerTool(Name = "register_template"), Description(
        "Register or update an entry type template for normalization. " +
        "The template JSON must include a 'type' field. " +
        "If a template for this type already exists, it will be overwritten.")]
    public static string RegisterTemplate(
        TemplateRegistry registry,
        JsonSerializerOptions jsonOptions,
        [Description("The template JSON string. Must include at minimum a 'type' field.")] string templateJson)
    {
        try
        {
            var template = registry.Register(templateJson);
            return JsonSerializer.Serialize(new
            {
                success = true,
                type = template.Type,
                description = template.Description,
                contentBlocks = template.ContentTemplate.Count,
                expectedActions = template.ExpectedActions.Count
            }, jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to register template: {ex.Message}" }, jsonOptions);
        }
    }

    [McpServerTool(Name = "remove_template", Destructive = true), Description(
        "Remove a registered entry type template. " +
        "This does not affect existing entries that were already normalized with this template.")]
    public static string RemoveTemplate(
        TemplateRegistry registry,
        JsonSerializerOptions jsonOptions,
        [Description("The entry type name of the template to remove")] string type)
    {
        var removed = registry.Remove(type);
        if (!removed)
            return JsonSerializer.Serialize(new { error = $"No template found for type: {type}" }, jsonOptions);

        return JsonSerializer.Serialize(new { success = true, type, status = "removed" }, jsonOptions);
    }
}
