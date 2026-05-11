namespace InventorySync.Server.Models;

/// <summary>
/// Tracks the two connection IDs paired under a single GUID.
/// The server stores this only in memory — nothing is persisted.
/// If the server restarts, clients reconnect and re-pair automatically.
/// </summary>
public sealed class PairSession
{
    /// <summary>The pairing GUID both clients agreed on.</summary>
    public string PairCode { get; init; } = string.Empty;

    /// <summary>SignalR connection IDs of the two paired clients. Max 2.</summary>
    private readonly List<string> _connectionIds = new(2);

    public IReadOnlyList<string> ConnectionIds => _connectionIds;

    /// <summary>True once both clients have joined.</summary>
    public bool IsFull => _connectionIds.Count >= 2;

    /// <summary>
    /// Attempts to add a connection. Returns false if the session is already full.
    /// </summary>
    public bool TryAdd(string connectionId)
    {
        if (IsFull) return false;
        _connectionIds.Add(connectionId);
        return true;
    }

    /// <summary>Removes a connection (called on disconnect).</summary>
    public void Remove(string connectionId) =>
        _connectionIds.Remove(connectionId);

    /// <summary>True if the session has no remaining connections.</summary>
    public bool IsEmpty => _connectionIds.Count == 0;

    /// <summary>
    /// Returns the other connection ID in the pair, or null if not yet paired.
    /// </summary>
    public string? GetPartner(string connectionId) =>
        _connectionIds.FirstOrDefault(id => id != connectionId);
}
