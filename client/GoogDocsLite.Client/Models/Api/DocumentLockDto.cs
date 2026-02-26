namespace GoogDocsLite.Client.Models.Api;

public class DocumentLockDto
{
    public Guid DocumentId { get; set; }
    public bool IsLocked { get; set; }
    public string? LockOwnerUserId { get; set; }
    public string? LockOwnerDisplayName { get; set; }
    public DateTime? AcquiredAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsOwnedByCurrentUser { get; set; }
}
