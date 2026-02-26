namespace GoogDocsLite.Client.Models.Documents;

public class DocumentEditorViewModel
{
    public SaveDocumentInputModel Form { get; set; } = new();
    public ShareInviteInputModel ShareForm { get; set; } = new();
    public List<DocumentShareItemViewModel> Shares { get; set; } = [];
    public DocumentLockViewModel CurrentLock { get; set; } = new();

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool NotFound { get; set; }
    public string AccessRole { get; set; } = "Viewer";
    public bool IsOwner { get; set; }
    public long CurrentRevision { get; set; }
    public string ContentDeltaJson { get; set; } = "{\"ops\":[]}";
    public string CurrentUserId { get; set; } = string.Empty;
    public string CurrentUserDisplayName { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? InfoMessage { get; set; }

    public bool CanEditByRole => AccessRole is "Owner" or "Editor";
    public bool CanManageShares => IsOwner;
}
