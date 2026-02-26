namespace GoogDocsLite.Server.Contracts.Health;

public class HealthResponse
{
    public string Status { get; init; } = "ok";
    public DateTime UtcTime { get; init; } = DateTime.UtcNow;
}
