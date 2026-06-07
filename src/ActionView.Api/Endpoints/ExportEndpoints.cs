using System.Text;
using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Api.Endpoints;

/// <summary>
/// Endpoint that exports an entry as Markdown or HTML for download.
///
/// The client also implements export entirely in the browser; this server
/// endpoint exists for:
///   - CLI scripting (`curl /api/entries/{id}/export?format=md > out.md`)
///   - environments where the client bundle isn't loaded
///   - producing a stable "save the entry to disk" record outside the SPA
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/entries/{id}/export",
            (string id, string? format, EntryStore store) =>
        {
            var entry = store.GetEntry(id) ?? store.GetArchivedEntry(id);
            if (entry is null) return Results.NotFound();

            var fmt = (format ?? "md").ToLowerInvariant();
            var safeName = SanitizeFileName(entry.Title);

            if (fmt is "md" or "markdown")
            {
                var md = EntryMarkdownWriter.ToMarkdown(entry);
                return Results.File(Encoding.UTF8.GetBytes(md), "text/markdown; charset=utf-8", $"{safeName}.md");
            }
            if (fmt == "html")
            {
                var md = EntryMarkdownWriter.ToMarkdown(entry);
                var html = EntryMarkdownWriter.ToHtml(entry, md);
                return Results.File(Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8", $"{safeName}.html");
            }
            if (fmt == "json")
            {
                var json = System.Text.Json.JsonSerializer.Serialize(entry, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });
                return Results.File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", $"{safeName}.json");
            }
            return Results.BadRequest(new { error = "Unsupported format. Use 'md', 'html', or 'json'." });
        });
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "entry";
        var sb = new StringBuilder();
        foreach (var ch in name)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_' ? ch : '_');
            if (sb.Length >= 80) break;
        }
        var s = sb.ToString().Trim('_');
        return s.Length == 0 ? "entry" : s;
    }
}
