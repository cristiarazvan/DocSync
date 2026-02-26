namespace GoogDocsLite.Client.Services;

public sealed class ApiUserContext
{
    public required string UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
}
