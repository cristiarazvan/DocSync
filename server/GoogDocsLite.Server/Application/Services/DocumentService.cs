using GoogDocsLite.Server.Contracts.Documents;
using GoogDocsLite.Server.Contracts.Invites;
using GoogDocsLite.Server.Data;
using GoogDocsLite.Server.Data.Entities;
using GoogDocsLite.Shared;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GoogDocsLite.Server.Application.Services;

public class DocumentService(
    AppDbContext dbContext,
    IDocumentAccessService accessService,
    ILogger<DocumentService> logger) : IDocumentService
{
    private static readonly HtmlSanitizer HtmlSanitizer = BuildHtmlSanitizer();
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(14);

    // Intoarce documentele vizibile pentru user in functie de filtrul cerut.
    public async Task<IReadOnlyCollection<DocumentListItemDto>> ListForUserAsync(string userId, string view, CancellationToken cancellationToken)
    {
        var normalizedView = (view ?? string.Empty).Trim().ToLowerInvariant();

        var ownedDocuments = await dbContext.Documents
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .Select(x => new DocumentListItemDto
            {
                Id = x.Id,
                Title = x.Title,
                UpdatedAt = x.UpdatedAt,
                AccessRole = DocumentAccessRole.Owner.ToString(),
                IsOwner = true
            })
            .ToListAsync(cancellationToken);

        var sharedDocuments = await dbContext.DocumentPermissions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(
                dbContext.Documents.AsNoTracking().Where(x => x.OwnerUserId != userId),
                permission => permission.DocumentId,
                document => document.Id,
                (permission, document) => new DocumentListItemDto
                {
                    Id = document.Id,
                    Title = document.Title,
                    UpdatedAt = document.UpdatedAt,
                    AccessRole = permission.Role == DocumentShareRole.Editor
                        ? DocumentAccessRole.Editor.ToString()
                        : DocumentAccessRole.Viewer.ToString(),
                    IsOwner = false
                })
            .ToListAsync(cancellationToken);

        return normalizedView switch
        {
            "owned" => ownedDocuments.OrderByDescending(x => x.UpdatedAt).ToList(),
            "shared" => sharedDocuments.OrderByDescending(x => x.UpdatedAt).ToList(),
            _ => ownedDocuments
                .Concat(sharedDocuments)
                .OrderByDescending(x => x.UpdatedAt)
                .ToList()
        };
    }

    // Returneaza documentul daca userul are macar drept de citire.
    public async Task<ServiceResult<DocumentDto>> GetForUserAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || !access.CanRead)
        {
            return ServiceResult<DocumentDto>.NotFound();
        }

        return ServiceResult<DocumentDto>.Success(ToDto(access.Document, access.AccessRole));
    }

    // Creeaza document nou (owner = user curent).
    public async Task<DocumentDto> CreateForUserAsync(string userId, CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var entity = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = string.Empty,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            OwnerUserId = userId
        };

        dbContext.Documents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document created {DocumentId}", entity.Id);

        return ToDto(entity, DocumentAccessRole.Owner);
    }

    // Actualizeaza documentul daca userul are drept de editare.
    public async Task<ServiceResult<DocumentDto>> UpdateForUserAsync(string userId, Guid id, UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<DocumentDto>.NotFound();
        }

        if (!access.CanEdit)
        {
            return ServiceResult<DocumentDto>.Forbidden("You only have view access for this document.");
        }

        var entity = await dbContext.Documents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return ServiceResult<DocumentDto>.NotFound();
        }

        entity.Title = request.Title.Trim();
        entity.Content = SanitizeContent(request.Content);
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Concurrency conflict while updating {DocumentId}", id);
            return ServiceResult<DocumentDto>.NotFound();
        }

        logger.LogInformation("Document updated {DocumentId}", entity.Id);

        return ServiceResult<DocumentDto>.Success(ToDto(entity, access.AccessRole));
    }

    // Stage 6: aplica patch live pe continut (owner/editor + lock activ).
    public async Task<ServiceResult<LiveContentPatchResponseDto>> ApplyLiveContentAsync(
        string userId,
        Guid id,
        ApplyLiveContentRequest request,
        CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<LiveContentPatchResponseDto>.NotFound();
        }

        if (!access.CanEdit)
        {
            return ServiceResult<LiveContentPatchResponseDto>.Forbidden("Only owner or editor can stream live updates.");
        }

        var activeLock = await dbContext.DocumentEditLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DocumentId == id, cancellationToken);

        if (activeLock is null || activeLock.ExpiresAtUtc <= DateTime.UtcNow || activeLock.LockOwnerUserId != userId)
        {
            return ServiceResult<LiveContentPatchResponseDto>.Locked("You must hold the active edit lock for live updates.");
        }

        if (request.ClientSequence <= 0)
        {
            return ServiceResult<LiveContentPatchResponseDto>.ValidationError("Client sequence must be a positive number.");
        }

        var entity = await dbContext.Documents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return ServiceResult<LiveContentPatchResponseDto>.NotFound();
        }

        if (request.Title is not null)
        {
            var normalizedTitle = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return ServiceResult<LiveContentPatchResponseDto>.ValidationError("Title cannot be empty.");
            }

            if (normalizedTitle.Length > AppDefaults.DocumentTitleMaxLength)
            {
                return ServiceResult<LiveContentPatchResponseDto>.ValidationError($"Title must be {AppDefaults.DocumentTitleMaxLength} characters or fewer.");
            }

            entity.Title = normalizedTitle;
        }

        entity.Content = SanitizeContent(request.ContentHtml);
        entity.ContentDeltaJson = NormalizeDeltaOrNull(request.ContentDeltaJson);
        entity.LiveRevision += 1;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<LiveContentPatchResponseDto>.Success(new LiveContentPatchResponseDto
        {
            DocumentId = entity.Id,
            UpdatedAtUtc = entity.UpdatedAt,
            LiveRevision = entity.LiveRevision
        });
    }

    // Sterge documentul; permis doar ownerului.
    public async Task<ServiceResult> DeleteForUserAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult.NotFound();
        }

        if (!access.CanDelete)
        {
            return ServiceResult.Forbidden("Only owner can delete a document.");
        }

        var entity = await dbContext.Documents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return ServiceResult.NotFound();
        }

        dbContext.Documents.Remove(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Concurrency conflict while deleting {DocumentId}", id);
            return ServiceResult.NotFound();
        }

        logger.LogInformation("Document deleted {DocumentId}", entity.Id);
        return ServiceResult.Success();
    }

    // Returneaza shares (permissions + invites) pentru owner.
    public async Task<ServiceResult<IReadOnlyCollection<DocumentShareItemDto>>> GetSharesForUserAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<IReadOnlyCollection<DocumentShareItemDto>>.NotFound();
        }

        if (!access.CanManageShares)
        {
            return ServiceResult<IReadOnlyCollection<DocumentShareItemDto>>.Forbidden("Only owner can manage shares.");
        }

        await MarkExpiredInvitesForDocumentAsync(id, cancellationToken);

        var permissionItems = await dbContext.DocumentPermissions
            .AsNoTracking()
            .Where(x => x.DocumentId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new DocumentShareItemDto
            {
                Id = x.Id,
                ShareType = "permission",
                Subject = x.UserId,
                Role = x.Role.ToString(),
                Status = "Accepted",
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var inviteItems = await dbContext.DocumentInvites
            .AsNoTracking()
            .Where(x => x.DocumentId == id &&
                        x.Status != DocumentInviteStatus.Revoked &&
                        x.Status != DocumentInviteStatus.Declined)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new DocumentShareItemDto
            {
                Id = x.Id,
                ShareType = "invite",
                Subject = x.InviteeEmailNormalized,
                Role = x.Role.ToString(),
                Status = x.Status.ToString(),
                CreatedAtUtc = x.CreatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc,
                AcceptedAtUtc = x.AcceptedAtUtc
            })
            .ToListAsync(cancellationToken);

        var allItems = permissionItems
            .Concat(inviteItems)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        return ServiceResult<IReadOnlyCollection<DocumentShareItemDto>>.Success(allItems);
    }

    // Creeaza invitatie noua (sau reactualizeaza una existenta) pentru owner.
    public async Task<ServiceResult<DocumentShareItemDto>> CreateShareAsync(string userId, Guid id, CreateShareRequest request, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<DocumentShareItemDto>.NotFound();
        }

        if (!access.CanManageShares)
        {
            return ServiceResult<DocumentShareItemDto>.Forbidden("Only owner can create shares.");
        }

        if (!TryParseShareRole(request.Role, out var parsedRole))
        {
            return ServiceResult<DocumentShareItemDto>.ValidationError("Role must be Viewer or Editor.");
        }

        var normalizedEmail = NormalizeEmail(request.InviteeEmail);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return ServiceResult<DocumentShareItemDto>.ValidationError("Invitee email is required.");
        }

        var utcNow = DateTime.UtcNow;
        var existingInvite = await dbContext.DocumentInvites
            .FirstOrDefaultAsync(x => x.DocumentId == id && x.InviteeEmailNormalized == normalizedEmail, cancellationToken);

        if (existingInvite is null)
        {
            existingInvite = new DocumentInviteEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = id,
                InviteeEmailNormalized = normalizedEmail,
                Role = parsedRole,
                Status = DocumentInviteStatus.Pending,
                CreatedByUserId = userId,
                CreatedAtUtc = utcNow,
                ExpiresAtUtc = utcNow.Add(InviteLifetime)
            };

            dbContext.DocumentInvites.Add(existingInvite);
        }
        else
        {
            existingInvite.Role = parsedRole;
            existingInvite.Status = DocumentInviteStatus.Pending;
            existingInvite.CreatedByUserId = userId;
            existingInvite.CreatedAtUtc = utcNow;
            existingInvite.AcceptedByUserId = null;
            existingInvite.AcceptedAtUtc = null;
            existingInvite.ExpiresAtUtc = utcNow.Add(InviteLifetime);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<DocumentShareItemDto>.Success(new DocumentShareItemDto
        {
            Id = existingInvite.Id,
            ShareType = "invite",
            Subject = existingInvite.InviteeEmailNormalized,
            Role = existingInvite.Role.ToString(),
            Status = existingInvite.Status.ToString(),
            CreatedAtUtc = existingInvite.CreatedAtUtc,
            ExpiresAtUtc = existingInvite.ExpiresAtUtc
        });
    }

    // Revoca un permission sau un invite dupa id; doar owner are drept.
    public async Task<ServiceResult> DeleteShareAsync(string userId, Guid id, Guid shareId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult.NotFound();
        }

        if (!access.CanManageShares)
        {
            return ServiceResult.Forbidden("Only owner can revoke shares.");
        }

        var permission = await dbContext.DocumentPermissions
            .FirstOrDefaultAsync(x => x.DocumentId == id && x.Id == shareId, cancellationToken);

        if (permission is not null)
        {
            dbContext.DocumentPermissions.Remove(permission);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }

        var invite = await dbContext.DocumentInvites
            .FirstOrDefaultAsync(x => x.DocumentId == id && x.Id == shareId, cancellationToken);

        if (invite is null)
        {
            return ServiceResult.NotFound();
        }

        invite.Status = DocumentInviteStatus.Revoked;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    // Listeaza invitatiile pending pentru email-ul userului.
    public async Task<IReadOnlyCollection<IncomingInviteDto>> GetIncomingInvitesAsync(string userId, string userEmail, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return [];
        }

        await MarkExpiredInvitesForEmailAsync(normalizedEmail, cancellationToken);

        return await dbContext.DocumentInvites
            .AsNoTracking()
            .Where(x => x.InviteeEmailNormalized == normalizedEmail && x.Status == DocumentInviteStatus.Pending)
            .Join(
                dbContext.Documents.AsNoTracking(),
                invite => invite.DocumentId,
                document => document.Id,
                (invite, document) => new IncomingInviteDto
                {
                    InviteId = invite.Id,
                    DocumentId = document.Id,
                    DocumentTitle = document.Title,
                    Role = invite.Role.ToString(),
                    Status = invite.Status.ToString(),
                    CreatedAtUtc = invite.CreatedAtUtc,
                    ExpiresAtUtc = invite.ExpiresAtUtc,
                    CreatedByUserId = invite.CreatedByUserId
                })
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    // Accepta invitatia: status Accepted + upsert permission.
    public async Task<ServiceResult> AcceptInviteAsync(string userId, string userEmail, Guid inviteId, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return ServiceResult.ValidationError("User email is required.");
        }

        var invite = await dbContext.DocumentInvites
            .FirstOrDefaultAsync(x => x.Id == inviteId && x.InviteeEmailNormalized == normalizedEmail, cancellationToken);

        if (invite is null)
        {
            return ServiceResult.NotFound();
        }

        if (invite.Status != DocumentInviteStatus.Pending)
        {
            return ServiceResult.ValidationError("Invite is no longer pending.");
        }

        if (invite.ExpiresAtUtc <= DateTime.UtcNow)
        {
            invite.Status = DocumentInviteStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.ValidationError("Invite has expired.");
        }

        var document = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == invite.DocumentId, cancellationToken);

        if (document is null)
        {
            return ServiceResult.NotFound();
        }

        if (document.OwnerUserId != userId)
        {
            await UpsertPermissionAsync(invite.DocumentId, userId, invite.Role, invite.CreatedByUserId, cancellationToken);
        }

        invite.Status = DocumentInviteStatus.Accepted;
        invite.AcceptedByUserId = userId;
        invite.AcceptedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    // Refuza invitatia: status Declined.
    public async Task<ServiceResult> DeclineInviteAsync(string userId, string userEmail, Guid inviteId, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return ServiceResult.ValidationError("User email is required.");
        }

        var invite = await dbContext.DocumentInvites
            .FirstOrDefaultAsync(x => x.Id == inviteId && x.InviteeEmailNormalized == normalizedEmail, cancellationToken);

        if (invite is null)
        {
            return ServiceResult.NotFound();
        }

        if (invite.Status != DocumentInviteStatus.Pending)
        {
            return ServiceResult.ValidationError("Invite is no longer pending.");
        }

        if (invite.ExpiresAtUtc <= DateTime.UtcNow)
        {
            invite.Status = DocumentInviteStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.ValidationError("Invite has expired.");
        }

        invite.Status = DocumentInviteStatus.Declined;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    // Convertește toate pending invites ale email-ului in permissions active.
    public async Task<int> SyncPendingInvitesAsync(string userId, string userEmail, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return 0;
        }

        var utcNow = DateTime.UtcNow;
        var pendingInvites = await dbContext.DocumentInvites
            .Where(x => x.InviteeEmailNormalized == normalizedEmail && x.Status == DocumentInviteStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingInvites.Count == 0)
        {
            return 0;
        }

        var documentIds = pendingInvites.Select(x => x.DocumentId).Distinct().ToList();
        var documents = await dbContext.Documents
            .Where(x => documentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var existingPermissions = await dbContext.DocumentPermissions
            .Where(x => x.UserId == userId && documentIds.Contains(x.DocumentId))
            .ToDictionaryAsync(x => x.DocumentId, cancellationToken);

        var syncedCount = 0;
        foreach (var invite in pendingInvites)
        {
            if (invite.ExpiresAtUtc <= utcNow)
            {
                invite.Status = DocumentInviteStatus.Expired;
                continue;
            }

            if (!documents.TryGetValue(invite.DocumentId, out var document))
            {
                invite.Status = DocumentInviteStatus.Revoked;
                continue;
            }

            if (document.OwnerUserId != userId)
            {
                if (existingPermissions.TryGetValue(invite.DocumentId, out var permission))
                {
                    permission.Role = invite.Role;
                }
                else
                {
                    permission = new DocumentPermissionEntity
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = invite.DocumentId,
                        UserId = userId,
                        Role = invite.Role,
                        GrantedByUserId = invite.CreatedByUserId,
                        CreatedAtUtc = utcNow
                    };
                    dbContext.DocumentPermissions.Add(permission);
                    existingPermissions[invite.DocumentId] = permission;
                }
            }

            invite.Status = DocumentInviteStatus.Accepted;
            invite.AcceptedByUserId = userId;
            invite.AcceptedAtUtc = utcNow;
            syncedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return syncedCount;
    }

    // Stage 7: intoarce starea realtime curenta (revizie + delta + html mirror).
    public async Task<ServiceResult<RealtimeStateDto>> GetRealtimeStateAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || !access.CanRead)
        {
            return ServiceResult<RealtimeStateDto>.NotFound();
        }

        var document = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return ServiceResult<RealtimeStateDto>.NotFound();
        }

        return ServiceResult<RealtimeStateDto>.Success(new RealtimeStateDto
        {
            DocumentId = document.Id,
            Revision = document.LiveRevision,
            Title = document.Title,
            ContentDeltaJson = NormalizeDeltaOrNull(document.ContentDeltaJson) ?? EmptyDeltaJson(),
            ContentHtml = document.Content,
            UpdatedAtUtc = document.UpdatedAt,
            AccessRole = access.AccessRole.ToString()
        });
    }

    // Stage 7: accepta o operatie Quill Delta, o transforma fata de operatiile lipsa si o persista.
    public async Task<ServiceResult<SubmitRealtimeOperationResponseDto>> SubmitRealtimeOperationAsync(
        string userId,
        Guid id,
        SubmitRealtimeOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BaseRevision < 0)
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.ValidationError("Base revision cannot be negative.");
        }

        var normalizedClientOpId = (request.ClientOpId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedClientOpId))
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.ValidationError("Client operation id is required.");
        }

        var parsedIncoming = TryParseDelta(request.DeltaJson, out var incomingOps, out var parseError);
        if (!parsedIncoming)
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.ValidationError(parseError ?? "Invalid delta payload.");
        }

        if (incomingOps.Count == 0)
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.ValidationError("Delta operation cannot be empty.");
        }

        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.NotFound();
        }

        if (!access.CanEdit)
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.Forbidden("Only owner/editor can submit realtime operations.");
        }

        var existingByClientOpId = await dbContext.DocumentOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DocumentId == id &&
                     x.UserId == userId &&
                     x.ClientOpId == normalizedClientOpId,
                cancellationToken);

        if (existingByClientOpId is not null)
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.Success(new SubmitRealtimeOperationResponseDto
            {
                AcceptedRevision = existingByClientOpId.Revision,
                TransformedDeltaJson = existingByClientOpId.DeltaJson,
                UpdatedAtUtc = existingByClientOpId.CreatedAtUtc,
                AcceptedByUserId = existingByClientOpId.UserId
            });
        }

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.NotFound();
        }

        var currentRevision = document.LiveRevision;
        if (request.BaseRevision > currentRevision)
        {
            return ServiceResult<SubmitRealtimeOperationResponseDto>.Conflict("Base revision is ahead of server revision. Resync is required.");
        }

        var operationsAfterBase = await dbContext.DocumentOperations
            .AsNoTracking()
            .Where(x => x.DocumentId == id && x.Revision > request.BaseRevision)
            .OrderBy(x => x.Revision)
            .ToListAsync(cancellationToken);

        var transformedIncoming = incomingOps;
        foreach (var operation in operationsAfterBase)
        {
            if (!TryParseDelta(operation.DeltaJson, out var existingOps, out _))
            {
                logger.LogWarning("Skipping invalid stored delta operation {OperationId}", operation.Id);
                continue;
            }

            transformedIncoming = TransformOtherAgainstBase(existingOps, transformedIncoming, baseHasPriority: true);
        }

        var utcNow = DateTime.UtcNow;
        var transformedDeltaJson = SerializeDelta(transformedIncoming);
        var nextRevision = currentRevision + 1;

        var newOperation = new DocumentOperationEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = id,
            Revision = nextRevision,
            UserId = userId,
            ClientOpId = normalizedClientOpId,
            DeltaJson = transformedDeltaJson,
            CreatedAtUtc = utcNow
        };

        dbContext.DocumentOperations.Add(newOperation);
        document.LiveRevision = nextRevision;
        document.ContentDeltaJson = transformedDeltaJson;
        document.UpdatedAt = utcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Conflict while saving realtime op for document {DocumentId}", id);
            return ServiceResult<SubmitRealtimeOperationResponseDto>.Conflict("Realtime conflict detected. Please request a resync.");
        }

        return ServiceResult<SubmitRealtimeOperationResponseDto>.Success(new SubmitRealtimeOperationResponseDto
        {
            AcceptedRevision = newOperation.Revision,
            TransformedDeltaJson = transformedDeltaJson,
            UpdatedAtUtc = utcNow,
            AcceptedByUserId = userId
        });
    }

    // Stage 7: replay operatiile dupa revizia ceruta.
    public async Task<ServiceResult<IReadOnlyCollection<RealtimeOperationDto>>> ListRealtimeOperationsAsync(
        string userId,
        Guid id,
        long afterRevision,
        CancellationToken cancellationToken)
    {
        if (afterRevision < 0)
        {
            return ServiceResult<IReadOnlyCollection<RealtimeOperationDto>>.ValidationError("afterRevision cannot be negative.");
        }

        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || !access.CanRead)
        {
            return ServiceResult<IReadOnlyCollection<RealtimeOperationDto>>.NotFound();
        }

        var exists = await dbContext.Documents
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!exists)
        {
            return ServiceResult<IReadOnlyCollection<RealtimeOperationDto>>.NotFound();
        }

        var operations = await dbContext.DocumentOperations
            .AsNoTracking()
            .Where(x => x.DocumentId == id && x.Revision > afterRevision)
            .OrderBy(x => x.Revision)
            .Select(x => new RealtimeOperationDto
            {
                Revision = x.Revision,
                UserId = x.UserId,
                DeltaJson = x.DeltaJson,
                CreatedAtUtc = x.CreatedAtUtc,
                ClientOpId = x.ClientOpId
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<RealtimeOperationDto>>.Success(operations);
    }

    // Stage 7: salveaza snapshot-ul HTML mirror doar daca revizia corespunde.
    public async Task<ServiceResult<SaveRealtimeHtmlSnapshotResponseDto>> SaveRealtimeHtmlSnapshotAsync(
        string userId,
        Guid id,
        SaveRealtimeHtmlSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Revision < 0)
        {
            return ServiceResult<SaveRealtimeHtmlSnapshotResponseDto>.ValidationError("Revision cannot be negative.");
        }

        var access = await accessService.GetAccessContextAsync(id, userId, cancellationToken);
        if (access is null || access.AccessRole == DocumentAccessRole.None)
        {
            return ServiceResult<SaveRealtimeHtmlSnapshotResponseDto>.NotFound();
        }

        if (!access.CanEdit)
        {
            return ServiceResult<SaveRealtimeHtmlSnapshotResponseDto>.Forbidden("Only owner/editor can save html snapshots.");
        }

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return ServiceResult<SaveRealtimeHtmlSnapshotResponseDto>.NotFound();
        }

        if (document.LiveRevision != request.Revision)
        {
            return ServiceResult<SaveRealtimeHtmlSnapshotResponseDto>.Conflict("Snapshot revision is stale. Reload realtime state.");
        }

        document.Content = SanitizeContent(request.ContentHtml);
        document.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SaveRealtimeHtmlSnapshotResponseDto>.Success(new SaveRealtimeHtmlSnapshotResponseDto
        {
            Revision = document.LiveRevision,
            UpdatedAtUtc = document.UpdatedAt
        });
    }

    // Curata HTML-ul primit din editor pentru a bloca script-uri periculoase.
    private static string SanitizeContent(string? rawHtml)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return string.Empty;
        }

        return HtmlSanitizer.Sanitize(rawHtml);
    }

    // Configureaza sanitizer-ul astfel incat Quill sa isi poata pastra clasele utile.
    private static HtmlSanitizer BuildHtmlSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("class");
        return sanitizer;
    }

    // Normalizeaza JSON-ul delta (curata whitespace si valideaza structura minima).
    private static string? NormalizeDeltaOrNull(string? rawDeltaJson)
    {
        if (string.IsNullOrWhiteSpace(rawDeltaJson))
        {
            return null;
        }

        if (!TryParseDelta(rawDeltaJson, out var ops, out _))
        {
            return null;
        }

        return SerializeDelta(ops);
    }

    // Delta gol valid pentru Quill.
    private static string EmptyDeltaJson() => "{\"ops\":[]}";

    // Parseaza payload-ul quill delta intr-o forma interna simplificata (insert/retain/delete).
    private static bool TryParseDelta(string? deltaJson, out List<DeltaOp> operations, out string? error)
    {
        operations = [];
        error = null;

        if (string.IsNullOrWhiteSpace(deltaJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(deltaJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("ops", out var opsElement) ||
                opsElement.ValueKind != JsonValueKind.Array)
            {
                error = "Delta must contain an ops array.";
                return false;
            }

            foreach (var opElement in opsElement.EnumerateArray())
            {
                if (opElement.ValueKind != JsonValueKind.Object)
                {
                    error = "Each operation must be an object.";
                    return false;
                }

                var hasInsert = opElement.TryGetProperty("insert", out var insertElement);
                var hasDelete = opElement.TryGetProperty("delete", out var deleteElement);
                var hasRetain = opElement.TryGetProperty("retain", out var retainElement);
                var typeCount = (hasInsert ? 1 : 0) + (hasDelete ? 1 : 0) + (hasRetain ? 1 : 0);
                if (typeCount != 1)
                {
                    error = "Each operation must define exactly one of insert/delete/retain.";
                    return false;
                }

                JsonElement? attributes = null;
                if (opElement.TryGetProperty("attributes", out var attributesElement))
                {
                    attributes = attributesElement.Clone();
                }

                if (hasInsert)
                {
                    if (insertElement.ValueKind == JsonValueKind.String)
                    {
                        operations.Add(DeltaOp.Insert(insertElement.GetString() ?? string.Empty, attributes));
                    }
                    else
                    {
                        operations.Add(DeltaOp.InsertObject(insertElement.Clone(), attributes));
                    }

                    continue;
                }

                if (hasDelete)
                {
                    if (deleteElement.ValueKind != JsonValueKind.Number || !deleteElement.TryGetInt32(out var deleteLength) || deleteLength <= 0)
                    {
                        error = "Delete operations must be positive integers.";
                        return false;
                    }

                    operations.Add(DeltaOp.Delete(deleteLength));
                    continue;
                }

                if (retainElement.ValueKind != JsonValueKind.Number || !retainElement.TryGetInt32(out var retainLength) || retainLength <= 0)
                {
                    error = "Retain operations must be positive integers.";
                    return false;
                }

                operations.Add(DeltaOp.Retain(retainLength, attributes));
            }

            return true;
        }
        catch (JsonException)
        {
            error = "Delta payload is not valid JSON.";
            return false;
        }
    }

    // Serializeaza lista interna de operatii inapoi la formatul standard Quill delta.
    private static string SerializeDelta(IReadOnlyCollection<DeltaOp> operations)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WritePropertyName("ops");
        writer.WriteStartArray();

        foreach (var operation in operations)
        {
            writer.WriteStartObject();

            switch (operation.Kind)
            {
                case DeltaOpKind.Insert:
                    writer.WritePropertyName("insert");
                    if (operation.InsertText is not null)
                    {
                        writer.WriteStringValue(operation.InsertText);
                    }
                    else
                    {
                        operation.InsertEmbed!.Value.WriteTo(writer);
                    }
                    break;

                case DeltaOpKind.Delete:
                    writer.WriteNumber("delete", operation.Length);
                    break;

                case DeltaOpKind.Retain:
                    writer.WriteNumber("retain", operation.Length);
                    break;
            }

            if (operation.Attributes is { ValueKind: JsonValueKind.Object } attributes &&
                attributes.EnumerateObject().Any())
            {
                writer.WritePropertyName("attributes");
                attributes.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // Transforma operatiile "other" impotriva "base" (algoritm OT simplificat Quill Delta).
    private static List<DeltaOp> TransformOtherAgainstBase(
        IReadOnlyList<DeltaOp> baseOperations,
        IReadOnlyList<DeltaOp> otherOperations,
        bool baseHasPriority)
    {
        var baseIterator = new DeltaOpIterator(baseOperations);
        var otherIterator = new DeltaOpIterator(otherOperations);
        var result = new List<DeltaOp>();

        while (baseIterator.HasNext || otherIterator.HasNext)
        {
            var baseType = baseIterator.PeekKind();
            var otherType = otherIterator.PeekKind();

            if (baseType == DeltaOpKind.Insert && (baseHasPriority || otherType != DeltaOpKind.Insert))
            {
                result.Add(DeltaOp.Retain(baseIterator.PeekLength(), null));
                baseIterator.Next();
                continue;
            }

            if (otherType == DeltaOpKind.Insert)
            {
                result.Add(otherIterator.Next());
                continue;
            }

            var nextLength = Math.Min(baseIterator.PeekLength(), otherIterator.PeekLength());
            var baseOp = baseIterator.Next(nextLength);
            var otherOp = otherIterator.Next(nextLength);

            if (baseOp.Kind == DeltaOpKind.Delete)
            {
                continue;
            }

            if (otherOp.Kind == DeltaOpKind.Delete)
            {
                result.Add(otherOp);
                continue;
            }

            result.Add(DeltaOp.Retain(nextLength, otherOp.Attributes));
        }

        return CompactOperations(result);
    }

    // Comaseaza operatiile consecutive compatibile pentru payload mai mic.
    private static List<DeltaOp> CompactOperations(IReadOnlyList<DeltaOp> operations)
    {
        var compacted = new List<DeltaOp>();

        foreach (var operation in operations)
        {
            if (operation.IsNoop)
            {
                continue;
            }

            if (compacted.Count == 0)
            {
                compacted.Add(operation);
                continue;
            }

            var previous = compacted[^1];
            if (!CanMerge(previous, operation))
            {
                compacted.Add(operation);
                continue;
            }

            compacted[^1] = Merge(previous, operation);
        }

        return compacted;
    }

    // Decide daca doua operatii consecutive pot fi unite.
    private static bool CanMerge(DeltaOp left, DeltaOp right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        if (left.Kind == DeltaOpKind.Delete)
        {
            return true;
        }

        if (!AttributesEqual(left.Attributes, right.Attributes))
        {
            return false;
        }

        if (left.Kind == DeltaOpKind.Retain)
        {
            return true;
        }

        return left.InsertText is not null && right.InsertText is not null;
    }

    // Uneste doua operatii compatibile intr-una singura.
    private static DeltaOp Merge(DeltaOp left, DeltaOp right)
    {
        return left.Kind switch
        {
            DeltaOpKind.Delete => DeltaOp.Delete(left.Length + right.Length),
            DeltaOpKind.Retain => DeltaOp.Retain(left.Length + right.Length, left.Attributes),
            DeltaOpKind.Insert when left.InsertText is not null && right.InsertText is not null
                => DeltaOp.Insert(left.InsertText + right.InsertText, left.Attributes),
            _ => right
        };
    }

    // Compara atributele pentru a sti daca putem comasa operatiile.
    private static bool AttributesEqual(JsonElement? left, JsonElement? right)
    {
        if (!left.HasValue && !right.HasValue)
        {
            return true;
        }

        if (!left.HasValue || !right.HasValue)
        {
            return false;
        }

        return left.Value.GetRawText() == right.Value.GetRawText();
    }

    private enum DeltaOpKind
    {
        Insert = 0,
        Delete = 1,
        Retain = 2
    }

    // Reprezentarea interna a unei operatii delta.
    private sealed class DeltaOp
    {
        public required DeltaOpKind Kind { get; init; }
        public int Length { get; init; }
        public string? InsertText { get; init; }
        public JsonElement? InsertEmbed { get; init; }
        public JsonElement? Attributes { get; init; }

        public bool IsNoop => (Kind is DeltaOpKind.Delete or DeltaOpKind.Retain) && Length <= 0;

        public static DeltaOp Insert(string text, JsonElement? attributes) => new()
        {
            Kind = DeltaOpKind.Insert,
            Length = text.Length,
            InsertText = text,
            Attributes = attributes
        };

        public static DeltaOp InsertObject(JsonElement embed, JsonElement? attributes) => new()
        {
            Kind = DeltaOpKind.Insert,
            Length = 1,
            InsertEmbed = embed,
            Attributes = attributes
        };

        public static DeltaOp Delete(int length) => new()
        {
            Kind = DeltaOpKind.Delete,
            Length = length
        };

        public static DeltaOp Retain(int length, JsonElement? attributes) => new()
        {
            Kind = DeltaOpKind.Retain,
            Length = length,
            Attributes = attributes
        };

        public DeltaOp Slice(int length)
        {
            return Kind switch
            {
                DeltaOpKind.Delete => Delete(length),
                DeltaOpKind.Retain => Retain(length, Attributes),
                DeltaOpKind.Insert when InsertText is not null => Insert(InsertText[..length], Attributes),
                _ => this
            };
        }
    }

    // Iterator care permite split pe operatii delta pentru algoritmul de transform.
    private sealed class DeltaOpIterator(IReadOnlyList<DeltaOp> operations)
    {
        private int _index;
        private int _offset;

        public bool HasNext => PeekLength() < int.MaxValue;

        public DeltaOpKind PeekKind()
        {
            if (_index >= operations.Count)
            {
                return DeltaOpKind.Retain;
            }

            return operations[_index].Kind;
        }

        public int PeekLength()
        {
            if (_index >= operations.Count)
            {
                return int.MaxValue;
            }

            return operations[_index].Length - _offset;
        }

        public DeltaOp Next(int? requestedLength = null)
        {
            if (_index >= operations.Count)
            {
                var retainLength = requestedLength ?? int.MaxValue;
                return DeltaOp.Retain(retainLength, null);
            }

            var current = operations[_index];
            var remaining = current.Length - _offset;
            var length = requestedLength.HasValue ? Math.Min(requestedLength.Value, remaining) : remaining;
            var slice = Slice(current, _offset, length);

            if (_offset + length >= current.Length)
            {
                _index++;
                _offset = 0;
            }
            else
            {
                _offset += length;
            }

            return slice;
        }

        private static DeltaOp Slice(DeltaOp operation, int offset, int length)
        {
            return operation.Kind switch
            {
                DeltaOpKind.Insert when operation.InsertText is not null
                    => DeltaOp.Insert(operation.InsertText.Substring(offset, length), operation.Attributes),
                DeltaOpKind.Insert
                    => operation,
                DeltaOpKind.Delete
                    => DeltaOp.Delete(length),
                _ => DeltaOp.Retain(length, operation.Attributes)
            };
        }
    }

    // Transforma entitatea EF in DTO-ul expus clientului.
    private static DocumentDto ToDto(DocumentEntity entity, DocumentAccessRole accessRole)
    {
        return new DocumentDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Content = entity.Content,
            ContentDeltaJson = NormalizeDeltaOrNull(entity.ContentDeltaJson) ?? EmptyDeltaJson(),
            LiveRevision = entity.LiveRevision,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            AccessRole = accessRole.ToString(),
            IsOwner = accessRole == DocumentAccessRole.Owner
        };
    }

    // Parseaza rolul de share trimis in request.
    private static bool TryParseShareRole(string rawRole, out DocumentShareRole role)
    {
        role = default;

        if (string.IsNullOrWhiteSpace(rawRole))
        {
            return false;
        }

        return Enum.TryParse(rawRole.Trim(), true, out role) &&
               role is DocumentShareRole.Viewer or DocumentShareRole.Editor;
    }

    // Normalizeaza email-ul pentru comparatii stabile in DB.
    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    // Marcheaza invitatiile expirate pentru un document (din pending -> expired).
    private async Task MarkExpiredInvitesForDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var expired = await dbContext.DocumentInvites
            .Where(x => x.DocumentId == documentId &&
                        x.Status == DocumentInviteStatus.Pending &&
                        x.ExpiresAtUtc <= utcNow)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var invite in expired)
        {
            invite.Status = DocumentInviteStatus.Expired;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Marcheaza invitatiile expirate pentru email-ul utilizatorului.
    private async Task MarkExpiredInvitesForEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var expired = await dbContext.DocumentInvites
            .Where(x => x.InviteeEmailNormalized == normalizedEmail &&
                        x.Status == DocumentInviteStatus.Pending &&
                        x.ExpiresAtUtc <= utcNow)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var invite in expired)
        {
            invite.Status = DocumentInviteStatus.Expired;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Creeaza sau actualizeaza permission-ul pentru un user pe document.
    private async Task UpsertPermissionAsync(
        Guid documentId,
        string userId,
        DocumentShareRole role,
        string grantedByUserId,
        CancellationToken cancellationToken)
    {
        var permission = await dbContext.DocumentPermissions
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.UserId == userId, cancellationToken);

        if (permission is null)
        {
            permission = new DocumentPermissionEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                UserId = userId,
                Role = role,
                GrantedByUserId = grantedByUserId,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.DocumentPermissions.Add(permission);
            return;
        }

        permission.Role = role;
        permission.GrantedByUserId = grantedByUserId;
    }
}
