using GoogDocsLite.Shared;
using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Client.Models.Documents;

public class SaveDocumentInputModel
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(AppDefaults.DocumentTitleMaxLength, ErrorMessage = "Title must be 120 characters or fewer.")]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
