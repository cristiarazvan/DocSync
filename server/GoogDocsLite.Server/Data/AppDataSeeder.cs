using GoogDocsLite.Server.Data.Entities;
using GoogDocsLite.Shared;
using Microsoft.EntityFrameworkCore;

namespace GoogDocsLite.Server.Data;

public static class AppDataSeeder
{
    // Populeaza baza cu documente demo si permisiuni de test pentru Stage 4 + 5.
    public static async Task SeedAsync(AppDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        await EnsureDocumentAsync(
            dbContext,
            DemoSeedDefaults.PrivateDocId,
            DemoSeedDefaults.OwnerUserId,
            "Private Resume Draft",
            "<p>Acesta este un document privat al owner-ului demo.</p>",
            utcNow,
            cancellationToken);

        await EnsureDocumentAsync(
            dbContext,
            DemoSeedDefaults.SharedEditorDocId,
            DemoSeedDefaults.OwnerUserId,
            "Shared Resume (Editor)",
            "<p>Document partajat cu drept de editare.</p>",
            utcNow.AddMinutes(-3),
            cancellationToken);

        await EnsureDocumentAsync(
            dbContext,
            DemoSeedDefaults.SharedViewerDocId,
            DemoSeedDefaults.OwnerUserId,
            "Shared Portfolio (Viewer)",
            "<p>Document partajat doar pentru vizualizare.</p>",
            utcNow.AddMinutes(-6),
            cancellationToken);

        await EnsureDocumentAsync(
            dbContext,
            DemoSeedDefaults.PendingInviteDocId,
            DemoSeedDefaults.OwnerUserId,
            "Pending Invite Example",
            "<p>Exemplu pentru invitatie pending pe email neinregistrat.</p>",
            utcNow.AddMinutes(-9),
            cancellationToken);

        await EnsurePermissionAsync(
            dbContext,
            DemoSeedDefaults.SharedEditorDocId,
            DemoSeedDefaults.EditorUserId,
            DocumentShareRole.Editor,
            cancellationToken);

        await EnsurePermissionAsync(
            dbContext,
            DemoSeedDefaults.SharedViewerDocId,
            DemoSeedDefaults.ViewerUserId,
            DocumentShareRole.Viewer,
            cancellationToken);

        await EnsurePendingInviteAsync(
            dbContext,
            DemoSeedDefaults.PendingInviteDocId,
            DemoSeedDefaults.PendingInviteEmail,
            DocumentShareRole.Editor,
            cancellationToken);

        logger.LogInformation("Demo documents + permissions seeded.");
    }

    // Creeaza documentul demo daca nu exista deja.
    private static async Task EnsureDocumentAsync(
        AppDbContext dbContext,
        Guid documentId,
        string ownerUserId,
        string title,
        string content,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Documents.FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        dbContext.Documents.Add(new DocumentEntity
        {
            Id = documentId,
            OwnerUserId = ownerUserId,
            Title = title,
            Content = content,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Creeaza permission (editor/viewer) daca nu exista deja pentru acel user/document.
    private static async Task EnsurePermissionAsync(
        AppDbContext dbContext,
        Guid documentId,
        string userId,
        DocumentShareRole role,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.DocumentPermissions
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            if (existing.Role != role)
            {
                existing.Role = role;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        dbContext.DocumentPermissions.Add(new DocumentPermissionEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserId = userId,
            Role = role,
            GrantedByUserId = DemoSeedDefaults.OwnerUserId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Creeaza invitatia pending daca nu exista deja una activa pe acel email/document.
    private static async Task EnsurePendingInviteAsync(
        AppDbContext dbContext,
        Guid documentId,
        string inviteeEmail,
        DocumentShareRole role,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = inviteeEmail.Trim().ToLowerInvariant();

        var existing = await dbContext.DocumentInvites
            .FirstOrDefaultAsync(
                x => x.DocumentId == documentId &&
                     x.InviteeEmailNormalized == normalizedEmail &&
                     x.Status == DocumentInviteStatus.Pending,
                cancellationToken);

        if (existing is not null)
        {
            return;
        }

        dbContext.DocumentInvites.Add(new DocumentInviteEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            InviteeEmailNormalized = normalizedEmail,
            Role = role,
            Status = DocumentInviteStatus.Pending,
            CreatedByUserId = DemoSeedDefaults.OwnerUserId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
