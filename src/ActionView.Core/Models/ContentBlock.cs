using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActionView.Core.Models;

/// <summary>
/// A single renderable block of content within an entry.
/// The frontend dispatches rendering based on the Type field.
/// </summary>
public sealed class ContentBlock
{
    /// <summary>
    /// Block type that determines how this block is rendered.
    /// Supported: markdown, code, json, table, key-value, link, section, divider, alert, image.
    /// </summary>
    public required ContentBlockType Type { get; set; }

    /// <summary>Optional label/heading for the block.</summary>
    public string? Label { get; set; }

    /// <summary>
    /// The primary content. Interpretation depends on Type:
    /// - markdown: the markdown string
    /// - code: the source code string
    /// - json: a JSON object/value (serialized as JsonElement)
    /// - alert: the alert message text
    /// - link: the URL
    /// - key-value, table, section: unused (use dedicated fields)
    /// - divider: unused
    /// - image: unused (use Url for the source)
    /// </summary>
    public JsonElement? Body { get; set; }

    // --- Code block specific ---

    /// <summary>Programming language for syntax highlighting.</summary>
    public string? Language { get; set; }

    /// <summary>Filename to display above the code block.</summary>
    public string? Filename { get; set; }

    /// <summary>Line numbers to highlight.</summary>
    public List<int>? Highlight { get; set; }

    // --- Table specific ---

    /// <summary>Column headers for table blocks.</summary>
    public List<string>? Columns { get; set; }

    /// <summary>Row data for table blocks. Each row is a list of cell values.</summary>
    public List<List<string>>? Rows { get; set; }

    // --- Key-Value specific ---

    /// <summary>Key-value pairs for key-value blocks.</summary>
    public Dictionary<string, string>? Pairs { get; set; }

    // --- Section specific ---

    /// <summary>Section title.</summary>
    public string? Title { get; set; }

    /// <summary>Nested content blocks within a section.</summary>
    [JsonPropertyName("content")]
    public List<ContentBlock>? Children { get; set; }

    /// <summary>Actions scoped to this section.</summary>
    public List<EntryAction>? Actions { get; set; }

    // --- Alert specific ---

    /// <summary>Alert level: info, warning, error, success.</summary>
    public AlertLevel? Level { get; set; }

    // --- Link specific ---

    /// <summary>URL for link blocks and image blocks.</summary>
    public string? Url { get; set; }

    // --- Image specific ---

    /// <summary>Alternative text for image blocks. Shown if the image fails to load and used by assistive tech.</summary>
    public string? Alt { get; set; }

    /// <summary>Optional caption rendered beneath an image block.</summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Maximum width (in CSS pixels) for the rendered thumbnail of an image block.
    /// The lightbox view always expands to the viewport regardless of this value.
    /// </summary>
    public int? MaxWidth { get; set; }
}

public enum ContentBlockType
{
    Markdown,
    Code,
    Json,
    Table,
    KeyValue,
    Link,
    Section,
    Divider,
    Alert,
    Image
}

public enum AlertLevel
{
    Info,
    Warning,
    Error,
    Success
}
