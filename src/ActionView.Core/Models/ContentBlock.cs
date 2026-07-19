using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActionView.Core.Models;

/// <summary>
/// A single renderable block of content within an entry.
/// The frontend dispatches rendering based on the Type field.
///
/// This is intentionally a wide "junk drawer" type: many of the per-Type
/// fields are mutually exclusive (e.g. <see cref="Rows"/> only applies to
/// <see cref="ContentBlockType.Table"/>) but keeping them on a single record
/// keeps the JSON contract flat for entry authors and avoids per-type
/// polymorphic discriminator wiring on the C# side.
/// </summary>
public sealed class ContentBlock
{
    /// <summary>Block type that determines how this block is rendered.</summary>
    public required ContentBlockType Type { get; set; }

    /// <summary>
    /// Optional stable identifier for this block. When set, it is used as the
    /// target key for action outcome markers (so a marker survives block
    /// reordering); otherwise the block's positional path is used. Producers
    /// that want durable per-target state (e.g. a PR comment's draft id) should
    /// set this.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>Optional label/heading for the block.</summary>
    public string? Label { get; set; }

    /// <summary>
    /// The primary content. Interpretation depends on Type:
    /// - markdown: the markdown string
    /// - code: the source code string
    /// - json: a JSON object/value (serialized as JsonElement)
    /// - alert: the alert message text
    /// - link: the URL (or use Url field)
    /// - image: image URL (or use Url field)
    /// - diff: the unified diff text
    /// - diagram: the Mermaid source
    /// - video: the video URL (or use Url + Provider/VideoId)
    /// - stat: the value (or use Value field)
    /// - file: ignored (use Url + Filename)
    /// - gallery, timeline, tabs, chart, key-value, table, section, divider, beforeAfter: unused
    /// </summary>
    public JsonElement? Body { get; set; }

    // --- Code block specific ---

    /// <summary>Programming language for syntax highlighting (code, diff blocks).</summary>
    public string? Language { get; set; }

    /// <summary>Filename to display above code blocks, and used as the download name for file blocks.</summary>
    public string? Filename { get; set; }

    /// <summary>Line numbers to highlight in code blocks.</summary>
    public List<int>? Highlight { get; set; }

    /// <summary>Whether code blocks should show line numbers. Defaults to true.</summary>
    public bool? ShowLineNumbers { get; set; }

    /// <summary>Whether code blocks should soft-wrap long lines. Defaults to true.</summary>
    public bool? WordWrap { get; set; }

    /// <summary>Per-line annotations for code blocks (review-style inline comments).</summary>
    public List<CodeAnnotation>? Annotations { get; set; }

    // --- Table specific ---

    /// <summary>Column headers for table blocks.</summary>
    public List<string>? Columns { get; set; }

    /// <summary>
    /// Row data for table blocks. Each cell may be a plain string or a
    /// rich-cell object: <c>{ "type": "link"|"status"|"code"|"copy"|"badge"|"markdown", ... }</c>.
    /// Stored as raw JSON; the client interprets per-cell.
    /// </summary>
    public List<List<JsonElement>>? Rows { get; set; }

    /// <summary>Whether table column headers should be click-to-sort. Defaults to false.</summary>
    public bool? Sortable { get; set; }

    /// <summary>Whether a filter/search box should be shown above the table. Defaults to false.</summary>
    public bool? Filterable { get; set; }

    // --- Key-Value specific ---

    /// <summary>
    /// Key-value pairs for keyValue blocks. Values may be plain strings or
    /// rich-value objects (same shape as table cells).
    /// </summary>
    public Dictionary<string, JsonElement>? Pairs { get; set; }

    // --- Section / Tab specific ---

    /// <summary>Section / tab title.</summary>
    public string? Title { get; set; }

    /// <summary>Nested content blocks within a section.</summary>
    [JsonPropertyName("content")]
    public List<ContentBlock>? Children { get; set; }

    /// <summary>Actions scoped to this section.</summary>
    public List<EntryAction>? Actions { get; set; }

    /// <summary>Whether the section should start collapsed. Defaults to false (expanded).</summary>
    public bool? DefaultCollapsed { get; set; }

    /// <summary>Optional badge text rendered next to the section title (e.g. "11 sources", "5 pending").</summary>
    public string? Badge { get; set; }

