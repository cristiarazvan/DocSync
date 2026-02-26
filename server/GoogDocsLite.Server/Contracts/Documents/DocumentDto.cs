namespace GoogDocsLite.Server.Contracts.Documents;

public class DocumentDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? ContentDeltaJson { get; init; }
    public long LiveRevision { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string AccessRole { get; init; } = "Owner";
    public bool IsOwner { get; init; }
}
