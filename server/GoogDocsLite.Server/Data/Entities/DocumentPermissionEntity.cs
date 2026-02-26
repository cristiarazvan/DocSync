using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Data.Entities;

public class DocumentPermissionEntity
{
    public Guid Id { get; set; }

    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public DocumentShareRole Role { get; set; }

    [Required]
    public string GrantedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DocumentEntity? Document { get; set; }
}
