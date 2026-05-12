namespace ActionView.Core.Models;

/// <summary>
/// Partial-update payload for an existing active entry. Used by the HTTP
/// <c>PUT /api/entries/{id}</c> endpoint and the MCP <c>update_entry</c> tool.
///
/// Semantics: each field is optional. <c>null</c> (or "missing in the payload")
/// means "leave alone". Identity / audit fields (<c>id</c>, <c>type</c>,
/// <c>source</c>, <c>createdAt</c>) are intentionally not updatable.
/// </summary>
public sealed class EntryUpdateRequest
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public Severity? Severity { get; set; }
    public List<string>? Tags { get; set; }
    public List<ContentBlock>? Content { get; set; }
    public List<EntryAction>? Actions { get; set; }
    public int? Priority { get; set; }
}
