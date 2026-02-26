using System.Net;
using System.Text.Json;

namespace GoogDocsLite.Client.Services;

public static class ApiErrorHelper
{
    // Verifica raspunsul HTTP; daca este eroare, arunca exceptie cu status + mesaj prietenos.
    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var rawBody = await response.Content.ReadAsStringAsync();
        var details = ExtractProblemMessage(rawBody);

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Session expired or missing user context. Please log in again.",
            HttpStatusCode.Forbidden => details ?? "You do not have permission for this action.",
            HttpStatusCode.BadRequest => details ?? "Some input is invalid. Please check and try again.",
            HttpStatusCode.Conflict => details ?? "Realtime revision conflict. Resync is required.",
            HttpStatusCode.UnprocessableEntity => details ?? "Validation failed. Please check your content and retry.",
            HttpStatusCode.NotFound => "The resource was not found.",
            (HttpStatusCode)423 => details ?? "Document is locked by another active editor.",
            _ => details ?? "Request failed. Please try again."
        };

        throw new ApiRequestException(response.StatusCode, message);
    }

    // Incearca sa extraga campurile uzuale ("message" / "detail" / "title") din JSON.
    private static string? ExtractProblemMessage(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(rawBody);

            if (json.RootElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }

            if (json.RootElement.TryGetProperty("detail", out var detailElement))
            {
                return detailElement.GetString();
            }

            if (json.RootElement.TryGetProperty("title", out var titleElement))
            {
                return titleElement.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
