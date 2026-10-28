using System.Security.Claims;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(Roles = "Paciente")]
public class AppointmentsController(AppointmentService appointmentService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine() => Ok(await appointmentService.MineAsync(CurrentUserId()));

    [HttpPost]
    public async Task<IActionResult> Create(CreateAppointmentRequest request)
    {
        var (appointment, error) = await appointmentService.CreateAsync(CurrentUserId(), request.SlotId);
        return appointment is null ? Conflict(new { message = error }) : Ok(appointment);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var (ok, error) = await appointmentService.CancelAsync(CurrentUserId(), id);
        return ok ? NoContent() : BadRequest(new { message = error });
    }

    [HttpPut("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleAppointmentRequest request)
    {
        var (appointment, error) = await appointmentService.RescheduleAsync(CurrentUserId(), id, request.SlotId);
        return appointment is null ? Conflict(new { message = error }) : Ok(appointment);
    }

    [HttpPut("{id:guid}/attendance")]
    public async Task<IActionResult> RespondAttendance(Guid id, AttendanceResponseRequest request)
    {
        var (appointment, error) = await appointmentService.RespondAttendanceAsync(
            CurrentUserId(),
            id,
            request.Response);

        return appointment is null
            ? BadRequest(new { message = error })
            : Ok(appointment);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
