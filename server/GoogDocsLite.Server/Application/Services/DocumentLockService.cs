using GoogDocsLite.Server.Contracts.Documents;
using GoogDocsLite.Server.Data;
using GoogDocsLite.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogDocsLite.Server.Application.Services;

public class DocumentLockService(
    AppDbContext dbContext,
    IDocumentAccessService accessService,
    ILogger<DocumentLockService> logger) : IDocumentLockService
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);

    // Returneaza lock-ul activ daca userul are minim drept de citire pe document.
    public async Task<ServiceResult<DocumentLockDto>> GetLockAsync(string userId, Guid documentId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(documentId, userId, cancellationToken);
        if (access is null || !access.CanRead)
        {
            return ServiceResult<DocumentLockDto>.NotFound();
        }

        var activeLock = await GetActiveLockEntityAsync(documentId, cancellationToken);
        return ServiceResult<DocumentLockDto>.Success(ToLockDto(documentId, userId, activeLock));
    }

    // Incearca sa acorde lock utilizatorului curent daca are drept de editare.
    public async Task<ServiceResult<DocumentLockDto>> AcquireLockAsync(string userId, string displayName, Guid documentId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(documentId, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<DocumentLockDto>.NotFound();
        }

        if (!access.CanEdit)
        {
            return ServiceResult<DocumentLockDto>.Forbidden("Only owner or editor can acquire lock.");
        }

        var utcNow = DateTime.UtcNow;
        var existing = await dbContext.DocumentEditLocks
            .FirstOrDefaultAsync(x => x.DocumentId == documentId, cancellationToken);

        if (existing is not null && existing.ExpiresAtUtc > utcNow && existing.LockOwnerUserId != userId)
        {
            return ServiceResult<DocumentLockDto>.Locked("Document is already being edited by another user.");
        }

        if (existing is null)
        {
            existing = new DocumentEditLockEntity
            {
                DocumentId = documentId
            };
            dbContext.DocumentEditLocks.Add(existing);
        }

        existing.LockOwnerUserId = userId;
        existing.LockOwnerDisplayName = string.IsNullOrWhiteSpace(displayName) ? userId : displayName.Trim();
        existing.AcquiredAtUtc = utcNow;
        existing.LastHeartbeatAtUtc = utcNow;
        existing.ExpiresAtUtc = utcNow.Add(LockDuration);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Lock acquired for document {DocumentId} by {UserId}", documentId, userId);
        return ServiceResult<DocumentLockDto>.Success(ToLockDto(documentId, userId, existing));
    }

    // Prelungeste lock-ul daca acesta apartine aceluiasi utilizator.
    public async Task<ServiceResult<DocumentLockDto>> HeartbeatLockAsync(string userId, Guid documentId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(documentId, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<DocumentLockDto>.NotFound();
        }

        if (!access.CanEdit)
        {
            return ServiceResult<DocumentLockDto>.Forbidden("Only owner or editor can send heartbeat.");
        }

        var utcNow = DateTime.UtcNow;
        var existing = await dbContext.DocumentEditLocks
            .FirstOrDefaultAsync(x => x.DocumentId == documentId, cancellationToken);

        if (existing is null || existing.ExpiresAtUtc <= utcNow)
        {
            if (existing is not null)
            {
                dbContext.DocumentEditLocks.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return ServiceResult<DocumentLockDto>.Locked("No active lock. Acquire lock first.");
        }

        if (existing.LockOwnerUserId != userId)
        {
            return ServiceResult<DocumentLockDto>.Locked("Another user currently owns the lock.");
        }

        existing.LastHeartbeatAtUtc = utcNow;
        existing.ExpiresAtUtc = utcNow.Add(LockDuration);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<DocumentLockDto>.Success(ToLockDto(documentId, userId, existing));
    }

    // Elibereaza lock-ul doar daca userul curent este owner-ul lock-ului.
    public async Task<ServiceResult<DocumentLockDto>> ReleaseLockAsync(string userId, Guid documentId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(documentId, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<DocumentLockDto>.NotFound();
        }

        var existing = await dbContext.DocumentEditLocks
            .FirstOrDefaultAsync(x => x.DocumentId == documentId, cancellationToken);

        if (existing is null)
        {
            return ServiceResult<DocumentLockDto>.Success(ToLockDto(documentId, userId, null));
        }

        var utcNow = DateTime.UtcNow;
        if (existing.ExpiresAtUtc <= utcNow)
        {
            dbContext.DocumentEditLocks.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<DocumentLockDto>.Success(ToLockDto(documentId, userId, null));
        }

        if (existing.LockOwnerUserId != userId)
        {
            return ServiceResult<DocumentLockDto>.Locked("Only the lock owner can release the lock.");
        }

        dbContext.DocumentEditLocks.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Lock released for document {DocumentId} by {UserId}", documentId, userId);
        return ServiceResult<DocumentLockDto>.Success(ToLockDto(documentId, userId, null));
    }

    // Verifica strict daca lock-ul activ exista si apartine userului curent.
    public async Task<bool> IsUserCurrentLockOwnerAsync(Guid documentId, string userId, CancellationToken cancellationToken)
    {
        var activeLock = await GetActiveLockEntityAsync(documentId, cancellationToken);
        return activeLock is not null && activeLock.LockOwnerUserId == userId;
    }

    // Returneaza lock activ; daca lock-ul expira, il curata automat din baza.
    private async Task<DocumentEditLockEntity?> GetActiveLockEntityAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.DocumentEditLocks
            .FirstOrDefaultAsync(x => x.DocumentId == documentId, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.ExpiresAtUtc > DateTime.UtcNow)
        {
            return existing;
        }

        dbContext.DocumentEditLocks.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return null;
    }

    // Construieste DTO-ul de lock pe care il trimitem clientului.
    private static DocumentLockDto ToLockDto(Guid documentId, string currentUserId, DocumentEditLockEntity? lockEntity)
    {
        if (lockEntity is null)
        {
            return new DocumentLockDto
            {
                DocumentId = documentId,
                IsLocked = false,
                IsOwnedByCurrentUser = false
            };
        }

        return new DocumentLockDto
        {
            DocumentId = documentId,
            IsLocked = true,
            LockOwnerUserId = lockEntity.LockOwnerUserId,
            LockOwnerDisplayName = lockEntity.LockOwnerDisplayName,
            AcquiredAtUtc = lockEntity.AcquiredAtUtc,
            LastHeartbeatAtUtc = lockEntity.LastHeartbeatAtUtc,
            ExpiresAtUtc = lockEntity.ExpiresAtUtc,
            IsOwnedByCurrentUser = lockEntity.LockOwnerUserId == currentUserId
        };
    }
}
