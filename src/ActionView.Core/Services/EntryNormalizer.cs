using System.Text.Json;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Normalizes incoming entries against their type template.
/// Applies defaults, normalizes key names, reorders content blocks,
/// and normalizes section titles to ensure consistency.
/// </summary>
public sealed class EntryNormalizer
{
    private readonly TemplateRegistry _registry;
    private readonly ILogger<EntryNormalizer> _logger;

    public EntryNormalizer(TemplateRegistry registry, ILogger<EntryNormalizer> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Normalize an entry against its type template.
    /// If no template exists for the entry type, returns the entry unchanged.
    /// </summary>
    public Entry Normalize(Entry entry)
    {
        var template = _registry.GetTemplate(entry.Type);
        if (template is null)
            return entry;

        ApplyDefaults(entry, template);
        NormalizeContentBlocks(entry, template);

        return entry;
    }

    private void ApplyDefaults(Entry entry, EntryTemplate template)
    {
        if (string.IsNullOrWhiteSpace(entry.Icon) && template.Defaults.Icon is not null)
        {
            entry.Icon = template.Defaults.Icon;
            _logger.LogDebug("Applied default icon '{Icon}' to entry {Id}", template.Defaults.Icon, entry.Id);
        }

        if (entry.Severity == Severity.Medium && template.Defaults.Severity.HasValue
            && template.Defaults.Severity.Value != Severity.Medium)
        {
            entry.Severity = template.Defaults.Severity.Value;
            _logger.LogDebug("Applied default severity '{Severity}' to entry {Id}", template.Defaults.Severity, entry.Id);
        }

        if (entry.Tags.Count == 0 && template.Defaults.Tags is { Count: > 0 })
        {
            entry.Tags = new List<string>(template.Defaults.Tags);
            _logger.LogDebug("Applied default tags to entry {Id}", entry.Id);
        }
    }

    private void NormalizeContentBlocks(Entry entry, EntryTemplate template)
    {
        // Normalize individual blocks (key aliases, section title aliases)
        foreach (var block in entry.Content)
        {
            NormalizeBlock(block, template, entry.Id);
        }

        // Reorder content blocks to match template order
        ReorderBlocks(entry, template);

        // Log warnings for missing required blocks
        foreach (var templateBlock in template.ContentTemplate.Where(t => t.Required))
        {
            var found = FindMatchingBlock(entry.Content, templateBlock);
            if (found is null)
            {
                _logger.LogWarning(
                    "Entry {Id} (type: {Type}) is missing required {BlockType} block{Label}",
                    entry.Id, entry.Type, templateBlock.Type,
                    templateBlock.Label is not null ? $" '{templateBlock.Label}'" :
                    templateBlock.Title is not null ? $" '{templateBlock.Title}'" : "");
            }
        }
    }

    private void NormalizeBlock(ContentBlock block, EntryTemplate template, string entryId)
    {
        // Find matching template block
        var templateBlock = FindMatchingTemplateBlock(block, template);
        if (templateBlock is null)
            return;

        // Normalize keyValue block keys
        if (block.Type == ContentBlockType.KeyValue && block.Pairs is not null && templateBlock.KeyAliases is not null)
        {
            NormalizeKeyValuePairs(block, templateBlock, entryId);
        }

        // Normalize section titles
        if (block.Type == ContentBlockType.Section && block.Title is not null && templateBlock.TitleAliases is not null)
        {
            NormalizeSectionTitle(block, templateBlock, entryId);
        }

        // Apply label from template if block has no label
        if (string.IsNullOrWhiteSpace(block.Label) && templateBlock.Label is not null)
        {
            block.Label = templateBlock.Label;
        }

        // Recursively normalize section children
        if (block.Type == ContentBlockType.Section && block.Children is not null)
        {
            foreach (var child in block.Children)
            {
                NormalizeBlock(child, template, entryId);
            }
        }
    }

    private void NormalizeKeyValuePairs(ContentBlock block, ContentTemplateBlock templateBlock, string entryId)
    {
        if (block.Pairs is null || templateBlock.KeyAliases is null)
            return;

        var normalized = new Dictionary<string, JsonElement>();

        foreach (var (key, value) in block.Pairs)
        {
            // Check if this key matches any alias (case-insensitive)
            var canonicalKey = key;
            foreach (var (alias, canonical) in templateBlock.KeyAliases)
            {
                if (string.Equals(key, alias, StringComparison.OrdinalIgnoreCase))
                {
                    canonicalKey = canonical;
                    _logger.LogDebug("Normalized key '{Old}' -> '{New}' in entry {Id}", key, canonical, entryId);
                    break;
                }
            }

            // Also check if the key itself matches a canonical name with different casing
            if (canonicalKey == key && templateBlock.RequiredKeys is not null)
            {
                var matchedCanonical = templateBlock.RequiredKeys
                    .FirstOrDefault(rk => string.Equals(rk, key, StringComparison.OrdinalIgnoreCase));
                if (matchedCanonical is not null && matchedCanonical != key)
                {
                    canonicalKey = matchedCanonical;
                    _logger.LogDebug("Normalized key casing '{Old}' -> '{New}' in entry {Id}", key, matchedCanonical, entryId);
                }
            }

            normalized[canonicalKey] = value;
        }

        block.Pairs = normalized;
    }

    private void NormalizeSectionTitle(ContentBlock block, ContentTemplateBlock templateBlock, string entryId)
    {
        if (block.Title is null || templateBlock.Title is null || templateBlock.TitleAliases is null)
            return;

        // Check if current title matches any alias
        foreach (var alias in templateBlock.TitleAliases)
        {
            if (string.Equals(block.Title, alias, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Normalized section title '{Old}' -> '{New}' in entry {Id}",
                    block.Title, templateBlock.Title, entryId);
                block.Title = templateBlock.Title;
                return;
            }
        }

        // Also normalize casing of the canonical title
        if (string.Equals(block.Title, templateBlock.Title, StringComparison.OrdinalIgnoreCase)
            && block.Title != templateBlock.Title)
        {
            block.Title = templateBlock.Title;
        }
    }

    private void ReorderBlocks(Entry entry, EntryTemplate template)
    {
        if (entry.Content.Count == 0 || template.ContentTemplate.Count == 0)
            return;

        var ordered = new List<ContentBlock>();
        var remaining = new List<ContentBlock>(entry.Content);

        // Place blocks that match template order first
        foreach (var templateBlock in template.ContentTemplate)
        {
            var match = FindMatchingBlock(remaining, templateBlock);
            if (match is not null)
            {
                ordered.Add(match);
                remaining.Remove(match);
            }
        }

        // Append any remaining blocks that didn't match any template slot
        ordered.AddRange(remaining);

        entry.Content = ordered;
    }

    private static ContentBlock? FindMatchingBlock(List<ContentBlock> blocks, ContentTemplateBlock templateBlock)
    {
        return blocks.FirstOrDefault(b =>
        {
            if (b.Type != templateBlock.Type) return false;

            // For sections, match by title (including aliases)
            if (templateBlock.Type == ContentBlockType.Section && templateBlock.Title is not null)
            {
                if (string.Equals(b.Title, templateBlock.Title, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (templateBlock.TitleAliases is not null)
                {
                    return templateBlock.TitleAliases.Any(a =>
                        string.Equals(b.Title, a, StringComparison.OrdinalIgnoreCase));
                }

                return false;
            }

            // For labeled blocks, match by label
            if (templateBlock.Label is not null)
            {
                return string.Equals(b.Label, templateBlock.Label, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        });
    }

    private static ContentTemplateBlock? FindMatchingTemplateBlock(ContentBlock block, EntryTemplate template)
    {
        return template.ContentTemplate.FirstOrDefault(t =>
        {
            if (t.Type != block.Type) return false;

            if (t.Type == ContentBlockType.Section && t.Title is not null)
            {
                if (string.Equals(block.Title, t.Title, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (t.TitleAliases is not null)
                {
                    return t.TitleAliases.Any(a =>
                        string.Equals(block.Title, a, StringComparison.OrdinalIgnoreCase));
                }

                return false;
            }

            if (t.Label is not null)
            {
                return string.Equals(block.Label, t.Label, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        });
    }
}