    // --- Alert specific ---

    /// <summary>Alert level: info, warning, error, success.</summary>
    public AlertLevel? Level { get; set; }

    /// <summary>Whether the alert can be dismissed by the user (per-browser, persisted in localStorage).</summary>
    public bool? Dismissible { get; set; }

    // --- Link / Image / Video / File shared ---

    /// <summary>URL for link, image, video, and file blocks.</summary>
    public string? Url { get; set; }

    /// <summary>Multiple links for link blocks (alternative to single <see cref="Url"/>).</summary>
    public List<LinkItem>? Links { get; set; }

    /// <summary>Optional icon name (Lucide) for link / file / stat blocks.</summary>
    public string? Icon { get; set; }

    // --- Image specific ---

    /// <summary>Alternative text for image blocks.</summary>
    public string? Alt { get; set; }

    /// <summary>Optional caption rendered beneath image / gallery / video / chart / diagram blocks.</summary>
    public string? Caption { get; set; }

    /// <summary>Maximum thumbnail width in CSS pixels.</summary>
    public int? MaxWidth { get; set; }

    /// <summary>Optional URL to open when the image is clicked instead of the lightbox (e.g. jump to a YouTube timestamp).</summary>
    public string? TimestampUrl { get; set; }

    /// <summary>Optional overlay annotations on an image (arrows, boxes, text).</summary>
    public List<ImageAnnotation>? ImageAnnotations { get; set; }

    /// <summary>For beforeAfter blocks: the "before" image URL.</summary>
    public string? BeforeUrl { get; set; }

    /// <summary>For beforeAfter blocks: the "after" image URL.</summary>
    public string? AfterUrl { get; set; }

    /// <summary>For beforeAfter blocks: label for the "before" side.</summary>
    public string? BeforeLabel { get; set; }

    /// <summary>For beforeAfter blocks: label for the "after" side.</summary>
    public string? AfterLabel { get; set; }

    // --- Gallery specific ---

    /// <summary>Images for a gallery block.</summary>
    public List<GalleryImage>? Images { get; set; }

    // --- Video specific ---

    /// <summary>Video provider: "youtube", "vimeo", or "file" (served via /api/files or any URL).</summary>
    public string? Provider { get; set; }

    /// <summary>Provider-specific video ID (for YouTube/Vimeo).</summary>
    public string? VideoId { get; set; }

    /// <summary>Start time in seconds (video block).</summary>
    public double? StartTime { get; set; }

    /// <summary>End time in seconds for video clipping.</summary>
    public double? EndTime { get; set; }

    /// <summary>Optional poster image URL for video blocks.</summary>
    public string? Poster { get; set; }

    /// <summary>Optional chapter markers shown beneath the video player.</summary>
    public List<VideoChapter>? Chapters { get; set; }

    // --- Timeline specific ---

    /// <summary>Chronological events for a timeline block.</summary>
    public List<TimelineEvent>? Events { get; set; }

    // --- Tabs specific ---

    /// <summary>Tab definitions for a tabs block.</summary>
    public List<TabItem>? Tabs { get; set; }

    // --- Stat specific ---

    /// <summary>Stat value (big-number display).</summary>
    public string? Value { get; set; }

    /// <summary>Optional delta indicator (e.g. "+0.5%", "-12").</summary>
    public string? Delta { get; set; }

    /// <summary>Direction of delta: "up", "down", or "flat". Drives color.</summary>
    public string? Trend { get; set; }

    /// <summary>Optional unit suffix shown next to the value (e.g. "%", "req/s").</summary>
    public string? Unit { get; set; }

    /// <summary>Optional sparkline data points for stat blocks.</summary>
    public List<double>? Sparkline { get; set; }

    // --- File specific ---

    /// <summary>File size in bytes for file blocks (formatted for display).</summary>
    public long? FileSize { get; set; }

    /// <summary>MIME type hint for file blocks.</summary>
    public string? MimeType { get; set; }

    // --- Chart specific ---

    /// <summary>Chart variant: "line", "bar", "area", "pie".</summary>
    public string? ChartType { get; set; }

    /// <summary>Chart data series.</summary>
    public List<ChartSeries>? Series { get; set; }

