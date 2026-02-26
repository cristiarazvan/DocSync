namespace GoogDocsLite.Client.Models.Documents;

public class IncomingInviteViewModel
{
    public Guid InviteId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
}
