using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Addresses a content block by its positional path within an entry's content
/// tree. A path is the sequence of indices into the content/children arrays at
/// each level, so a block at <c>entry.Content[3].Children[0]</c> has path
/// <c>[3, 0]</c> (serialized over the wire as the dot-delimited string "3.0").
///
/// This replaces the previous "Nth top-level section" scheme, which could not
/// address actions on nested sections (e.g. a per-comment Approve button inside
/// a "Review Comments" section), leaving those buttons inert.
/// </summary>
public static class BlockPath
{
    /// <summary>Parses a dot-delimited path ("3.0" =&gt; [3, 0]). Returns null when empty or malformed.</summary>
    public static List<int>? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;

        var result = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var n) || n < 0) return null;
            result.Add(n);
        }
        return result;
    }

    /// <summary>
    /// Walks the content tree by positional index at each level
    /// (entry.Content, then each block's Children) and returns the addressed
    /// block, or null when any index is out of range.
    /// </summary>
    public static ContentBlock? Resolve(Entry entry, IReadOnlyList<int> path)
    {
        if (entry is null || path is null || path.Count == 0) return null;

        List<ContentBlock>? level = entry.Content;
        ContentBlock? block = null;
        foreach (var idx in path)
        {
            if (level is null || idx < 0 || idx >= level.Count) return null;
            block = level[idx];
            level = block.Children;
        }
        return block;
    }
}
