namespace GoogDocsLite.Server.Contracts.Documents;

public class RealtimeStateDto
{
    public Guid DocumentId { get; init; }
    public long Revision { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? ContentDeltaJson { get; init; }
    public string ContentHtml { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; }
    public string AccessRole { get; init; } = "Viewer";
}
