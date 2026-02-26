using GoogDocsLite.Server.Contracts.Documents;
using GoogDocsLite.Server.Contracts.Invites;

namespace GoogDocsLite.Server.Application.Services;

public interface IDocumentService
{
    // Intoarce documentele vizibile pentru user, filtrate dupa view (owned/shared/all).
    Task<IReadOnlyCollection<DocumentListItemDto>> ListForUserAsync(string userId, string view, CancellationToken cancellationToken);

    // Citeste un document daca userul are acces.
    Task<ServiceResult<DocumentDto>> GetForUserAsync(string userId, Guid id, CancellationToken cancellationToken);

    // Creeaza un document nou pentru userul curent.
    Task<DocumentDto> CreateForUserAsync(string userId, CreateDocumentRequest request, CancellationToken cancellationToken);

    // Actualizeaza documentul daca userul are drept de editare si lock activ.
    Task<ServiceResult<DocumentDto>> UpdateForUserAsync(string userId, Guid id, UpdateDocumentRequest request, CancellationToken cancellationToken);

    // Stage 6: aplica patch live pe continut (lock owner-only).
    Task<ServiceResult<LiveContentPatchResponseDto>> ApplyLiveContentAsync(
        string userId,
        Guid id,
        ApplyLiveContentRequest request,
        CancellationToken cancellationToken);

    // Sterge documentul; permis doar ownerului.
    Task<ServiceResult> DeleteForUserAsync(string userId, Guid id, CancellationToken cancellationToken);

    // Returneaza lista de permission + invites pentru owner.
    Task<ServiceResult<IReadOnlyCollection<DocumentShareItemDto>>> GetSharesForUserAsync(string userId, Guid id, CancellationToken cancellationToken);

    // Creeaza/actualizeaza invitatia de share.
    Task<ServiceResult<DocumentShareItemDto>> CreateShareAsync(string userId, Guid id, CreateShareRequest request, CancellationToken cancellationToken);

    // Revoca permission sau invite dupa id.
    Task<ServiceResult> DeleteShareAsync(string userId, Guid id, Guid shareId, CancellationToken cancellationToken);

    // Returneaza invitatiile incoming pentru email-ul userului.
    Task<IReadOnlyCollection<IncomingInviteDto>> GetIncomingInvitesAsync(string userId, string userEmail, CancellationToken cancellationToken);

    // Accepta invitatia si acorda permission.
    Task<ServiceResult> AcceptInviteAsync(string userId, string userEmail, Guid inviteId, CancellationToken cancellationToken);

    // Refuza invitatia.
    Task<ServiceResult> DeclineInviteAsync(string userId, string userEmail, Guid inviteId, CancellationToken cancellationToken);

    // Convertește invitatiile pending in permissions pentru user-ul logat.
    Task<int> SyncPendingInvitesAsync(string userId, string userEmail, CancellationToken cancellationToken);

    // Stage 7: intoarce snapshot-ul realtime curent (revizie + delta + html).
    Task<ServiceResult<RealtimeStateDto>> GetRealtimeStateAsync(string userId, Guid id, CancellationToken cancellationToken);

    // Stage 7: aplica un delta op OT pe serverul autoritativ.
    Task<ServiceResult<SubmitRealtimeOperationResponseDto>> SubmitRealtimeOperationAsync(
        string userId,
        Guid id,
        SubmitRealtimeOperationRequest request,
        CancellationToken cancellationToken);

    // Stage 7: lista de operatii dupa o anumita revizie.
    Task<ServiceResult<IReadOnlyCollection<RealtimeOperationDto>>> ListRealtimeOperationsAsync(
        string userId,
        Guid id,
        long afterRevision,
        CancellationToken cancellationToken);

    // Stage 7: actualizeaza HTML mirror daca revizia corespunde.
    Task<ServiceResult<SaveRealtimeHtmlSnapshotResponseDto>> SaveRealtimeHtmlSnapshotAsync(
        string userId,
        Guid id,
        SaveRealtimeHtmlSnapshotRequest request,
        CancellationToken cancellationToken);
}
