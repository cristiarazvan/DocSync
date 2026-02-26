namespace GoogDocsLite.Server.Contracts.Invites;

public class IncomingInviteDto
{
    public Guid InviteId { get; init; }
    public Guid DocumentId { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string Role { get; init; } = "Viewer";
    public string Status { get; init; } = "Pending";
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
}
