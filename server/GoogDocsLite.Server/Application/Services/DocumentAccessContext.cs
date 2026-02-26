using GoogDocsLite.Server.Data.Entities;

namespace GoogDocsLite.Server.Application.Services;

public sealed class DocumentAccessContext
{
    public required DocumentEntity Document { get; init; }
    public required DocumentAccessRole AccessRole { get; init; }

    public bool CanRead => AccessRole != DocumentAccessRole.None;
    public bool CanEdit => AccessRole is DocumentAccessRole.Owner or DocumentAccessRole.Editor;
    public bool CanManageShares => AccessRole == DocumentAccessRole.Owner;
    public bool CanDelete => AccessRole == DocumentAccessRole.Owner;
}
