using System.ComponentModel.DataAnnotations;
using GoogDocsLite.Shared;

namespace GoogDocsLite.Server.Data.Entities;

public class DocumentEntity
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(AppDefaults.DocumentTitleMaxLength)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    // Delta Quill curent (modelul de operatii pentru realtime OT).
    public string? ContentDeltaJson { get; set; }

    // Revizia live curenta a documentului (creste la fiecare patch/op acceptat).
    public long LiveRevision { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [Required]
    public string OwnerUserId { get; set; } = string.Empty;

    public List<DocumentPermissionEntity> Permissions { get; set; } = [];

    public List<DocumentInviteEntity> Invites { get; set; } = [];

    public DocumentEditLockEntity? EditLock { get; set; }

    public List<DocumentOperationEntity> Operations { get; set; } = [];
}
