using System.Security.Claims;
using System.Text.RegularExpressions;
using GestionCitas.Api.Common;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

/// <summary>HU-14: el profesional registra sus vacaciones o licencias y ve las citas afectadas.</summary>
[ApiController]
[Route("api/professional/absences")]
[Authorize(Roles = "Profesional")]
public class ProfessionalAbsencesController(AppDbContext db) : ControllerBase
{
    private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);
    private const int MaxDays = 90;

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var professional = await CurrentProfessionalAsync();
        if (professional is null) return NotLinked();

        var absences = await db.ProfessionalAbsences
            .AsNoTracking()
            .Where(x => x.ProfessionalId == professional.Id)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();

        var result = new List<AbsenceDto>();
        foreach (var absence in absences)
            result.Add(ToDto(absence, absence.IsActive ? await AffectedQuery(absence).CountAsync() : 0));

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAbsenceRequest request)
    {
        var professional = await CurrentProfessionalAsync();
        if (professional is null) return NotLinked();

        var startDate = request.StartDate!.Value;
        var endDate = request.EndDate!.Value;
        var today = DateOnly.FromDateTime(PeruClock.Now.DateTime);

        if (startDate < today) return BadRequest(new { message = "La fecha de inicio no puede ser anterior a hoy." });
        if (endDate < startDate) return BadRequest(new { message = "La fecha de fin debe ser igual o posterior a la de inicio." });
        if (endDate.DayNumber - startDate.DayNumber + 1 > MaxDays)
            return BadRequest(new { message = $"La ausencia no puede durar más de {MaxDays} días." });

        var startAt = StartOfDay(startDate);
        var endAt = StartOfDay(endDate.AddDays(1));

        var overlaps = await db.ProfessionalAbsences.AnyAsync(x =>
            x.ProfessionalId == professional.Id && x.IsActive && x.StartAt < endAt && x.EndAt > startAt);
        if (overlaps)
            return Conflict(new { message = "Ya tienes una ausencia registrada que se cruza con esas fechas." });

        var absence = new ProfessionalAbsence
        {
            ProfessionalId = professional.Id,
            Type = Enum.Parse<AbsenceType>(request.Type),
            StartAt = startAt,
            EndAt = endAt,
            Reason = CleanReason(request.Reason)
        };

        db.ProfessionalAbsences.Add(absence);
        await db.SaveChangesAsync();

        // Escenario 3: se identifican las citas ya reservadas dentro del periodo.
        var affected = await AffectedAppointmentsAsync(absence);
        return Ok(new CreateAbsenceResponse(ToDto(absence, affected.Count), affected));
    }

    [HttpGet("{id:guid}/affected")]
    public async Task<IActionResult> Affected(Guid id)
    {
        var professional = await CurrentProfessionalAsync();
        if (professional is null) return NotLinked();

        var absence = await db.ProfessionalAbsences.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ProfessionalId == professional.Id);
        if (absence is null) return NotFound(new { message = "Ausencia no encontrada." });

        return Ok(await AffectedAppointmentsAsync(absence));
    }

    // Anular no borra el registro; solo se permite si la ausencia aún no empieza.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var professional = await CurrentProfessionalAsync();
        if (professional is null) return NotLinked();

        var absence = await db.ProfessionalAbsences
            .FirstOrDefaultAsync(x => x.Id == id && x.ProfessionalId == professional.Id);
        if (absence is null) return NotFound(new { message = "Ausencia no encontrada." });
        if (!absence.IsActive) return BadRequest(new { message = "Esta ausencia ya fue anulada." });
        if (absence.StartAt <= PeruClock.Now) return BadRequest(new { message = "Solo puedes anular ausencias que aún no empiezan." });

        absence.IsActive = false;
        absence.CancelledAt = PeruClock.Now;
        await db.SaveChangesAsync();

        return Ok(ToDto(absence, 0));
    }

    private async Task<Professional?> CurrentProfessionalAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.Professionals.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
    }

    private ObjectResult NotLinked() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu cuenta no está vinculada a un profesional. Contacta al administrador." });

    private IQueryable<Appointment> AffectedQuery(ProfessionalAbsence absence) =>
        db.Appointments.AsNoTracking().Where(x =>
            x.Status == AppointmentStatus.Confirmada &&
            x.Slot.ProfessionalId == absence.ProfessionalId &&
            x.Slot.StartAt < absence.EndAt &&
            x.Slot.EndAt > absence.StartAt);

    private Task<List<AffectedAppointmentDto>> AffectedAppointmentsAsync(ProfessionalAbsence absence) =>
        AffectedQuery(absence)
            .OrderBy(x => x.Slot.StartAt)
            .Select(x => new AffectedAppointmentDto(x.Id, x.Code, x.Patient.FullName, x.Patient.Email, x.Slot.StartAt))
            .ToListAsync();

    private static DateTimeOffset StartOfDay(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), PeruOffset);

    private static DateOnly ToPeruDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(value.ToOffset(PeruOffset).DateTime);

    // Motivo: sin espacios repetidos ni caracteres de control.
    private static string CleanReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return string.Empty;
        var clean = Regex.Replace(reason, @"[\p{C}]", " ");
        return Regex.Replace(clean, @"\s+", " ").Trim();
    }

    private static AbsenceDto ToDto(ProfessionalAbsence absence, int affectedCount)
    {
        var now = PeruClock.Now;
        var status = !absence.IsActive ? "Anulada"
            : absence.EndAt <= now ? "Finalizada"
            : absence.StartAt <= now ? "En curso"
            : "Programada";

        return new AbsenceDto(
            absence.Id,
            absence.Type.ToString(),
            ToPeruDate(absence.StartAt),
            ToPeruDate(absence.EndAt).AddDays(-1),
            absence.Reason,
            status,
            affectedCount,
            absence.IsActive && absence.StartAt > now);
    }
}
