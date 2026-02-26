namespace GoogDocsLite.Server.Contracts.Documents;

public class LiveContentPatchResponseDto
{
    public Guid DocumentId { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public long LiveRevision { get; init; }
}
