using System.Collections.Concurrent;
using InventorySync.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace InventorySync.Server.Hubs;

/// <summary>
/// SignalR hub — the entire server logic lives here.
///
/// Protocol (client → server):
///   JoinPair(pairCode)           — client presents their GUID, joins or creates a session
///   SendSnapshot(snapshotJson)   — client pushes their inventory snapshot
///   LeavePair()                  — client explicitly leaves (also called on disconnect)
///
/// Protocol (server → client):
///   PartnerJoined()              — fired when the second client connects
///   PartnerLeft()                — fired when the partner disconnects
///   ReceiveSnapshot(json)        — delivers the partner's snapshot
///   PairFull()                   — fired if a third client tries to join a full session
/// </summary>
public sealed class InventoryHub : Hub
{
    // pairCode → session
    private static readonly ConcurrentDictionary<string, PairSession> Sessions = new();

    // connectionId → pairCode (for fast lookup on disconnect)
    private static readonly ConcurrentDictionary<string, string> ConnectionPairs = new();

    // ── Client → Server ───────────────────────────────────────────────────────

    /// <summary>
    /// Client calls this with their pairing GUID.
    /// - First caller creates the session and waits.
    /// - Second caller completes the pair and both are notified.
    /// - Third+ callers are rejected.
    /// </summary>
    public async Task JoinPair(string pairCode)
    {
        if (string.IsNullOrWhiteSpace(pairCode)) return;

        // Normalize to lowercase for case-insensitive matching
        pairCode = pairCode.Trim().ToLowerInvariant();

        var session = Sessions.GetOrAdd(pairCode, code => new PairSession { PairCode = code });

        if (!session.TryAdd(Context.ConnectionId))
        {
            // Session already has 2 clients
            await Clients.Caller.SendAsync("PairFull");
            return;
        }

        ConnectionPairs[Context.ConnectionId] = pairCode;

        // Join a SignalR group so we can broadcast between the two
        await Groups.AddToGroupAsync(Context.ConnectionId, pairCode);

        if (session.IsFull)
        {
            // Both clients are now connected — notify both
            await Clients.Group(pairCode).SendAsync("PartnerJoined");
        }
        // else: first client just waits silently for the partner
    }

    /// <summary>
    /// Client pushes their inventory snapshot JSON.
    /// Server forwards it only to their paired partner — never broadcasts.
    /// </summary>
    public async Task SendSnapshot(string snapshotJson)
    {
        if (!ConnectionPairs.TryGetValue(Context.ConnectionId, out var pairCode)) return;
        if (!Sessions.TryGetValue(pairCode, out var session)) return;

        var partnerId = session.GetPartner(Context.ConnectionId);
        if (partnerId == null) return;

        await Clients.Client(partnerId).SendAsync("ReceiveSnapshot", snapshotJson);
    }

    /// <summary>Client explicitly leaves the pair.</summary>
    public async Task LeavePair()
    {
        await CleanupConnection(Context.ConnectionId);
    }

    // ── Connection lifecycle ──────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await CleanupConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task CleanupConnection(string connectionId)
    {
        if (!ConnectionPairs.TryRemove(connectionId, out var pairCode)) return;
        if (!Sessions.TryGetValue(pairCode, out var session)) return;

        var partnerId = session.GetPartner(connectionId);

        session.Remove(connectionId);
        await Groups.RemoveFromGroupAsync(connectionId, pairCode);

        if (session.IsEmpty)
        {
            // Last client left — remove the session entirely
            Sessions.TryRemove(pairCode, out _);
        }
        else if (partnerId != null)
        {
            // Notify the remaining partner
            await Clients.Client(partnerId).SendAsync("PartnerLeft");
        }
    }
}
