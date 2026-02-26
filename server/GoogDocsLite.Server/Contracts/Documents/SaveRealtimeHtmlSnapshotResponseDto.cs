namespace GoogDocsLite.Server.Contracts.Documents;

public class SaveRealtimeHtmlSnapshotResponseDto
{
    public long Revision { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
