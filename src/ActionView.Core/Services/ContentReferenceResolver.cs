using System.Text.RegularExpressions;
using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Resolves <c>{{content.*}}</c> and <c>{{entry.*}}</c> placeholders in action
/// command strings against the current entry (see <see cref="ActionContext"/>):
///
/// <list type="bullet">
///   <item><c>{{content.self}}</c> — the text of the block that owns the action (section actions).</item>
///   <item><c>{{content.ID}}</c> — the text of the block whose <see cref="ContentBlock.Id"/> matches.</item>
///   <item><c>{{entry.title|subtitle|type|id|source|severity|tags}}</c> — entry fields (tags joined by ", ").</item>
/// </list>
///
/// Because edits persist to the block's <see cref="ContentBlock.Body"/>, these
/// references expand to the current (possibly edited) text at execution time.
/// Runs after parameter substitution and before secret substitution. Unknown
/// references are left verbatim.
/// </summary>
public sealed partial class ContentReferenceResolver
{
    /// <summary>Resolves all content/entry references in the input against the given context.</summary>
    public string Resolve(string input, ActionContext? context)
    {
        if (string.IsNullOrEmpty(input) || context is null)
            return input;

        return ReferencePattern().Replace(input, match =>
        {
            var ns = match.Groups[1].Value;
            var key = match.Groups[2].Value;
            var resolved = ns == "content"
                ? ResolveContent(key, context)
                : ResolveEntry(key, context.Entry);
            return resolved ?? match.Value; // leave unresolved references untouched
        });
    }

    private static string? ResolveContent(string key, ActionContext context)
    {
        if (string.Equals(key, "self", StringComparison.OrdinalIgnoreCase))
            return context.SelfBlock?.GetText();

        var block = context.Entry is null ? null : FindById(context.Entry.Content, key);
        return block?.GetText();
    }

    private static string? ResolveEntry(string key, Entry? entry)
    {
        if (entry is null) return null;
        return key.ToLowerInvariant() switch
        {
            "title" => entry.Title,
            "subtitle" => entry.Subtitle ?? string.Empty,
            "type" => entry.Type,
            "id" => entry.Id,
            "source" => entry.Source,
            "severity" => entry.Severity.ToString().ToLowerInvariant(),
            "tags" => string.Join(", ", entry.Tags),
            _ => null,
        };
    }

    private static ContentBlock? FindById(IEnumerable<ContentBlock>? blocks, string id)
    {
        if (blocks is null) return null;
        foreach (var block in blocks)
        {
            if (string.Equals(block.Id, id, StringComparison.Ordinal))
                return block;
            var nested = FindById(block.Children, id);
            if (nested is not null) return nested;
        }
        return null;
    }

    // Keys allow hyphens so GUID-style block ids (e.g. a PR comment draft id) work.
    [GeneratedRegex(@"\{\{(content|entry)\.([A-Za-z0-9_-]+)\}\}")]
    private static partial Regex ReferencePattern();
}
