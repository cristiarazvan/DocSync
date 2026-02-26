namespace GoogDocsLite.Client.Models.Api;

public class RealtimeStateDto
{
    public Guid DocumentId { get; set; }
    public long Revision { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ContentDeltaJson { get; set; }
    public string ContentHtml { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public string AccessRole { get; set; } = "Viewer";
}
