namespace GoogDocsLite.Client.Models.Api;

public class SubmitRealtimeOperationResponseDto
{
    public long AcceptedRevision { get; set; }
    public string TransformedDeltaJson { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public string AcceptedByUserId { get; set; } = string.Empty;
}
