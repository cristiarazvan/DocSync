using GoogDocsLite.Server.Data;
using GoogDocsLite.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogDocsLite.Server.Application.Services;

public class DocumentAccessService(AppDbContext dbContext) : IDocumentAccessService
{
    // Cauta documentul si determina rolul utilizatorului: Owner/Editor/Viewer/None.
    public async Task<DocumentAccessContext?> GetAccessContextAsync(Guid documentId, string userId, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        if (document.OwnerUserId == userId)
        {
            return new DocumentAccessContext
            {
                Document = document,
                AccessRole = DocumentAccessRole.Owner
            };
        }

        var permission = await dbContext.DocumentPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.UserId == userId, cancellationToken);

        if (permission is null)
        {
            return new DocumentAccessContext
            {
                Document = document,
                AccessRole = DocumentAccessRole.None
            };
        }

        return new DocumentAccessContext
        {
            Document = document,
            AccessRole = permission.Role == DocumentShareRole.Editor
                ? DocumentAccessRole.Editor
                : DocumentAccessRole.Viewer
        };
    }
}
