using GoogDocsLite.Shared;
using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Contracts.Documents;

public class CreateDocumentRequest
{
    [Required]
    [MaxLength(AppDefaults.DocumentTitleMaxLength)]
    public string Title { get; init; } = string.Empty;
}
