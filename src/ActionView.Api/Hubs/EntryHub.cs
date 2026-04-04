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
}
