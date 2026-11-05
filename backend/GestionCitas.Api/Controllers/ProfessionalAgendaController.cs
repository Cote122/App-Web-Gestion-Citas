using System.Security.Claims;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionCitas.Api.Controllers;

/// <summary>HU-39 y HU-41: consulta y gestión de agenda para el profesional autenticado.</summary>
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

    /// <summary>HU-41: Obtiene los slots disponibles del profesional para reprogramación.</summary>
    [HttpGet("available-slots")]
    public async Task<IActionResult> GetAvailableSlots()
    {
        var (slots, error) = await agendaService.GetAvailableSlotsAsync(CurrentUserId());
        return error is null ? Ok(slots) : StatusCode(StatusCodes.Status403Forbidden, new { message = error });
    }

    /// <summary>HU-41: Cancela una cita y libera su cupo.</summary>
    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var (success, error) = await agendaService.CancelAppointmentAsync(CurrentUserId(), id);
        if (success) return Ok(new { message = "Cita cancelada con éxito." });
        if (error == "Cita no encontrada o no te pertenece.") return NotFound(new { message = error });
        return BadRequest(new { message = error });
    }

    /// <summary>HU-41: Reprograma una cita a un nuevo slot.</summary>
    [HttpPatch("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentRequest dto)
    {
        var (success, error) = await agendaService.RescheduleAppointmentAsync(CurrentUserId(), id, dto.SlotId);
        if (success) return Ok(new { message = "Cita reprogramada con éxito." });
        if (error == "Cita no encontrada o no te pertenece.") return NotFound(new { message = error });
        return BadRequest(new { message = error });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}