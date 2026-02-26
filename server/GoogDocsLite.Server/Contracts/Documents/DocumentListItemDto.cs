namespace GoogDocsLite.Server.Contracts.Documents;

public class DocumentListItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
    public string AccessRole { get; init; } = "Owner";
    public bool IsOwner { get; init; }
}
