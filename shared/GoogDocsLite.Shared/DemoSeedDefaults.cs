namespace GoogDocsLite.Shared;

public static class DemoSeedDefaults
{
    public const string SeedPassword = "demo123";

    public const string OwnerUserId = "demo-owner-user";
    public const string OwnerEmail = "owner.demo@googdocs.local";

    public const string EditorUserId = "demo-editor-user";
    public const string EditorEmail = "editor.demo@googdocs.local";

    public const string ViewerUserId = "demo-viewer-user";
    public const string ViewerEmail = "viewer.demo@googdocs.local";

    public const string OutsiderUserId = "demo-outsider-user";
    public const string OutsiderEmail = "outsider.demo@googdocs.local";

    public const string PendingInviteEmail = "pending.demo@googdocs.local";

    public static readonly Guid PrivateDocId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SharedEditorDocId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SharedViewerDocId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid PendingInviteDocId = Guid.Parse("44444444-4444-4444-4444-444444444444");
}
