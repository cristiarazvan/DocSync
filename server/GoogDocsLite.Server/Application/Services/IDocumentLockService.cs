using GoogDocsLite.Server.Contracts.Documents;

namespace GoogDocsLite.Server.Application.Services;

public interface IDocumentLockService
{
    // Returneaza lock-ul curent (daca exista) pentru un document accesibil userului.
    Task<ServiceResult<DocumentLockDto>> GetLockAsync(string userId, Guid documentId, CancellationToken cancellationToken);

    // Incearca sa ia lock-ul de editare pentru utilizatorul curent.
    Task<ServiceResult<DocumentLockDto>> AcquireLockAsync(string userId, string displayName, Guid documentId, CancellationToken cancellationToken);

    // Mentine lock-ul activ prin heartbeat.
    Task<ServiceResult<DocumentLockDto>> HeartbeatLockAsync(string userId, Guid documentId, CancellationToken cancellationToken);

    // Elibereaza lock-ul daca apartine utilizatorului curent.
    Task<ServiceResult<DocumentLockDto>> ReleaseLockAsync(string userId, Guid documentId, CancellationToken cancellationToken);

    // Folosit de update pentru a valida ca userul care salveaza este lock owner activ.
    Task<bool> IsUserCurrentLockOwnerAsync(Guid documentId, string userId, CancellationToken cancellationToken);
}
