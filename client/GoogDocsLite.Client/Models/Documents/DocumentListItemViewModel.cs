namespace GoogDocsLite.Client.Models.Documents;

public class DocumentListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string AccessRole { get; set; } = "Owner";
    public bool IsOwner { get; set; }
}
