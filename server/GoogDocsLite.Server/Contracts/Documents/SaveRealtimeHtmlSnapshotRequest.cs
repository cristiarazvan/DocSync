using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Contracts.Documents;

public class SaveRealtimeHtmlSnapshotRequest
{
    public long Revision { get; init; }

    [Required]
    public string ContentHtml { get; init; } = string.Empty;
}
