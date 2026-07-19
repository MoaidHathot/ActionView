namespace ActionView.Core.Models;

/// <summary>
/// An append-only audit record of a single action execution (or lifecycle
/// event) against an entry. Written to the JSON-Lines action log so the full
/// history of what happened to an entry survives archive/dismiss/delete.
///
/// Unlike <see cref="EntryOutcome"/> (a single terminal record stored on the
/// entry when it is archived), an <see cref="ActionEvent"/> is emitted for
/// EVERY execution — including <c>onSuccess: keep</c> actions, section actions,
/// batch actions, dismiss, and delete — none of which were previously recorded.
/// </summary>
public sealed class ActionEvent
{
    /// <summary>Unique id for this event.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>When the action was executed.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The entry this event belongs to.</summary>
    public required string EntryId { get; set; }

    /// <summary>Entry title at the time of execution (denormalized so the log is self-contained even after delete).</summary>
    public string? EntryTitle { get; set; }

    /// <summary>Button label that was clicked (e.g. "Approve", "Submit Review", "Dismissed").</summary>
    public required string ActionLabel { get; set; }

    /// <summary>Visual style of the action, carried so the UI can render a matching outcome chip.</summary>
    public ActionStyle ActionStyle { get; set; } = ActionStyle.Default;

    /// <summary>Where the action lived: entry | section | system.</summary>
    public string Target { get; set; } = "entry";

    /// <summary>
    /// For section actions: the positional block path from entry content root to
    /// the section that owns the action (indices into content/children arrays).
    /// Null for entry-level and system events.
    /// </summary>
    public List<int>? Path { get; set; }

    /// <summary>
    /// Optional stable target id (the owning block's <see cref="ContentBlock.Id"/>)
    /// so outcome markers can key by id instead of position.
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>How the execution was triggered: click | batch | undo | dismiss | delete.</summary>
    public string Trigger { get; set; } = "click";

    /// <summary>Redacted summary of the command that ran (never includes headers, body, or resolved secrets).</summary>
    public ActionCommandInfo? Command { get; set; }

    /// <summary>Whether the command/operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>HTTP status code or process exit code, when applicable.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Result or error message.</summary>
    public string? Message { get; set; }

    /// <summary>Captured stdout/stderr or response body (already truncated by the executor).</summary>
    public string? Output { get; set; }

    /// <summary>What happened to the entry after a successful action (archive/keep/delete), when applicable.</summary>
    public PostActionBehavior? PostBehavior { get; set; }
}

/// <summary>
/// A redacted, human-readable summary of an <see cref="ActionCommand"/> for the
/// audit log and the "what will this do?" UI preview. Deliberately omits HTTP
/// headers and body and never resolves <c>{{SECRET}}</c> placeholders, so no
/// credentials are ever written to disk or shown in the browser.
/// </summary>
public sealed class ActionCommandInfo
{
    /// <summary>Command type: cli | http.</summary>
    public required string Type { get; set; }

    // --- CLI ---
    public string? Program { get; set; }
    public List<string>? Args { get; set; }

    // --- HTTP ---
    public string? Method { get; set; }
    public string? Url { get; set; }

    /// <summary>Builds a redacted summary from an <see cref="ActionCommand"/>.</summary>
    public static ActionCommandInfo From(ActionCommand command) => new()
    {
        Type = command.Type == CommandType.Cli ? "cli" : "http",
        Program = command.Program,
        Args = command.Args is null ? null : new List<string>(command.Args),
        Method = command.Method,
        Url = command.Url,
    };
}
