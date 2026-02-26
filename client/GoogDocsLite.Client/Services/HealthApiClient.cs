using GoogDocsLite.Client.Models.Api;
using System.Net.Http.Json;

namespace GoogDocsLite.Client.Services;

public class HealthApiClient(HttpClient httpClient)
{
    // Apeleaza endpoint-ul de health pentru a verifica daca API-ul ruleaza corect.
    public async Task<HealthResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/health", cancellationToken);
        await ApiErrorHelper.EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken))!;
    }
}
