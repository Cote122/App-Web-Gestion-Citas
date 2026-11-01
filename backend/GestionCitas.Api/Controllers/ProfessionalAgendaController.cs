using System.Security.Claims;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionCitas.Api.Controllers;

/// <summary>HU-39: consulta de pacientes agendados para el profesional autenticado.</summary>
[ApiController]
[Route("api/professional/agenda")]
[Authorize(Roles = "Profesional")]
public class ProfessionalAgendaController(ProfessionalAgendaService agendaService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var (items, error) = await agendaService.ListAsync(CurrentUserId());
        return error is null ? Ok(items) : StatusCode(StatusCodes.Status403Forbidden, new { message = error });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var (detail, error) = await agendaService.DetailAsync(CurrentUserId(), id);
        if (detail is not null) return Ok(detail);
        if (error == "Cita no encontrada.") return NotFound(new { message = error });
        return StatusCode(StatusCodes.Status403Forbidden, new { message = error });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
