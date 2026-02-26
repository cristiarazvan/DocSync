using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Contracts.Documents;

public class CreateShareRequest
{
    [Required]
    [EmailAddress]
    public string InviteeEmail { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^(Viewer|Editor)$", ErrorMessage = "Role must be Viewer or Editor.")]
    public string Role { get; init; } = "Viewer";
}
