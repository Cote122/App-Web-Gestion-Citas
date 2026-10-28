using GestionCitas.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/availability")]
[Authorize]
public class AvailabilityController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid professionalId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        var start = from ?? DateTimeOffset.UtcNow;
        var end = to ?? start.AddDays(30);

        var data = await db.AvailabilitySlots
            .AsNoTracking()
            .Where(x => x.ProfessionalId == professionalId && x.StartAt >= start && x.StartAt <= end)
            // HU-11: no mostrar agenda de profesionales o especialidades desactivados.
            .Where(x => x.Professional.IsActive && x.Professional.Specialty.IsActive)
            .OrderBy(x => x.StartAt)
            .Select(x => new
            {
                x.Id,
                x.StartAt,
                x.EndAt,
                x.IsAvailable
            })
            .ToListAsync();

        return Ok(data);
    }
}
