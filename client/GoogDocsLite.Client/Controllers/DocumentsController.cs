using System.Net;
using System.Security.Claims;
using GoogDocsLite.Client.Hubs;
using GoogDocsLite.Client.Models.Api;
using GoogDocsLite.Client.Models.Documents;
using GoogDocsLite.Client.Services;
using GoogDocsLite.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace GoogDocsLite.Client.Controllers;

[Authorize]
public class DocumentsController(
    DocumentsApiClient documentsApiClient,
    IHubContext<DocumentCollabHub> hubContext,
    ILogger<DocumentsController> logger) : Controller
{
    // Afiseaza lista de documente filtrata pe owned/shared/all.
    [HttpGet("/docs")]
    public async Task<IActionResult> Index([FromQuery] string view = "all", CancellationToken cancellationToken = default)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        var syncedCount = await TrySyncPendingInvitesAsync(userContext, cancellationToken);
        var viewModel = await BuildIndexViewModelAsync(userContext, view, syncedCount, cancellationToken);
        return View(viewModel);
    }

    // Creeaza document nou din dashboard.
    [HttpPost("/docs/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "CreateForm")] CreateDocumentInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            var syncedCount = await TrySyncPendingInvitesAsync(userContext, cancellationToken);
            var invalidModel = await BuildIndexViewModelAsync(userContext, "all", syncedCount, cancellationToken);
            invalidModel.CreateForm = input;
            invalidModel.ErrorMessage = "Please fix the validation errors and try again.";
            return View("Index", invalidModel);
        }

        try
        {
            var created = await documentsApiClient.CreateAsync(userContext, input.Title.Trim(), cancellationToken);
            return RedirectToAction(nameof(Edit), new { id = created.Id });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create document.");
            var syncedCount = await TrySyncPendingInvitesAsync(userContext, cancellationToken);
            var viewModel = await BuildIndexViewModelAsync(userContext, "all", syncedCount, cancellationToken);
            viewModel.CreateForm = input;
            viewModel.ErrorMessage = ex.Message;
            return View("Index", viewModel);
        }
    }

    // Deschide editorul pentru documentul ales.
    [HttpGet("/docs/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        await TrySyncPendingInvitesAsync(userContext, cancellationToken);

        var viewModel = await BuildEditorViewModelAsync(userContext, id, cancellationToken);
        return View(viewModel);
    }

    // Salveaza manual documentul (titlu + continut).
    [HttpPost("/docs/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [Bind(Prefix = "Form")] SaveDocumentInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        if (!TryValidateSaveInput(input, out var validationMessage))
        {
            var invalidViewModel = await BuildEditorViewModelAsync(userContext, input.Id, cancellationToken);
            invalidViewModel.Form = input;
            invalidViewModel.ErrorMessage = validationMessage;
            return View("Edit", invalidViewModel);
        }

        try
        {
            var updated = await documentsApiClient.UpdateAsync(
                userContext,
                input.Id,
                input.Title.Trim(),
                input.Content ?? string.Empty,
                cancellationToken);

            await BroadcastContentUpdatedAsync(updated.Id, updated.Content, updated.UpdatedAt, userContext.DisplayName ?? userContext.UserId);

            var viewModel = await BuildEditorViewModelAsync(userContext, updated.Id, cancellationToken);
            viewModel.SuccessMessage = "Saved";
            return View("Edit", viewModel);
        }
        catch (ApiRequestException ex) when (ex.StatusCode == (HttpStatusCode)423)
        {
            logger.LogInformation("Save blocked by lock for document {DocumentId}", input.Id);
            var viewModel = await BuildEditorViewModelAsync(userContext, input.Id, cancellationToken);
            viewModel.Form = input;
            viewModel.ErrorMessage = ex.Message;
            return View("Edit", viewModel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save document {DocumentId}", input.Id);
            var viewModel = await BuildEditorViewModelAsync(userContext, input.Id, cancellationToken);
            viewModel.Form = input;
            viewModel.ErrorMessage = ex.Message;
            return View("Edit", viewModel);
        }
    }

    // Autosave in fundal (folosit din JS), fara refresh complet.
    [HttpPost("/docs/{id:guid}/autosave")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSave(
        Guid id,
        [Bind(Prefix = "Form")] SaveDocumentInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Unauthorized();
        }

        if (id != input.Id)
        {
            return BadRequest(new { success = false, message = "Invalid document id." });
        }

        if (!TryValidateSaveInput(input, out var validationMessage))
        {
            return BadRequest(new { success = false, message = validationMessage });
        }

        try
        {
            var updated = await documentsApiClient.UpdateAsync(
                userContext,
                input.Id,
                input.Title.Trim(),
                input.Content ?? string.Empty,
                cancellationToken);

            await BroadcastContentUpdatedAsync(updated.Id, updated.Content, updated.UpdatedAt, userContext.DisplayName ?? userContext.UserId);

            return Ok(new
            {
                success = true,
                updatedAt = updated.UpdatedAt,
                message = "Saved"
            });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == (HttpStatusCode)423)
        {
            return StatusCode(423, new { success = false, message = ex.Message });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to autosave document {DocumentId}", input.Id);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Returneaza starea realtime curenta a documentului pentru resync fara refresh complet.
    [HttpGet("/docs/{id:guid}/realtime/state")]
    public async Task<IActionResult> RealtimeState(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Unauthorized(new { success = false, message = "Missing user context." });
        }

        try
        {
            var state = await documentsApiClient.GetRealtimeStateAsync(userContext, id, cancellationToken);
            return Ok(new
            {
                success = true,
                state
            });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load realtime state for document {DocumentId}", id);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Persista periodic HTML mirror pe revizia curenta (guard pentru concurenta).
    [HttpPost("/docs/{id:guid}/realtime/html-snapshot")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRealtimeHtmlSnapshot(
        Guid id,
        [FromForm] long revision,
        [FromForm] string contentHtml,
        CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Unauthorized(new { success = false, message = "Missing user context." });
        }

        try
        {
            var saved = await documentsApiClient.SaveRealtimeHtmlSnapshotAsync(
                userContext,
                id,
                revision,
                contentHtml ?? string.Empty,
                cancellationToken);

            return Ok(new
            {
                success = true,
                revision = saved.Revision,
                updatedAt = saved.UpdatedAtUtc
            });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save realtime HTML snapshot for document {DocumentId}", id);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Sterge documentul din lista si revine la /docs.
    [HttpPost("/docs/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        try
        {
            await documentsApiClient.DeleteAsync(userContext, id, cancellationToken);
            TempData["InfoMessage"] = "Document deleted.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete document {DocumentId}", id);
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // Creeaza un invite nou (owner-only).
    [HttpPost("/docs/{id:guid}/shares/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateShare(
        Guid id,
        [Bind(Prefix = "ShareForm")] ShareInviteInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            var invalidView = await BuildEditorViewModelAsync(userContext, id, cancellationToken);
            invalidView.ShareForm = input;
            invalidView.ErrorMessage = "Please fix share validation errors and try again.";
            return View("Edit", invalidView);
        }

        try
        {
            await documentsApiClient.CreateShareAsync(userContext, id, input.InviteeEmail.Trim(), input.Role, cancellationToken);
            TempData["InfoMessage"] = "Invite sent.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create share for document {DocumentId}", id);
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    // Revoca un invite sau permission existent (owner-only).
    [HttpPost("/docs/{id:guid}/shares/{shareId:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShare(Guid id, Guid shareId, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        try
        {
            await documentsApiClient.DeleteShareAsync(userContext, id, shareId, cancellationToken);
            TempData["InfoMessage"] = "Share entry revoked.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete share {ShareId} for document {DocumentId}", shareId, id);
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    // Lista invitatiilor incoming pentru userul logat.
    [HttpGet("/docs/invites")]
    public async Task<IActionResult> Invites(CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        await TrySyncPendingInvitesAsync(userContext, cancellationToken);

        var viewModel = new InvitesIndexViewModel
        {
            InfoMessage = TempData["InfoMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string
        };

        try
        {
            var invites = await documentsApiClient.GetIncomingInvitesAsync(userContext, cancellationToken);
            viewModel.Invites = invites.Select(x => new IncomingInviteViewModel
            {
                InviteId = x.InviteId,
                DocumentId = x.DocumentId,
                DocumentTitle = x.DocumentTitle,
                Role = x.Role,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc,
                CreatedByUserId = x.CreatedByUserId
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load incoming invites.");
            viewModel.ErrorMessage ??= ex.Message;
        }

        return View(viewModel);
    }

    // Accepta invitatia selectata.
    [HttpPost("/docs/invites/{inviteId:guid}/accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        try
        {
            await documentsApiClient.AcceptInviteAsync(userContext, inviteId, cancellationToken);
            TempData["InfoMessage"] = "Invite accepted.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to accept invite {InviteId}", inviteId);
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Invites));
    }

    // Refuza invitatia selectata.
    [HttpPost("/docs/invites/{inviteId:guid}/decline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Challenge();
        }

        try
        {
            await documentsApiClient.DeclineInviteAsync(userContext, inviteId, cancellationToken);
            TempData["InfoMessage"] = "Invite declined.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decline invite {InviteId}", inviteId);
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Invites));
    }

    // Cere lock de editare pentru documentul curent.
    [HttpPost("/docs/{id:guid}/lock/acquire")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcquireLock(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Unauthorized(new { success = false, message = "Missing user context." });
        }

        try
        {
            var lockInfo = await documentsApiClient.AcquireLockAsync(userContext, id, cancellationToken);
            await BroadcastLockChangedAsync(id, lockInfo);
            return Ok(new { success = true, lockInfo });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == (HttpStatusCode)423)
        {
            return StatusCode(423, new { success = false, message = ex.Message });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to acquire lock for document {DocumentId}", id);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Trimite heartbeat periodic pentru lock-ul activ.
    [HttpPost("/docs/{id:guid}/lock/heartbeat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HeartbeatLock(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Unauthorized(new { success = false, message = "Missing user context." });
        }

        try
        {
            var lockInfo = await documentsApiClient.HeartbeatLockAsync(userContext, id, cancellationToken);
            await BroadcastLockChangedAsync(id, lockInfo);
            return Ok(new { success = true, lockInfo });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == (HttpStatusCode)423)
        {
            return StatusCode(423, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to heartbeat lock for document {DocumentId}", id);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Elibereaza lock-ul curent.
    [HttpPost("/docs/{id:guid}/lock/release")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseLock(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadCurrentUserContext(out var userContext))
        {
            return Unauthorized(new { success = false, message = "Missing user context." });
        }

        try
        {
            var lockInfo = await documentsApiClient.ReleaseLockAsync(userContext, id, cancellationToken);
            await BroadcastLockChangedAsync(id, lockInfo);
            return Ok(new { success = true, lockInfo });
        }
        catch (ApiRequestException ex) when (ex.StatusCode == (HttpStatusCode)423)
        {
            return StatusCode(423, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to release lock for document {DocumentId}", id);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Construieste view-model-ul pentru dashboard-ul /docs.
    private async Task<DocumentsIndexViewModel> BuildIndexViewModelAsync(
        ApiUserContext userContext,
        string view,
        int syncedCount,
        CancellationToken cancellationToken)
    {
        var normalizedView = NormalizeView(view);

        var viewModel = new DocumentsIndexViewModel
        {
            ActiveView = normalizedView,
            SyncedInvitesCount = syncedCount,
            InfoMessage = TempData["InfoMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string
        };

        try
        {
            var documents = await documentsApiClient.ListAsync(userContext, normalizedView, cancellationToken);
            viewModel.Documents = documents.Select(x => new DocumentListItemViewModel
            {
                Id = x.Id,
                Title = x.Title,
                UpdatedAt = x.UpdatedAt,
                AccessRole = x.AccessRole,
                IsOwner = x.IsOwner
            }).ToList();

            var invites = await documentsApiClient.GetIncomingInvitesAsync(userContext, cancellationToken);
            viewModel.IncomingInvitesCount = invites.Count;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to build documents index view model.");
            viewModel.ErrorMessage ??= ex.Message;
        }

        return viewModel;
    }

    // Construieste view-model-ul editorului (doc + lock + shares pentru owner).
    private async Task<DocumentEditorViewModel> BuildEditorViewModelAsync(
        ApiUserContext userContext,
        Guid id,
        CancellationToken cancellationToken)
    {
        var viewModel = new DocumentEditorViewModel
        {
            Form = new SaveDocumentInputModel { Id = id },
            CurrentUserId = userContext.UserId,
            CurrentUserDisplayName = userContext.DisplayName ?? userContext.UserId,
            InfoMessage = TempData["InfoMessage"] as string
        };

        if (!string.IsNullOrWhiteSpace(TempData["ErrorMessage"] as string))
        {
            viewModel.ErrorMessage = TempData["ErrorMessage"] as string;
        }

        try
        {
            var document = await documentsApiClient.GetAsync(userContext, id, cancellationToken);
            if (document is null)
            {
                viewModel.NotFound = true;
                return viewModel;
            }

            viewModel.Form = new SaveDocumentInputModel
            {
                Id = document.Id,
                Title = document.Title,
                Content = document.Content
            };
            viewModel.CreatedAt = document.CreatedAt;
            viewModel.UpdatedAt = document.UpdatedAt;
            viewModel.AccessRole = document.AccessRole;
            viewModel.IsOwner = document.IsOwner;
            viewModel.CurrentRevision = document.LiveRevision;
            viewModel.ContentDeltaJson = string.IsNullOrWhiteSpace(document.ContentDeltaJson)
                ? "{\"ops\":[]}"
                : document.ContentDeltaJson;

            try
            {
                var realtimeState = await documentsApiClient.GetRealtimeStateAsync(userContext, id, cancellationToken);
                viewModel.CurrentRevision = realtimeState.Revision;
                viewModel.ContentDeltaJson = string.IsNullOrWhiteSpace(realtimeState.ContentDeltaJson)
                    ? "{\"ops\":[]}"
                    : realtimeState.ContentDeltaJson;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load realtime state for document {DocumentId}", id);
            }

            var lockInfo = await documentsApiClient.GetLockAsync(userContext, id, cancellationToken);
            viewModel.CurrentLock = new DocumentLockViewModel
            {
                DocumentId = lockInfo.DocumentId,
                IsLocked = lockInfo.IsLocked,
                LockOwnerUserId = lockInfo.LockOwnerUserId,
                LockOwnerDisplayName = lockInfo.LockOwnerDisplayName,
                AcquiredAtUtc = lockInfo.AcquiredAtUtc,
                LastHeartbeatAtUtc = lockInfo.LastHeartbeatAtUtc,
                ExpiresAtUtc = lockInfo.ExpiresAtUtc,
                IsOwnedByCurrentUser = lockInfo.IsOwnedByCurrentUser
            };

            if (viewModel.CanManageShares)
            {
                var shares = await documentsApiClient.GetSharesAsync(userContext, id, cancellationToken);
                viewModel.Shares = shares.Select(x => new DocumentShareItemViewModel
                {
                    Id = x.Id,
                    ShareType = x.ShareType,
                    Subject = x.Subject,
                    Role = x.Role,
                    Status = x.Status,
                    CreatedAtUtc = x.CreatedAtUtc,
                    ExpiresAtUtc = x.ExpiresAtUtc,
                    AcceptedAtUtc = x.AcceptedAtUtc
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load document {DocumentId}", id);
            viewModel.ErrorMessage = ex.Message;
        }

        return viewModel;
    }

    // Sincronizeaza invitatiile pending daca userul are email disponibil in profil.
    private async Task<int> TrySyncPendingInvitesAsync(ApiUserContext userContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.Email))
        {
            return 0;
        }

        try
        {
            return await documentsApiClient.SyncPendingInvitesAsync(userContext, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync pending invites.");
            return 0;
        }
    }

    // Publica in SignalR faptul ca documentul a fost actualizat.
    private Task BroadcastContentUpdatedAsync(Guid documentId, string contentHtml, DateTime updatedAtUtc, string updatedBy)
    {
        return hubContext.Clients
            .Group(DocumentCollabHub.GroupName(documentId))
            .SendAsync("ContentUpdated", documentId, contentHtml, updatedAtUtc, updatedBy);
    }

    // Publica in SignalR faptul ca lock-ul documentului s-a schimbat.
    private Task BroadcastLockChangedAsync(Guid documentId, DocumentLockDto lockInfo)
    {
        return hubContext.Clients
            .Group(DocumentCollabHub.GroupName(documentId))
            .SendAsync("LockChanged", documentId, lockInfo);
    }

    // Citeste contextul userului autentificat din claims.
    private bool TryReadCurrentUserContext(out ApiUserContext userContext)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userContext = new ApiUserContext
            {
                UserId = string.Empty
            };
            return false;
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        var displayName = User.Identity?.Name ?? email ?? userId;

        userContext = new ApiUserContext
        {
            UserId = userId,
            Email = email,
            DisplayName = displayName
        };

        return true;
    }

    // Normalizeaza filtrul de tab pentru /docs.
    private static string NormalizeView(string? rawView)
    {
        var value = (rawView ?? string.Empty).Trim().ToLowerInvariant();
        return value is "owned" or "shared" or "all" ? value : "all";
    }

    // Valideaza explicit campurile necesare pentru save/autosave, fara dependenta fragila de ModelState implicit.
    private static bool TryValidateSaveInput(SaveDocumentInputModel input, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (input.Id == Guid.Empty)
        {
            errorMessage = "Invalid document id.";
            return false;
        }

        var normalizedTitle = (input.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            errorMessage = "Title is required.";
            return false;
        }

        if (normalizedTitle.Length > AppDefaults.DocumentTitleMaxLength)
        {
            errorMessage = $"Title must be {AppDefaults.DocumentTitleMaxLength} characters or fewer.";
            return false;
        }

        input.Title = normalizedTitle;
        input.Content ??= string.Empty;
        return true;
    }
}
