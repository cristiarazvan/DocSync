namespace GoogDocsLite.Client.Models.Documents;

public class InvitesIndexViewModel
{
    public List<IncomingInviteViewModel> Invites { get; set; } = [];
    public string? InfoMessage { get; set; }
    public string? ErrorMessage { get; set; }
}
