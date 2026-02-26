namespace GoogDocsLite.Server.Contracts.Documents;

public class DocumentShareItemDto
{
    public Guid Id { get; init; }
    public string ShareType { get; init; } = string.Empty; // permission | invite
    public string Subject { get; init; } = string.Empty; // user id sau email
    public string Role { get; init; } = "Viewer";
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? AcceptedAtUtc { get; init; }
}
