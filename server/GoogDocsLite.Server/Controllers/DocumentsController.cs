using GoogDocsLite.Server.Application.Services;
using GoogDocsLite.Server.Contracts.Documents;
using Microsoft.AspNetCore.Mvc;

namespace GoogDocsLite.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController(
    IDocumentService documentService,
    IDocumentLockService documentLockService,
    ILogger<DocumentsController> logger,
    IConfiguration configuration) : InternalApiControllerBase(configuration)
{
    // GET /api/documents?view=owned|shared|all - lista documentelor accesibile userului.
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<DocumentListItemDto>>> List(
        [FromQuery] string view = "all",
        CancellationToken cancellationToken = default)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var documents = await documentService.ListForUserAsync(userContext.UserId, view, cancellationToken);
        return Ok(documents);
    }

    // GET /api/documents/{id} - document complet, daca userul are acces.
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.GetForUserAsync(userContext.UserId, id, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to load document." })
        };
    }

    // POST /api/documents - creeaza document nou pentru userul curent.
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentDto>> Create(
        [FromBody] CreateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var created = await documentService.CreateForUserAsync(userContext.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    // PUT /api/documents/{id} - update permis pentru owner/editor.
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentDto>> Update(
        Guid id,
        [FromBody] UpdateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.UpdateForUserAsync(userContext.UserId, id, request, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to update document." })
        };
    }

    // PATCH /api/documents/{id}/live-content - stage 6 live stream patch (lock owner-only).
    [HttpPatch("{id:guid}/live-content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(423)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LiveContentPatchResponseDto>> LiveContentPatch(
        Guid id,
        [FromBody] ApplyLiveContentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.ApplyLiveContentAsync(userContext.UserId, id, request, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." }),
            ServiceResultType.Locked => StatusCode(423, new { message = result.ErrorMessage ?? "Active lock required." }),
            ServiceResultType.ValidationError => UnprocessableEntity(new { message = result.ErrorMessage ?? "Validation failed." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to patch live content." })
        };
    }

    // GET /api/documents/{id}/realtime/state - snapshot complet pentru resync.
    [HttpGet("{id:guid}/realtime/state")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RealtimeStateDto>> RealtimeState(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.GetRealtimeStateAsync(userContext.UserId, id, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to read realtime state." })
        };
    }

    // POST /api/documents/{id}/realtime/ops - submit op cu base revision.
    [HttpPost("{id:guid}/realtime/ops")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SubmitRealtimeOperationResponseDto>> SubmitRealtimeOperation(
        Guid id,
        [FromBody] SubmitRealtimeOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.SubmitRealtimeOperationAsync(userContext.UserId, id, request, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." }),
            ServiceResultType.Conflict => Conflict(new { message = result.ErrorMessage ?? "Realtime conflict." }),
            ServiceResultType.ValidationError => UnprocessableEntity(new { message = result.ErrorMessage ?? "Validation failed." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to submit realtime operation." })
        };
    }

    // GET /api/documents/{id}/realtime/ops?afterRevision=n - replay pentru gap-uri de revizie.
    [HttpGet("{id:guid}/realtime/ops")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<RealtimeOperationDto>>> RealtimeOperations(
        Guid id,
        [FromQuery] long afterRevision = 0,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.ListRealtimeOperationsAsync(userContext.UserId, id, afterRevision, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.ValidationError => UnprocessableEntity(new { message = result.ErrorMessage ?? "Validation failed." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to list realtime operations." })
        };
    }

    // PUT /api/documents/{id}/realtime/html-snapshot - persista HTML mirror cu revision guard.
    [HttpPut("{id:guid}/realtime/html-snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SaveRealtimeHtmlSnapshotResponseDto>> SaveRealtimeHtmlSnapshot(
        Guid id,
        [FromBody] SaveRealtimeHtmlSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.SaveRealtimeHtmlSnapshotAsync(userContext.UserId, id, request, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." }),
            ServiceResultType.Conflict => Conflict(new { message = result.ErrorMessage ?? "Realtime conflict." }),
            ServiceResultType.ValidationError => UnprocessableEntity(new { message = result.ErrorMessage ?? "Validation failed." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to save realtime html snapshot." })
        };
    }

    // DELETE /api/documents/{id} - owner-only.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.DeleteForUserAsync(userContext.UserId, id, cancellationToken);
        if (result.Type == ServiceResultType.NotFound)
        {
            return NotFound();
        }

        if (result.Type == ServiceResultType.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." });
        }

        logger.LogInformation("Deleted document {DocumentId}", id);
        return NoContent();
    }

    // GET /api/documents/{id}/shares - ownerul vede permissions + invites.
    [HttpGet("{id:guid}/shares")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<DocumentShareItemDto>>> GetShares(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.GetSharesForUserAsync(userContext.UserId, id, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to load shares." })
        };
    }

    // POST /api/documents/{id}/shares - ownerul trimite invite pe email.
    [HttpPost("{id:guid}/shares")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentShareItemDto>> CreateShare(
        Guid id,
        [FromBody] CreateShareRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.CreateShareAsync(userContext.UserId, id, request, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." }),
            ServiceResultType.ValidationError => BadRequest(new { message = result.ErrorMessage ?? "Validation failed." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to create invite." })
        };
    }

    // DELETE /api/documents/{id}/shares/{permissionOrInviteId} - revocare share.
    [HttpDelete("{id:guid}/shares/{permissionOrInviteId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteShare(Guid id, Guid permissionOrInviteId, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentService.DeleteShareAsync(userContext.UserId, id, permissionOrInviteId, cancellationToken);
        if (result.Type == ServiceResultType.NotFound)
        {
            return NotFound();
        }

        if (result.Type == ServiceResultType.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." });
        }

        return NoContent();
    }

    // GET /api/documents/{id}/lock - status lock curent.
    [HttpGet("{id:guid}/lock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentLockDto>> GetLock(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentLockService.GetLockAsync(userContext.UserId, id, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to read lock state." })
        };
    }

    // POST /api/documents/{id}/lock/acquire - cere lock de editare.
    [HttpPost("{id:guid}/lock/acquire")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(423)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentLockDto>> AcquireLock(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var displayName = !string.IsNullOrWhiteSpace(userContext.DisplayName)
            ? userContext.DisplayName!
            : !string.IsNullOrWhiteSpace(userContext.Email)
                ? userContext.Email!
                : userContext.UserId;

        var result = await documentLockService.AcquireLockAsync(userContext.UserId, displayName, id, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." }),
            ServiceResultType.Locked => StatusCode(423, new { message = result.ErrorMessage ?? "Document already locked." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to acquire lock." })
        };
    }

    // POST /api/documents/{id}/lock/heartbeat - mentine lock activ.
    [HttpPost("{id:guid}/lock/heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(423)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentLockDto>> Heartbeat(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentLockService.HeartbeatLockAsync(userContext.UserId, id, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage ?? "Forbidden." }),
            ServiceResultType.Locked => StatusCode(423, new { message = result.ErrorMessage ?? "Lock is not owned by current user." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to heartbeat lock." })
        };
    }

    // POST /api/documents/{id}/lock/release - elibereaza lock-ul de editare.
    [HttpPost("{id:guid}/lock/release")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(423)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentLockDto>> ReleaseLock(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadUserContext(out var userContext))
        {
            return Unauthorized(new { message = "Missing user context." });
        }

        var result = await documentLockService.ReleaseLockAsync(userContext.UserId, id, cancellationToken);
        return result.Type switch
        {
            ServiceResultType.Success => Ok(result.Value),
            ServiceResultType.NotFound => NotFound(),
            ServiceResultType.Locked => StatusCode(423, new { message = result.ErrorMessage ?? "Lock belongs to another user." }),
            _ => BadRequest(new { message = result.ErrorMessage ?? "Unable to release lock." })
        };
    }
}
