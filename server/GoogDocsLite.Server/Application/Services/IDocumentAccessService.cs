namespace GoogDocsLite.Server.Application.Services;

public interface IDocumentAccessService
{
    // Calculeaza drepturile utilizatorului pe documentul cerut.
    Task<DocumentAccessContext?> GetAccessContextAsync(Guid documentId, string userId, CancellationToken cancellationToken);
}
