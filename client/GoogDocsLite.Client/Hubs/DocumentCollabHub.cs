using System.Security.Claims;
using GoogDocsLite.Client.Models.Api;
using GoogDocsLite.Client.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GoogDocsLite.Client.Hubs;

[Authorize]
public class DocumentCollabHub(
    DocumentPresenceTracker presenceTracker,
    DocumentsApiClient documentsApiClient,
    ILogger<DocumentCollabHub> logger) : Hub
{
    // Intoarce numele grupului SignalR pentru un document.
    public static string GroupName(Guid documentId) => $"document-{documentId}";

    // Cand clientul intra pe document, il adaugam in grup si actualizam prezenta.
    public async Task JoinDocument(Guid documentId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new HubException("Missing user context.");
        }

        var displayName = Context.User?.Identity?.Name
            ?? Context.User?.FindFirstValue(ClaimTypes.Email)
            ?? userId;

        var previousDocumentId = presenceTracker.Join(documentId, Context.ConnectionId, userId, displayName);

        if (previousDocumentId.HasValue && previousDocumentId.Value != documentId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(previousDocumentId.Value));
            await BroadcastPresenceAsync(previousDocumentId.Value);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(documentId));
        await BroadcastPresenceAsync(documentId);
    }

    // Stage 6: lock owner-ul trimite continut live, persistam si broadcast-am instant.
    public async Task SendLiveContent(Guid documentId, long clientSequence, string contentHtml, string? contentDeltaJson)
    {
        var userContext = BuildUserContext();

        try
        {
            var lockInfo = await documentsApiClient.GetLockAsync(userContext, documentId, Context.ConnectionAborted);
            if (!lockInfo.IsOwnedByCurrentUser)
            {
                throw new HubException("You must hold the active edit lock to stream live content.");
            }

            var response = await documentsApiClient.PatchLiveContentAsync(
                userContext,
                documentId,
                title: null,
                contentHtml: contentHtml ?? string.Empty,
                contentDeltaJson: contentDeltaJson,
                clientSequence: clientSequence,
                cancellationToken: Context.ConnectionAborted);

            await Clients.Group(GroupName(documentId)).SendAsync(
                "LiveContentPatched",
                documentId,
                response.LiveRevision,
                contentHtml ?? string.Empty,
                contentDeltaJson,
                userContext.DisplayName ?? userContext.UserId,
                response.UpdatedAtUtc);
        }
        catch (ApiRequestException ex)
        {
            logger.LogInformation(ex, "Live content patch rejected for document {DocumentId}", documentId);
            throw new HubException(ex.Message);
        }
    }

    // Stage 7: trimite operatie OT catre API-ul autoritativ si broadcast-eaza forma canonica acceptata.
    public async Task SubmitOperation(Guid documentId, long baseRevision, string clientOpId, string deltaJson)
    {
        var userContext = BuildUserContext();

        try
        {
            var accepted = await documentsApiClient.SubmitRealtimeOperationAsync(
                userContext,
                documentId,
                baseRevision,
                clientOpId,
                deltaJson,
                Context.ConnectionAborted);

            await Clients.Group(GroupName(documentId)).SendAsync(
                "OperationAccepted",
                documentId,
                accepted.AcceptedRevision,
                accepted.TransformedDeltaJson,
                userContext.UserId,
                accepted.UpdatedAtUtc,
                clientOpId);
        }
        catch (ApiRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                await Clients.Caller.SendAsync("RealtimeStateResyncRequired", documentId, ex.Message);
                return;
            }

            logger.LogInformation(ex, "Realtime op rejected for document {DocumentId}", documentId);
            throw new HubException(ex.Message);
        }
    }

    // Stage 7: clientul cere operatiile lipsa dupa o revizie pentru recuperare fara refresh.
    public async Task RequestMissingOperations(Guid documentId, long afterRevision)
    {
        var userContext = BuildUserContext();

        try
        {
            var operations = await documentsApiClient.GetRealtimeOperationsAsync(
                userContext,
                documentId,
                afterRevision,
                Context.ConnectionAborted);

            await Clients.Caller.SendAsync("OperationsReplay", documentId, operations);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime replay failed for document {DocumentId}", documentId);
            await Clients.Caller.SendAsync("RealtimeStateResyncRequired", documentId, "Replay failed. Full resync required.");
        }
    }

    // Cand clientul paraseste documentul, il scoatem din grup si actualizam prezenta.
    public async Task LeaveDocument(Guid documentId)
    {
        presenceTracker.Leave(documentId, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(documentId));
        await BroadcastPresenceAsync(documentId);
    }

    // La disconnect, curatam prezenta pentru conexiunea inchisa.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var affectedDocuments = presenceTracker.RemoveConnection(Context.ConnectionId);

        foreach (var documentId in affectedDocuments)
        {
            await BroadcastPresenceAsync(documentId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Trimite lista userilor prezenti pe document catre toti clientii din grup.
    private Task BroadcastPresenceAsync(Guid documentId)
    {
        var users = presenceTracker.GetUsers(documentId);
        return Clients.Group(GroupName(documentId))
            .SendAsync("PresenceChanged", documentId, users);
    }

    // Construieste contextul userului autentificat pentru apelurile MVC -> API.
    private ApiUserContext BuildUserContext()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new HubException("Missing user context.");
        }

        var email = Context.User?.FindFirstValue(ClaimTypes.Email) ?? Context.User?.Identity?.Name;
        var displayName = Context.User?.Identity?.Name ?? email ?? userId;

        return new ApiUserContext
        {
            UserId = userId,
            Email = email,
            DisplayName = displayName
        };
    }
}
