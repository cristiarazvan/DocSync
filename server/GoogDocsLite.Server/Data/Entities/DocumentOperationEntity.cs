namespace GoogDocsLite.Server.Data.Entities;

public class DocumentOperationEntity
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public long Revision { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ClientOpId { get; set; } = string.Empty;
    public string DeltaJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public DocumentEntity? Document { get; set; }
}
