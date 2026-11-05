using System.Security.Claims;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionCitas.Api.Controllers;

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

    [HttpGet("available-slots")]
    public async Task<IActionResult> GetAvailableSlots()
    {
        var (slots, error) = await agendaService.GetAvailableSlotsAsync(CurrentUserId());
        return error is null ? Ok(slots) : StatusCode(StatusCodes.Status403Forbidden, new { message = error });
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var (success, error) = await agendaService.CancelAppointmentAsync(CurrentUserId(), id);
        if (success) return Ok(new { message = "Cita cancelada con éxito." });
        if (error == "Cita no encontrada o no te pertenece.") return NotFound(new { message = error });
        return BadRequest(new { message = error });
    }

    [HttpPatch("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentRequest request)
    {
        var (appointment, error) = await agendaService.RescheduleAppointmentAsync(CurrentUserId(), id, request.SlotId);
        return appointment is null ? Conflict(new { message = error }) : Ok(appointment);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}