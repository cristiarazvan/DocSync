namespace GoogDocsLite.Client.Models.Api;

public class DocumentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ContentDeltaJson { get; set; }
    public long LiveRevision { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string AccessRole { get; set; } = "Owner";
    public bool IsOwner { get; set; }
}
