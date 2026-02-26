using GoogDocsLite.Shared;
using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Contracts.Documents;

public class UpdateDocumentRequest
{
    [Required]
    [MaxLength(AppDefaults.DocumentTitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}
