namespace Amanah.Api.Services.Chats;

public sealed class ChatPresenceTracker
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, (Guid UserId, Guid ThreadId)> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid UserId, Guid ThreadId), HashSet<string>> _viewers = [];

    public void Join(string connectionId, Guid userId, Guid threadId)
    {
        lock (_sync)
        {
            if (_connections.TryGetValue(connectionId, out var existing)
                && existing.ThreadId != threadId)
            {
                RemoveViewerLocked(connectionId, existing.UserId, existing.ThreadId);
            }

            _connections[connectionId] = (userId, threadId);

            var key = (userId, threadId);
            if (!_viewers.TryGetValue(key, out var connections))
            {
                connections = new HashSet<string>(StringComparer.Ordinal);
                _viewers[key] = connections;
            }

            connections.Add(connectionId);
        }
    }

    public void Leave(string connectionId, Guid threadId)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var existing)
                || existing.ThreadId != threadId)
            {
                return;
            }

            RemoveViewerLocked(connectionId, existing.UserId, threadId);
            _connections.Remove(connectionId);
        }
    }

    public void RemoveConnection(string connectionId)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var existing))
            {
                return;
            }

            RemoveViewerLocked(connectionId, existing.UserId, existing.ThreadId);
            _connections.Remove(connectionId);
        }
    }

    public bool IsViewing(Guid userId, Guid threadId)
    {
        lock (_sync)
        {
            return _viewers.TryGetValue((userId, threadId), out var connections)
                && connections.Count > 0;
        }
    }

    private void RemoveViewerLocked(string connectionId, Guid userId, Guid threadId)
    {
        var key = (userId, threadId);
        if (!_viewers.TryGetValue(key, out var connections))
        {
            return;
        }

        connections.Remove(connectionId);
        if (connections.Count == 0)
        {
            _viewers.Remove(key);
        }
    }
}
