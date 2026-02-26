using GoogDocsLite.Server.Contracts.Health;
using Microsoft.AspNetCore.Mvc;

namespace GoogDocsLite.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    // Endpoint simplu de health-check folosit de client si de Docker smoke test.
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        return Ok(new HealthResponse
        {
            Status = "ok",
            UtcTime = DateTime.UtcNow
        });
    }
}
