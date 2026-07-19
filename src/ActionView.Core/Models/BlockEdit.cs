namespace ActionView.Core.Models;

/// <summary>
/// Edit provenance for a <see cref="ContentBlock"/>. Captured the first time a
/// block's text is edited from the dashboard so the original can be shown
/// (diff) and restored (revert). <see cref="ContentBlock.Body"/> holds the
/// current text; <see cref="OriginalText"/> holds the text before the first edit.
/// </summary>
public sealed class BlockEdit
{
    /// <summary>The block's text before the first dashboard edit.</summary>
    public required string OriginalText { get; set; }

    /// <summary>When the block was first edited.</summary>
    public DateTimeOffset FirstEditedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the block was most recently edited.</summary>
    public DateTimeOffset LastEditedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Number of times the block has been edited.</summary>
    public int Count { get; set; } = 1;
}
