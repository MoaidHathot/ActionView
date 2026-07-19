namespace ActionView.Core.Models;

/// <summary>
/// A background execution of an action command. Actions run asynchronously as
/// jobs so long-running commands don't block the request or the UI: the run is
/// started, progress (streamed CLI output) is pushed over SignalR, and the job
/// finishes as succeeded / failed / cancelled.
/// </summary>
public sealed class ActionJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public required string EntryId { get; set; }
    public string? EntryTitle { get; set; }

    public required string ActionLabel { get; set; }
    public ActionStyle ActionStyle { get; set; } = ActionStyle.Default;

    /// <summary>Where the action lived: entry | section.</summary>
    public string Target { get; set; } = "entry";

    /// <summary>Positional block path for a section action (null for entry actions).</summary>
    public List<int>? Path { get; set; }

    /// <summary>Owning block's stable id, when set.</summary>
    public string? TargetId { get; set; }

    /// <summary>Redacted summary of the command that ran (for the UI + audit; never secrets).</summary>
    public ActionCommandInfo? Command { get; set; }

    /// <summary>How the run was triggered (click | batch | undo).</summary>
    public string Trigger { get; set; } = "click";

    public ActionJobStatus Status { get; set; } = ActionJobStatus.Pending;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public long? DurationMs { get; set; }

    /// <summary>HTTP status code or process exit code once finished.</summary>
    public int? ExitCode { get; set; }

    /// <summary>Result or error message once finished.</summary>
    public string? Message { get; set; }

    /// <summary>Rolling tail of streamed output (bounded).</summary>
    public List<string> OutputTail { get; set; } = [];

    /// <summary>What happens to the entry after the job succeeds.</summary>
    public PostActionBehavior PostBehavior { get; set; } = PostActionBehavior.Keep;
}

public enum ActionJobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled
}
