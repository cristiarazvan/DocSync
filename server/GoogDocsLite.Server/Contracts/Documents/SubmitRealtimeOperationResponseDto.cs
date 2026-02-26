namespace GoogDocsLite.Server.Contracts.Documents;

public class SubmitRealtimeOperationResponseDto
{
    public long AcceptedRevision { get; init; }
    public string TransformedDeltaJson { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; }
    public string AcceptedByUserId { get; init; } = string.Empty;
}
