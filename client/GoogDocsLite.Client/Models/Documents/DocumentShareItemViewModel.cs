namespace GoogDocsLite.Client.Models.Documents;

public class DocumentShareItemViewModel
{
    public Guid Id { get; set; }
    public string ShareType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
}
