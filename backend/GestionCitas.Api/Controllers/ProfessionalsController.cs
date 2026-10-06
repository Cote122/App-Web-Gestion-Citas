using GestionCitas.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/professionals")]
[Authorize]
public class ProfessionalsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid specialtyId)
    {
        var data = await db.Professionals
            .AsNoTracking()
            .Where(x => x.IsActive && x.Specialty.IsActive && x.SpecialtyId == specialtyId)
            .OrderBy(x => x.FullName)
            .Select(x => new { x.Id, x.FullName, x.Office, x.SpecialtyId })
            .ToListAsync();

        return Ok(data);
    }
}
