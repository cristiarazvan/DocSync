using GoogDocsLite.Client.Models.Api;
using System.Net;
using System.Net.Http.Json;

namespace GoogDocsLite.Client.Services;

public class DocumentsApiClient(HttpClient httpClient, IConfiguration configuration)
{
    private readonly string _internalApiKey = configuration["InternalApiKey"] ?? "dev-internal-key-change-me";

    // Cere lista de documente de la API (owned/shared/all).
    public async Task<IReadOnlyList<DocumentListItemDto>> ListAsync(
        ApiUserContext userContext,
        string view = "all",
        CancellationToken cancellationToken = default)
    {
        var safeView = string.IsNullOrWhiteSpace(view) ? "all" : view.Trim().ToLowerInvariant();
        using var request = BuildRequest(HttpMethod.Get, $"api/documents?view={Uri.EscapeDataString(safeView)}", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<DocumentListItemDto>>(cancellationToken) ?? [];
    }

    // Citeste un document dupa id; daca nu exista, intoarce null.
    public async Task<DocumentDto?> GetAsync(ApiUserContext userContext, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Get, $"api/documents/{id}", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ApiErrorHelper.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken);
    }

    // Creeaza un document nou pe server.
    public async Task<DocumentDto> CreateAsync(ApiUserContext userContext, string title, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, "api/documents", userContext);
        request.Content = JsonContent.Create(new { title });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken))!;
    }

    // Actualizeaza titlul si continutul unui document existent.
    public async Task<DocumentDto> UpdateAsync(ApiUserContext userContext, Guid id, string title, string content, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Put, $"api/documents/{id}", userContext);
        request.Content = JsonContent.Create(new { title, content });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken))!;
    }

    // Stage 6: patch live de continut (lock owner-only).
    public async Task<LiveContentPatchResponseDto> PatchLiveContentAsync(
        ApiUserContext userContext,
        Guid id,
        string? title,
        string contentHtml,
        string? contentDeltaJson,
        long clientSequence,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Patch, $"api/documents/{id}/live-content", userContext);
        request.Content = JsonContent.Create(new
        {
            title,
            contentHtml,
            contentDeltaJson,
            clientSequence
        });

        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<LiveContentPatchResponseDto>(cancellationToken))!;
    }

    // Stage 7: snapshot realtime complet.
    public async Task<RealtimeStateDto> GetRealtimeStateAsync(ApiUserContext userContext, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Get, $"api/documents/{id}/realtime/state", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<RealtimeStateDto>(cancellationToken))!;
    }

    // Stage 7: trimite operatie OT.
    public async Task<SubmitRealtimeOperationResponseDto> SubmitRealtimeOperationAsync(
        ApiUserContext userContext,
        Guid id,
        long baseRevision,
        string clientOpId,
        string deltaJson,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, $"api/documents/{id}/realtime/ops", userContext);
        request.Content = JsonContent.Create(new
        {
            baseRevision,
            clientOpId,
            deltaJson
        });

        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<SubmitRealtimeOperationResponseDto>(cancellationToken))!;
    }

    // Stage 7: replay operatii dupa o anumita revizie.
    public async Task<IReadOnlyList<RealtimeOperationDto>> GetRealtimeOperationsAsync(
        ApiUserContext userContext,
        Guid id,
        long afterRevision,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Get, $"api/documents/{id}/realtime/ops?afterRevision={afterRevision}", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<RealtimeOperationDto>>(cancellationToken) ?? [];
    }

    // Stage 7: persista HTML mirror pe revizia curenta.
    public async Task<SaveRealtimeHtmlSnapshotResponseDto> SaveRealtimeHtmlSnapshotAsync(
        ApiUserContext userContext,
        Guid id,
        long revision,
        string contentHtml,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Put, $"api/documents/{id}/realtime/html-snapshot", userContext);
        request.Content = JsonContent.Create(new
        {
            revision,
            contentHtml
        });

        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<SaveRealtimeHtmlSnapshotResponseDto>(cancellationToken))!;
    }

    // Sterge documentul dupa id.
    public async Task DeleteAsync(ApiUserContext userContext, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Delete, $"api/documents/{id}", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
    }

    // Cere lista de shares pentru document (owner-only).
    public async Task<IReadOnlyList<DocumentShareItemDto>> GetSharesAsync(ApiUserContext userContext, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Get, $"api/documents/{id}/shares", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<DocumentShareItemDto>>(cancellationToken) ?? [];
    }

    // Creeaza sau reactualizeaza un invite pentru document.
    public async Task<DocumentShareItemDto> CreateShareAsync(
        ApiUserContext userContext,
        Guid id,
        string inviteeEmail,
        string role,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, $"api/documents/{id}/shares", userContext);
        request.Content = JsonContent.Create(new { inviteeEmail, role });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<DocumentShareItemDto>(cancellationToken))!;
    }

    // Revoca un share entry (permission sau invite).
    public async Task DeleteShareAsync(ApiUserContext userContext, Guid id, Guid shareId, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Delete, $"api/documents/{id}/shares/{shareId}", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
    }

    // Intoarce invitatiile incoming pentru email-ul userului.
    public async Task<IReadOnlyList<IncomingInviteDto>> GetIncomingInvitesAsync(ApiUserContext userContext, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Get, "api/invites/incoming", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<IncomingInviteDto>>(cancellationToken) ?? [];
    }

    // Accepta invitatia.
    public async Task AcceptInviteAsync(ApiUserContext userContext, Guid inviteId, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, $"api/invites/{inviteId}/accept", userContext);
        request.Content = JsonContent.Create(new { });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
    }

    // Refuza invitatia.
    public async Task DeclineInviteAsync(ApiUserContext userContext, Guid inviteId, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, $"api/invites/{inviteId}/decline", userContext);
        request.Content = JsonContent.Create(new { });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
    }

    // Sincronizeaza invitatiile pending pentru user-ul curent.
    public async Task<int> SyncPendingInvitesAsync(ApiUserContext userContext, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, "api/invites/sync-pending", userContext);
        request.Content = JsonContent.Create(new { });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<SyncPendingInvitesResponse>(cancellationToken);
        return payload?.SyncedCount ?? 0;
    }

    // Returneaza statusul lock-ului de editare.
    public async Task<DocumentLockDto> GetLockAsync(ApiUserContext userContext, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Get, $"api/documents/{id}/lock", userContext);
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<DocumentLockDto>(cancellationToken))!;
    }

    // Incearca sa obtina lock-ul de editare.
    public async Task<DocumentLockDto> AcquireLockAsync(ApiUserContext userContext, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, $"api/documents/{id}/lock/acquire", userContext);
        request.Content = JsonContent.Create(new { });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<DocumentLockDto>(cancellationToken))!;
    }

    // Trimite heartbeat pentru lock.
    public async Task<DocumentLockDto> HeartbeatLockAsync(ApiUserContext userContext, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, $"api/documents/{id}/lock/heartbeat", userContext);
        request.Content = JsonContent.Create(new { });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<DocumentLockDto>(cancellationToken))!;
    }

    // Elibereaza lock-ul curent.
    public async Task<DocumentLockDto> ReleaseLockAsync(ApiUserContext userContext, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, $"api/documents/{id}/lock/release", userContext);
        request.Content = JsonContent.Create(new { });
        var response = await httpClient.SendAsync(request, cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<DocumentLockDto>(cancellationToken))!;
    }

    // Construieste request-ul si adauga contextul userului autentificat.
    private HttpRequestMessage BuildRequest(HttpMethod method, string uri, ApiUserContext userContext)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-User-Id", userContext.UserId);
        request.Headers.Add("X-Internal-Api-Key", _internalApiKey);

        if (!string.IsNullOrWhiteSpace(userContext.Email))
        {
            request.Headers.Add("X-User-Email", userContext.Email);
        }

        if (!string.IsNullOrWhiteSpace(userContext.DisplayName))
        {
            request.Headers.Add("X-User-Display-Name", userContext.DisplayName);
        }

        return request;
    }
}
