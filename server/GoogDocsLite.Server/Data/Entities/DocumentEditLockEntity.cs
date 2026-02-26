using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Data.Entities;

public class DocumentEditLockEntity
{
    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    public string LockOwnerUserId { get; set; } = string.Empty;

    [Required]
    public string LockOwnerDisplayName { get; set; } = string.Empty;

    public DateTime AcquiredAtUtc { get; set; }

    public DateTime LastHeartbeatAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DocumentEntity? Document { get; set; }
}
