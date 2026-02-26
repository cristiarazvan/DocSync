using Microsoft.AspNetCore.Mvc;

namespace GoogDocsLite.Server.Controllers;

public abstract class InternalApiControllerBase(IConfiguration configuration) : ControllerBase
{
    private readonly string _internalApiKey = configuration[InternalApiKeyHeaderName] ?? "dev-internal-key-change-me";

    protected const string UserIdHeaderName = "X-User-Id";
    protected const string InternalApiKeyHeaderName = "InternalApiKey";
    protected const string InternalApiKeyWireHeaderName = "X-Internal-Api-Key";
    protected const string UserEmailHeaderName = "X-User-Email";
    protected const string UserDisplayNameHeaderName = "X-User-Display-Name";

    // Valideaza cheia interna si extrage contextul userului din headere.
    protected bool TryReadUserContext(out InternalApiUserContext userContext, bool requireEmail = false)
    {
        userContext = new InternalApiUserContext
        {
            UserId = string.Empty,
            Email = null,
            DisplayName = null
        };

        if (!Request.Headers.TryGetValue(InternalApiKeyWireHeaderName, out var apiKeyValues))
        {
            return false;
        }

        var providedApiKey = apiKeyValues.FirstOrDefault()?.Trim();
        if (!string.Equals(providedApiKey, _internalApiKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(UserIdHeaderName, out var userIdValues))
        {
            return false;
        }

        var userId = userIdValues.FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var email = Request.Headers.TryGetValue(UserEmailHeaderName, out var emailValues)
            ? emailValues.FirstOrDefault()?.Trim()
            : null;

        if (requireEmail && string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var displayName = Request.Headers.TryGetValue(UserDisplayNameHeaderName, out var displayNameValues)
            ? displayNameValues.FirstOrDefault()?.Trim()
            : null;

        userContext = new InternalApiUserContext
        {
            UserId = userId,
            Email = email,
            DisplayName = displayName
        };

        return true;
    }
}

public sealed class InternalApiUserContext
{
    public required string UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
}
