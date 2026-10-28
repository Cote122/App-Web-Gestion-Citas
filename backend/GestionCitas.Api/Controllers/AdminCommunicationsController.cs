using GestionCitas.Api.Common;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/admin/communications")]
[Authorize(Roles = "Administrador")]
public class AdminCommunicationsController(
    AppDbContext db,
    AppointmentCommunicationService communicationService)
    : ControllerBase
{
    [HttpPost("process")]
    public async Task<IActionResult> Process(CancellationToken cancellationToken)
    {
        var result = await communicationService.ProcessAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("appointments/{id:guid}/request-attendance")]
    public async Task<IActionResult> RequestAttendance(
        Guid id,
        CancellationToken cancellationToken)
    {
        var ok = await communicationService.RequestAttendanceAsync(id, cancellationToken);

        return ok
            ? Ok(new { message = "Solicitud de confirmación generada correctamente." })
            : BadRequest(new { message = "La cita no es elegible para solicitar confirmación." });
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> Attendance(CancellationToken cancellationToken)
    {
        var now = PeruClock.Now;

        var data = await db.Appointments
            .AsNoTracking()
            .Where(x =>
                x.Status == AppointmentStatus.Confirmada &&
                x.Slot.StartAt > now)
            .OrderBy(x => x.Slot.StartAt)
            .Select(x => new AdminAttendanceDto(
                x.Id,
                x.Code,
                x.PatientId,
                x.Patient.FullName,
                x.Patient.Email,
                x.Slot.Professional.Specialty.Name,
                x.Slot.Professional.FullName,
                x.Slot.StartAt,
                x.AttendanceStatus.ToString(),
                x.AttendanceRequestedAt,
                x.AttendanceRespondedAt))
            .ToListAsync(cancellationToken);

        return Ok(data);
    }
}
