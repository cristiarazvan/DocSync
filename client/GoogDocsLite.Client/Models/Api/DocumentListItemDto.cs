namespace GoogDocsLite.Client.Models.Api;

public class DocumentListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string AccessRole { get; set; } = "Owner";
    public bool IsOwner { get; set; }
}
