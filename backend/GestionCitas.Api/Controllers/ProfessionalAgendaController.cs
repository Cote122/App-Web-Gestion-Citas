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
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelAppointmentRequest request)
    {
        var (changeRequest, error) =
            await agendaService.CancelAppointmentAsync(
                CurrentUserId(),
                id,
                request?.Reason
            );

        if (changeRequest is not null)
        {
            return Accepted(new
            {
                message = "Solicitud de cancelación registrada. Pendiente de aprobación administrativa.",
                request = changeRequest
            });
        }

        if (error == "Cita no encontrada o no te pertenece.")
            return NotFound(new { message = error });

        if (error == "Tu usuario no está vinculado a un profesional.")
            return Forbid();

        return Conflict(new { message = error });
    }

    [HttpPatch("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(
        Guid id,
        [FromBody] RescheduleAppointmentRequest request)
    {
        var (changeRequest, error) =
            await agendaService.RescheduleAppointmentAsync(
                CurrentUserId(),
                id,
                request.SlotId,
                request.Reason
            );

        if (changeRequest is not null)
        {
            return Accepted(new
            {
                message = "Solicitud de reprogramación registrada. Pendiente de aprobación administrativa.",
                request = changeRequest
            });
        }

        if (error == "Cita no encontrada o no te pertenece.")
            return NotFound(new { message = error });

        if (error == "Tu usuario no está vinculado a un profesional.")
            return Forbid();

        return Conflict(new { message = error });
    }

    [HttpGet("change-requests")]
    public async Task<IActionResult> GetChangeRequests()
    {
        var (requests, error) =
            await agendaService.ListChangeRequestsAsync(CurrentUserId());

        if (requests is not null)
            return Ok(requests);

        return StatusCode(
            StatusCodes.Status403Forbidden,
            new { message = error }
        );
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}