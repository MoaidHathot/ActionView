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
    /// Supported: markdown, code, json, table, key-value, link, section, divider, alert.
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

    /// <summary>URL for link blocks.</summary>
    public string? Url { get; set; }

    // --- Image specific ---

    /// <summary>Image source: a URL or a base64 data URI (data:image/png;base64,...).</summary>
    public string? Src { get; set; }

    /// <summary>Alt text for the image.</summary>
    public string? Alt { get; set; }

    /// <summary>Optional max-width in pixels for the image.</summary>
    public int? Width { get; set; }
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
    Diff,
    Image
}

public enum AlertLevel
{
    Info,
    Warning,
    Error,
    Success
}
