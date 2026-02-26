namespace GoogDocsLite.Client.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    // Arata RequestId in UI doar daca exista o valoare valida.
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
