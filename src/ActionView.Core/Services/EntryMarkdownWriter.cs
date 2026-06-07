using System.Text;
using System.Text.Json;
using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Serialises an Entry to Markdown / minimal HTML for export.
///
/// The output is lossy for blocks that have no Markdown equivalent
/// (chart, diagram, before/after, video player) - those degrade to a
/// short "[unsupported block type: X]" placeholder plus any plain-text
/// fields we can preserve (label, caption).
/// </summary>
public static class EntryMarkdownWriter
{
    public static string ToMarkdown(Entry entry)
    {
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(entry.Title);
        if (!string.IsNullOrEmpty(entry.Subtitle))
            sb.Append('*').Append(entry.Subtitle).AppendLine("*");
        sb.AppendLine();
        sb.Append("- **Type:** ").AppendLine(entry.Type);
        sb.Append("- **Source:** ").AppendLine(entry.Source);
        sb.Append("- **Severity:** ").AppendLine(entry.Severity.ToString());
        sb.Append("- **Created:** ").AppendLine(entry.CreatedAt.ToString("O"));
        if (entry.Tags.Count > 0)
            sb.Append("- **Tags:** ").AppendLine(string.Join(", ", entry.Tags));
        sb.AppendLine();
        foreach (var block in entry.Content)
        {
            sb.AppendLine(BlockToMarkdown(block, depth: 0));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string ToHtml(Entry entry, string markdown)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.Append("<title>").Append(EscapeHtml(entry.Title)).AppendLine("</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, system-ui, 'Segoe UI', sans-serif; max-width: 880px; margin: 24px auto; padding: 0 16px; color: #1f2937; line-height: 1.55; }");
        sb.AppendLine("pre.av-export { white-space: pre-wrap; word-wrap: break-word; background: #fafafa; border: 1px solid #e5e7eb; border-radius: 6px; padding: 16px; font: 13px/1.55 ui-monospace, SFMono-Regular, Menlo, monospace; }");
        sb.AppendLine("h1 { color: #111827; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.Append("<h1>").Append(EscapeHtml(entry.Title)).AppendLine("</h1>");
        sb.Append("<pre class=\"av-export\">").Append(EscapeHtml(markdown)).AppendLine("</pre>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string EscapeHtml(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");

    private static string BodyAsString(JsonElement? body)
    {
        if (body is null) return string.Empty;
        var b = body.Value;
        if (b.ValueKind == JsonValueKind.String) return b.GetString() ?? string.Empty;
        if (b.ValueKind == JsonValueKind.Undefined || b.ValueKind == JsonValueKind.Null) return string.Empty;
        return b.GetRawText();
    }

    private static string EscapeCell(string s) => s.Replace("|", "\\|").Replace("\n", " ");

    private static string BlockToMarkdown(ContentBlock block, int depth)
    {
        var headingPrefix = new string('#', Math.Min(2 + depth, 6));
        var label = block.Label is not null ? $"{headingPrefix} {block.Label}\n\n" : string.Empty;
        return block.Type switch
        {
            ContentBlockType.Markdown => label + BodyAsString(block.Body),
            ContentBlockType.Code => label + (block.Filename is not null ? $"**{block.Filename}**\n\n" : string.Empty)
                + "```" + (block.Language ?? string.Empty) + "\n" + BodyAsString(block.Body) + "\n```",
            ContentBlockType.Json => label + "```json\n" + (block.Body?.GetRawText() ?? "null") + "\n```",
            ContentBlockType.Table => label + TableToMarkdown(block),
            ContentBlockType.KeyValue => label + KeyValueToMarkdown(block),
            ContentBlockType.Link => label + LinkToMarkdown(block),
            ContentBlockType.Image => label + $"![{block.Alt ?? block.Label ?? string.Empty}]({block.Url ?? BodyAsString(block.Body)})"
                + (block.Caption is not null ? "\n*" + block.Caption + "*" : string.Empty),
            ContentBlockType.Gallery => label + GalleryToMarkdown(block),
            ContentBlockType.Video => label + $"**Video:** [{block.Label ?? block.Url ?? string.Empty}]({block.Url ?? BodyAsString(block.Body)})"
                + (block.Caption is not null ? "\n\n*" + block.Caption + "*" : string.Empty),
            ContentBlockType.File => label + $"**File:** [{block.Filename ?? block.Url ?? string.Empty}]({block.Url ?? string.Empty})",
            ContentBlockType.Diff => label + (block.NewFilename is not null ? $"**{block.NewFilename}**\n\n" : string.Empty)
                + "```diff\n" + BodyAsString(block.Body) + "\n```",
            ContentBlockType.Diagram => label + "```mermaid\n" + BodyAsString(block.Body) + "\n```",
            ContentBlockType.Timeline => label + TimelineToMarkdown(block),
            ContentBlockType.Tabs => label + TabsToMarkdown(block, depth),
            ContentBlockType.Stat => label + StatToMarkdown(block),
            ContentBlockType.Alert => AlertToMarkdown(block),
            ContentBlockType.Section => SectionToMarkdown(block, depth),
            ContentBlockType.BeforeAfter => label + $"**Before:** {block.BeforeUrl ?? string.Empty}\n\n**After:** {block.AfterUrl ?? string.Empty}",
            ContentBlockType.Chart => label + $"*[chart: {block.ChartType ?? "line"}, {block.Series?.Count ?? 0} series]*"
                + (block.Caption is not null ? "\n\n" + block.Caption : string.Empty),
            ContentBlockType.Divider => "---",
            _ => label + $"*[unsupported block type: {block.Type}]*",
        };
    }

    private static string TableToMarkdown(ContentBlock block)
    {
        var cols = block.Columns ?? new List<string>();
        var rows = block.Rows ?? new List<List<JsonElement>>();
        if (cols.Count == 0) return "*(empty table)*";
        var sb = new StringBuilder();
        sb.Append("| ").Append(string.Join(" | ", cols)).AppendLine(" |");
        sb.Append("| ").Append(string.Join(" | ", cols.Select(_ => "---"))).AppendLine(" |");
        foreach (var row in rows)
        {
            sb.Append("| ").Append(string.Join(" | ", row.Select(CellToMarkdown))).AppendLine(" |");
        }
        return sb.ToString();
    }

    private static string KeyValueToMarkdown(ContentBlock block)
    {
        var pairs = block.Pairs ?? new Dictionary<string, JsonElement>();
        return string.Join("\n", pairs.Select(kv => $"- **{kv.Key}:** {CellToMarkdown(kv.Value)}"));
    }

    private static string LinkToMarkdown(ContentBlock block)
    {
        if (block.Links is { Count: > 0 })
        {
            return string.Join("\n", block.Links.Select(l =>
                $"- [{l.Label ?? l.Url}]({l.Url})" + (l.Body is not null ? $"  \n  {l.Body}" : string.Empty)));
        }
        if (block.Url is not null)
        {
            var body = block.Body is { ValueKind: JsonValueKind.String } b ? b.GetString() : null;
            return $"- [{block.Label ?? block.Url}]({block.Url})" + (body is not null ? $"  \n  {body}" : string.Empty);
        }
        return string.Empty;
    }

    private static string GalleryToMarkdown(ContentBlock block)
    {
        var images = block.Images ?? new List<GalleryImage>();
        return string.Join("\n\n", images.Select(img =>
            $"![{img.Alt ?? string.Empty}]({img.Url})" + (img.Caption is not null ? $"  \n*{img.Caption}*" : string.Empty)));
    }

    private static string TimelineToMarkdown(ContentBlock block)
    {
        var events = block.Events ?? new List<TimelineEvent>();
        return string.Join("\n", events.Select(e =>
            $"- **{e.At}** \u2014 {e.Label}" + (e.Body is not null ? "  \n  " + e.Body.Replace("\n", "\n  ") : string.Empty)));
    }

    private static string TabsToMarkdown(ContentBlock block, int depth)
    {
        var tabs = block.Tabs ?? new List<TabItem>();
        var headingPrefix = new string('#', Math.Min(2 + depth, 6));
        return string.Join("\n\n", tabs.Select(t =>
        {
            var inner = string.Join("\n\n", (t.Children ?? new List<ContentBlock>()).Select(c => BlockToMarkdown(c, depth + 1)));
            return $"{headingPrefix}# {t.Label}\n\n{inner}";
        }));
    }

    private static string StatToMarkdown(ContentBlock block)
    {
        var v = block.Value ?? string.Empty;
        var unit = block.Unit is not null ? $" {block.Unit}" : string.Empty;
        var delta = block.Delta is not null ? $" ({block.Delta})" : string.Empty;
        return $"**{v}{unit}**{delta}" + (block.Caption is not null ? $"  \n{block.Caption}" : string.Empty);
    }

    private static string AlertToMarkdown(ContentBlock block)
    {
        var level = (block.Level ?? AlertLevel.Info).ToString().ToUpperInvariant();
        var body = BodyAsString(block.Body);
        var headingLine = block.Label is not null ? $"**{block.Label}**  \n> " : string.Empty;
        return $"> [!{level}] {headingLine}" + string.Join("\n> ", body.Split('\n'));
    }

    private static string SectionToMarkdown(ContentBlock block, int depth)
    {
        var headingPrefix = new string('#', Math.Min(2 + depth, 6));
        var title = block.Title ?? block.Label ?? "Section";
        var inner = string.Join("\n\n", (block.Children ?? new List<ContentBlock>()).Select(c => BlockToMarkdown(c, depth + 1)));
        return $"{headingPrefix} {title}\n\n{inner}";
    }

    private static string CellToMarkdown(JsonElement cell)
    {
        if (cell.ValueKind == JsonValueKind.String) return EscapeCell(cell.GetString() ?? string.Empty);
        if (cell.ValueKind != JsonValueKind.Object) return EscapeCell(cell.ToString());
        if (!cell.TryGetProperty("type", out var typeEl)) return EscapeCell(cell.GetRawText());
        var type = typeEl.GetString() ?? string.Empty;
        var value = cell.TryGetProperty("value", out var vEl) ? vEl.GetString() ?? string.Empty : string.Empty;
        return type switch
        {
            "text" => (cell.TryGetProperty("mono", out var m) && m.ValueKind == JsonValueKind.True) ? "`" + value + "`" : EscapeCell(value),
            "link" => CellLinkToMarkdown(cell, value),
            "status" => CellStatusToMarkdown(cell),
            "badge" => "`" + EscapeCell(cell.TryGetProperty("label", out var bl) ? bl.GetString() ?? string.Empty : string.Empty) + "`",
            "code" => "`" + value.Replace("`", "\\`") + "`",
            "copy" => "`" + (cell.TryGetProperty("display", out var d) ? d.GetString() ?? value : value) + "`",
            "markdown" => value.Replace("|", "\\|").Replace("\n", " "),
            "image" => CellImageToMarkdown(cell),
            _ => EscapeCell(cell.GetRawText()),
        };
    }

    private static string CellLinkToMarkdown(JsonElement cell, string value)
    {
        var label = cell.TryGetProperty("label", out var l) ? l.GetString() ?? string.Empty : value;
        var url = cell.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
        return $"[{EscapeCell(label)}]({url})";
    }

    private static string CellStatusToMarkdown(JsonElement cell)
    {
        var level = cell.TryGetProperty("level", out var lv) ? lv.GetString() : string.Empty;
        var label = cell.TryGetProperty("label", out var sl) ? sl.GetString() ?? string.Empty : string.Empty;
        return $"**{level}: {EscapeCell(label)}**";
    }

    private static string CellImageToMarkdown(JsonElement cell)
    {
        var alt = cell.TryGetProperty("alt", out var a) ? a.GetString() ?? string.Empty : string.Empty;
        var url = cell.TryGetProperty("url", out var iu) ? iu.GetString() ?? string.Empty : string.Empty;
        return $"![{alt}]({url})";
    }
}
