namespace ActionView.Core.Models;

/// <summary>
/// An action that can be executed by the user on an entry or section.
/// The orchestration tool defines what the button does via the Command.
/// </summary>
public sealed class EntryAction
{
    /// <summary>Button label text.</summary>
    public required string Label { get; set; }

    /// <summary>Visual style of the button.</summary>
    public ActionStyle Style { get; set; } = ActionStyle.Default;

    /// <summary>Optional confirmation message shown before executing.</summary>
    public string? ConfirmMessage { get; set; }

    /// <summary>The command to execute when the user clicks this action.</summary>
    public required ActionCommand Command { get; set; }

    /// <summary>What to do with the entry after the action succeeds.</summary>
    public PostActionBehavior OnSuccess { get; set; } = PostActionBehavior.Archive;

    /// <summary>Optional undo command. If set, the UI shows an undo button after action execution.</summary>
    public ActionCommand? UndoCommand { get; set; }

    /// <summary>Seconds the undo option remains available (default 10). Global default can be set in config.</summary>
    public int? UndoWindowSeconds { get; set; }
}

public enum ActionStyle
{
    Default,
    Primary,
    Success,
    Danger
}

public enum PostActionBehavior
{
    /// <summary>Move the entry to archive.</summary>
    Archive,

    /// <summary>Keep the entry in the active list.</summary>
    Keep,

    /// <summary>Permanently delete the entry.</summary>
    Delete
}
