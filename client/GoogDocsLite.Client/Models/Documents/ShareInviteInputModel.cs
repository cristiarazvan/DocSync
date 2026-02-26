using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Client.Models.Documents;

public class ShareInviteInputModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string InviteeEmail { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Viewer|Editor)$", ErrorMessage = "Role must be Viewer or Editor.")]
    public string Role { get; set; } = "Viewer";
}
