using System.Net;

namespace GoogDocsLite.Client.Services;

public class ApiRequestException(HttpStatusCode statusCode, string message) : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
