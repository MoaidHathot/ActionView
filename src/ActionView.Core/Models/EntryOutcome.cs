namespace ActionView.Core.Models;

/// <summary>
/// Records what happened to an entry when it was archived.
/// </summary>
public sealed class EntryOutcome
{
    /// <summary>The action that was taken (e.g., "Approve PR", "dismissed", "deleted").</summary>
    public required string Action { get; set; }

    /// <summary>When the action was taken.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Whether the action command executed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Optional result message or error details.</summary>
    public string? ResultMessage { get; set; }
}
