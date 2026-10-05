using Microsoft.AspNetCore.Mvc;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", service = "GestionCitas.Api", utc = DateTimeOffset.UtcNow });
}
