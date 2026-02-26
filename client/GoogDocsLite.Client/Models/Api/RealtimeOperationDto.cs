namespace GoogDocsLite.Client.Models.Api;

public class RealtimeOperationDto
{
    public long Revision { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DeltaJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string ClientOpId { get; set; } = string.Empty;
}
