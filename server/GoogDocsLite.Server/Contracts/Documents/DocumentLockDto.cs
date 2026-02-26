namespace GoogDocsLite.Server.Contracts.Documents;

public class DocumentLockDto
{
    public Guid DocumentId { get; init; }
    public bool IsLocked { get; init; }
    public string? LockOwnerUserId { get; init; }
    public string? LockOwnerDisplayName { get; init; }
    public DateTime? AcquiredAtUtc { get; init; }
    public DateTime? LastHeartbeatAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public bool IsOwnedByCurrentUser { get; init; }
}
