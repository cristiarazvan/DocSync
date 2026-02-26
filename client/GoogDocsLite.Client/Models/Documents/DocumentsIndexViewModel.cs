namespace GoogDocsLite.Client.Models.Documents;

public class DocumentsIndexViewModel
{
    public string ActiveView { get; set; } = "all";
    public List<DocumentListItemViewModel> Documents { get; set; } = [];
    public CreateDocumentInputModel CreateForm { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }
    public int IncomingInvitesCount { get; set; }
    public int SyncedInvitesCount { get; set; }
}
