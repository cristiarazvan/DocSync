namespace GoogDocsLite.Server.Contracts.Documents;

public class RealtimeOperationDto
{
    public long Revision { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string DeltaJson { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string ClientOpId { get; init; } = string.Empty;
}