    /// <summary>Labels for the X axis (bar/line/area charts).</summary>
    public List<string>? XAxis { get; set; }

    // --- Diff specific ---

    /// <summary>Diff display mode: "unified" (default) or "split".</summary>
    public string? Mode { get; set; }

    /// <summary>Original filename for diff blocks (left side).</summary>
    public string? OldFilename { get; set; }

    /// <summary>New filename for diff blocks (right side).</summary>
    public string? NewFilename { get; set; }
}

/// <summary>One entry in a link block's links[] array.</summary>
public sealed class LinkItem
{
    public required string Url { get; set; }
    public string? Label { get; set; }
    public string? Body { get; set; }
    public string? Icon { get; set; }
}

/// <summary>One image in a gallery block.</summary>
public sealed class GalleryImage
{
    public required string Url { get; set; }
    public string? Alt { get; set; }
    public string? Caption { get; set; }
    public string? TimestampUrl { get; set; }
    public string? Thumbnail { get; set; }
}

/// <summary>One annotation overlaid on an image (arrow, box, or text marker).</summary>
public sealed class ImageAnnotation
{
    /// <summary>Annotation shape: "arrow", "box", "circle", "text".</summary>
    public required string Shape { get; set; }
    /// <summary>X coordinate as a percentage of image width (0-100).</summary>
    public required double X { get; set; }
    /// <summary>Y coordinate as a percentage of image height (0-100).</summary>
    public required double Y { get; set; }
    /// <summary>Width as a percentage of image width (for box / circle).</summary>
    public double? Width { get; set; }
    /// <summary>Height as a percentage of image height (for box / circle).</summary>
    public double? Height { get; set; }
    /// <summary>Annotation text label.</summary>
    public string? Label { get; set; }
    /// <summary>Annotation level: "info", "warning", "error", "success".</summary>
    public string? Level { get; set; }
}

/// <summary>One annotation on a line of a code block (renders as an inline review-style comment).</summary>
public sealed class CodeAnnotation
{
    /// <summary>1-based line number the annotation attaches to.</summary>
    public required int Line { get; set; }
    /// <summary>Annotation level: "info", "warning", "error", "success".</summary>
    public string? Level { get; set; }
    /// <summary>Annotation body (markdown allowed).</summary>
    public required string Body { get; set; }
    /// <summary>Optional author/source label (e.g. "ai-reviewer").</summary>
    public string? Author { get; set; }
}

/// <summary>One chapter marker on a video block.</summary>
public sealed class VideoChapter
{
    /// <summary>Chapter start time in seconds.</summary>
    public required double At { get; set; }
    /// <summary>Chapter label.</summary>
    public required string Label { get; set; }
}

/// <summary>One event on a timeline block.</summary>
public sealed class TimelineEvent
{
    /// <summary>Display timestamp (free-form string; renders verbatim).</summary>
    public required string At { get; set; }
    /// <summary>Short event label / title.</summary>
    public required string Label { get; set; }
    /// <summary>Optional event body (markdown allowed).</summary>
    public string? Body { get; set; }
    /// <summary>Event level: "info" (default), "warning", "error", "success".</summary>
    public string? Level { get; set; }
    /// <summary>Optional icon name (Lucide).</summary>
    public string? Icon { get; set; }
}

/// <summary>One tab in a tabs block.</summary>
public sealed class TabItem
{
    /// <summary>Tab label.</summary>
    public required string Label { get; set; }
    /// <summary>Tab content (nested blocks).</summary>
    [JsonPropertyName("content")]
    public List<ContentBlock>? Children { get; set; }
    /// <summary>Optional badge text on the tab.</summary>
    public string? Badge { get; set; }
}

/// <summary>One data series on a chart block.</summary>
public sealed class ChartSeries
{
    /// <summary>Series name (legend label).</summary>
    public required string Name { get; set; }
    /// <summary>Numeric data points.</summary>
    public required List<double> Data { get; set; }
    /// <summary>Optional color override (CSS color).</summary>
    public string? Color { get; set; }
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
    Image,
    Diff,
    Video,
    Gallery,
    Timeline,
    Tabs,
    Stat,
    File,
    Chart,
    Diagram,
    BeforeAfter
}

public enum AlertLevel
{
    Info,
    Warning,
    Error,
    Success
}
