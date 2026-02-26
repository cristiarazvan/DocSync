using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Contracts.Documents;

public class SubmitRealtimeOperationRequest
{
    public long BaseRevision { get; init; }

    [Required]
    public string ClientOpId { get; init; } = string.Empty;

    [Required]
    public string DeltaJson { get; init; } = string.Empty;
}
