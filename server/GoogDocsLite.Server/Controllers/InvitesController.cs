using GoogDocsLite.Server.Application.Services;
using GoogDocsLite.Server.Contracts.Invites;
using Microsoft.AspNetCore.Mvc;

namespace GoogDocsLite.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitesController(
    IDocumentService documentService,
    IConfiguration configuration) : InternalApiControllerBase(configuration)
{
    // GET /api/invites/incoming - lista invitatiilor pending pentru user-ul logat.
    [HttpGet("incoming")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<IncomingInviteDto>>> Incoming(CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext, requireEmail: true))
        {
            return Unauthorized(new { message = "Missing user context/email." });
        }

        var invites = await documentService.GetIncomingInvitesAsync(userContext.UserId, userContext.Email!, cancellationToken);
        return Ok(invites);
    }

    // POST /api/invites/{inviteId}/accept - accepta invitatia.
    [HttpPost("{inviteId:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Accept(Guid inviteId, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext, requireEmail: true))
        {
            return Unauthorized(new { message = "Missing user context/email." });
        }

        var result = await documentService.AcceptInviteAsync(userContext.UserId, userContext.Email!, inviteId, cancellationToken);
        if (result.Type == ServiceResultType.NotFound)
        {
            return NotFound();
        }

        if (result.Type == ServiceResultType.ValidationError)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Invite is invalid." });
        }

        return NoContent();
    }

    // POST /api/invites/{inviteId}/decline - refuza invitatia.
    [HttpPost("{inviteId:guid}/decline")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Decline(Guid inviteId, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext, requireEmail: true))
        {
            return Unauthorized(new { message = "Missing user context/email." });
        }

        var result = await documentService.DeclineInviteAsync(userContext.UserId, userContext.Email!, inviteId, cancellationToken);
        if (result.Type == ServiceResultType.NotFound)
        {
            return NotFound();
        }

        if (result.Type == ServiceResultType.ValidationError)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Invite is invalid." });
        }

        return NoContent();
    }

    // POST /api/invites/sync-pending - transforma invitatiile pending in access activ.
    [HttpPost("sync-pending")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SyncPendingInvitesResponse>> SyncPending(CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext, requireEmail: true))
        {
            return Unauthorized(new { message = "Missing user context/email." });
        }

        var syncedCount = await documentService.SyncPendingInvitesAsync(userContext.UserId, userContext.Email!, cancellationToken);
        return Ok(new SyncPendingInvitesResponse { SyncedCount = syncedCount });
    }
}
