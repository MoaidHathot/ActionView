using ActionView.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace ActionView.Api.Hubs;

/// <summary>
/// SignalR hub for real-time entry notifications.
/// Clients connect to receive updates when entries are added, updated, or archived.
/// </summary>
public sealed class EntryHub : Hub<IEntryHubClient>
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Typed interface for sending messages to connected clients.
/// </summary>
public interface IEntryHubClient
{
    Task EntriesAdded(List<Entry> entries);
    Task EntryUpdated(Entry entry);
    Task EntryArchived(Entry entry);
    Task EntryDeleted(string entryId);

    /// <summary>
    /// Signals that the server hot-reloaded runtime-safe config slices
    /// (views / tag-match default / notifications / secrets) from
    /// actionview.json. Clients re-fetch the slices they mirror (views + config).
    /// </summary>
    Task ConfigChanged();

    /// <summary>A background action job started running.</summary>
    Task ActionJobStarted(ActionJob job);

    /// <summary>A streamed output line from a running action job.</summary>
    Task ActionJobProgress(string jobId, string line);

    /// <summary>A background action job reached a terminal state (succeeded/failed/cancelled).</summary>
    Task ActionJobFinished(ActionJob job);
}
