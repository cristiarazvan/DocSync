namespace GoogDocsLite.Client.Models.Api;

public class LiveContentPatchResponseDto
{
    public Guid DocumentId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public long LiveRevision { get; set; }
}
