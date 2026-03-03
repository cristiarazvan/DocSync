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
            "Board Meeting Brief - Q2 Priorities",
            """
            <h2>Board meeting brief</h2>
            <p>This private draft prepares the owner for the quarterly board review.</p>
            <h3>Discussion points</h3>
            <ul>
                <li>Revenue pacing versus Q2 plan</li>
                <li>Hiring priorities across product and support</li>
                <li>Operational risks for the next two quarters</li>
            </ul>
            <p><strong>Owner note:</strong> keep this version private until finance commentary is merged.</p>
            """,
            utcNow,
            cancellationToken);

        await EnsureDocumentAsync(
            dbContext,
            DemoSeedDefaults.SharedEditorDocId,
            DemoSeedDefaults.OwnerUserId,
            "Product Launch FAQ - Working Draft",
            """
            <h2>Launch FAQ</h2>
            <p>This draft is shared with an editor for collaborative updates before release.</p>
            <h3>Current focus</h3>
            <ul>
                <li>Clarify pricing rollout questions</li>
                <li>Add support escalation guidance</li>
                <li>Finalize release timing language</li>
            </ul>
            <p>Please revise answers directly in the document and keep unresolved questions near the bottom.</p>
            """,
            utcNow.AddMinutes(-3),
            cancellationToken);

        await EnsureDocumentAsync(
            dbContext,
            DemoSeedDefaults.SharedViewerDocId,
            DemoSeedDefaults.OwnerUserId,
            "Client Case Study - Review Copy",
            """
            <h2>Case study review copy</h2>
            <p>This document is shared in viewer mode for final stakeholder review.</p>
            <h3>Story arc</h3>
            <ul>
                <li>Customer challenge and context</li>
                <li>Implementation timeline</li>
                <li>Measured outcome after launch</li>
            </ul>
            <p>Reviewers should confirm language, data points, and approval status without editing the source draft.</p>
            """,
            utcNow.AddMinutes(-6),
            cancellationToken);

        await EnsureDocumentAsync(
            dbContext,
            DemoSeedDefaults.PendingInviteDocId,
            DemoSeedDefaults.OwnerUserId,
            "Vendor Evaluation Notes - Awaiting Reviewer",
            """
            <h2>Vendor evaluation notes</h2>
            <p>This document demonstrates the pending invite flow for a reviewer who has not joined yet.</p>
            <h3>Sections prepared</h3>
            <ul>
                <li>Selection criteria</li>
                <li>Security review summary</li>
                <li>Commercial comparison</li>
            </ul>
            <p>An additional editor invitation is still pending on the seeded outsider account.</p>
            """,
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
            if (existing.Title != title || existing.Content != content || existing.UpdatedAt != updatedAt)
            {
                existing.Title = title;
                existing.Content = content;
                existing.UpdatedAt = updatedAt;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

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
