namespace GoogDocsLite.Client.Hubs;

public class DocumentPresenceTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<string, PresenceConnection> _connectionMap = [];
    private readonly Dictionary<Guid, Dictionary<string, DocumentPresenceUser>> _documentConnections = [];

    // Marcheaza faptul ca o conexiune intra pe un document.
    public Guid? Join(Guid documentId, string connectionId, string userId, string displayName)
    {
        lock (_sync)
        {
            Guid? previousDocumentId = null;

            if (_connectionMap.TryGetValue(connectionId, out var existing))
            {
                previousDocumentId = existing.DocumentId;
                RemoveConnectionInternal(existing.DocumentId, connectionId);
            }

            _connectionMap[connectionId] = new PresenceConnection
            {
                ConnectionId = connectionId,
                DocumentId = documentId,
                UserId = userId,
                DisplayName = displayName
            };

            if (!_documentConnections.TryGetValue(documentId, out var connections))
            {
                connections = [];
                _documentConnections[documentId] = connections;
            }

            connections[connectionId] = new DocumentPresenceUser
            {
                UserId = userId,
                DisplayName = displayName
            };

            return previousDocumentId;
        }
    }

    // Scoate conexiunea din documentul curent.
    public bool Leave(Guid documentId, string connectionId)
    {
        lock (_sync)
        {
            if (_connectionMap.TryGetValue(connectionId, out var existing) && existing.DocumentId == documentId)
            {
                _connectionMap.Remove(connectionId);
            }

            return RemoveConnectionInternal(documentId, connectionId);
        }
    }

    // Scoate conexiunea complet (ex. disconnect) si intoarce documentele afectate.
    public IReadOnlyCollection<Guid> RemoveConnection(string connectionId)
    {
        lock (_sync)
        {
            if (!_connectionMap.TryGetValue(connectionId, out var existing))
            {
                return [];
            }

            _connectionMap.Remove(connectionId);
            var removed = RemoveConnectionInternal(existing.DocumentId, connectionId);

            return removed ? [existing.DocumentId] : [];
        }
    }

    // Returneaza utilizatorii prezenti pe document (fara duplicate pe acelasi user id).
    public IReadOnlyList<DocumentPresenceUser> GetUsers(Guid documentId)
    {
        lock (_sync)
        {
            if (!_documentConnections.TryGetValue(documentId, out var connections) || connections.Count == 0)
            {
                return [];
            }

            return connections.Values
                .GroupBy(x => x.UserId)
                .Select(g => g.First())
                .OrderBy(x => x.DisplayName)
                .ToList();
        }
    }

    // Scoate intern conexiunea din mapa documentului.
    private bool RemoveConnectionInternal(Guid documentId, string connectionId)
    {
        if (!_documentConnections.TryGetValue(documentId, out var connections))
        {
            return false;
        }

        var removed = connections.Remove(connectionId);
        if (connections.Count == 0)
        {
            _documentConnections.Remove(documentId);
        }

        return removed;
    }

    private sealed class PresenceConnection
    {
        public required string ConnectionId { get; init; }
        public required Guid DocumentId { get; init; }
        public required string UserId { get; init; }
        public required string DisplayName { get; init; }
    }
}
