using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Data.Entities;

public class DocumentInviteEntity
{
    public Guid Id { get; set; }

    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    public string InviteeEmailNormalized { get; set; } = string.Empty;

    [Required]
    public DocumentShareRole Role { get; set; }

    [Required]
    public DocumentInviteStatus Status { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string? AcceptedByUserId { get; set; }

    public DateTime? AcceptedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DocumentEntity? Document { get; set; }
}
