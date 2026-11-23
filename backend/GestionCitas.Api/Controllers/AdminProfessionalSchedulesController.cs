using System.Data;
using System.Globalization;
using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/admin/professionals/{professionalId:guid}/schedule")]
[Authorize(Roles = "Administrador")]
public class AdminProfessionalSchedulesController(AppDbContext db) : ControllerBase
{
    public record ScheduleEntry(int DayOfWeek, string StartTime, bool IsEnabled);
    public record SaveScheduleRequest(List<ScheduleEntry>? Slots);

    [HttpGet]
    public async Task<IActionResult> Get(Guid professionalId)
    {
        var professional = await db.Professionals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == professionalId);
        if (professional is null) return NotFound(new { message = "Profesional no encontrado." });

        var stored = professional.ScheduleConfigured
            ? await db.ProfessionalWeeklySlots.AsNoTracking()
                .Where(x => x.ProfessionalId == professionalId)
                .OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime)
                .ToListAsync()
            : [];
        var entries = professional.ScheduleConfigured
            ? stored.Select(x => new ScheduleEntry(x.DayOfWeek, x.StartTime.ToString(@"hh\:mm"), x.IsEnabled)).ToList()
            : Enumerable.Range(1, 6)
                .SelectMany(day => WeeklySchedule.LegacyHours.Select(hour =>
                    new ScheduleEntry(day, hour.ToString(@"hh\:mm"), true)))
                .ToList();

        return Ok(new { professional.Id, professional.FullName, professional.ScheduleConfigured, Slots = entries });
    }

    [HttpPut]
    public async Task<IActionResult> Save(Guid professionalId, SaveScheduleRequest request, CancellationToken cancellationToken)
    {
        if (request.Slots is null || request.Slots.Count > 200)
            return BadRequest(new { message = "Envía hasta 200 horarios semanales." });

        var parsed = new List<ProfessionalWeeklySlot>();
        foreach (var entry in request.Slots)
        {
            if (entry.DayOfWeek is < 0 or > 6 ||
                !TimeSpan.TryParseExact(entry.StartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var start) ||
                start > TimeSpan.FromDays(1) - WeeklySchedule.Duration ||
                start.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
                return BadRequest(new { message = "Usa días válidos y horas cada 15 minutos, entre 00:00 y 23:15." });

            parsed.Add(new ProfessionalWeeklySlot
            {
                ProfessionalId = professionalId, DayOfWeek = entry.DayOfWeek,
                StartTime = start, IsEnabled = entry.IsEnabled
            });
        }

        if (parsed.GroupBy(x => (x.DayOfWeek, x.StartTime)).Any(x => x.Count() > 1) ||
            parsed.Where(x => x.IsEnabled).GroupBy(x => x.DayOfWeek).Any(group =>
                group.OrderBy(x => x.StartTime).Zip(group.OrderBy(x => x.StartTime).Skip(1))
                    .Any(pair => pair.First.StartTime + WeeklySchedule.Duration > pair.Second.StartTime)))
            return BadRequest(new { message = "Hay horarios duplicados o superpuestos en un mismo día." });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var professional = await db.Professionals.FirstOrDefaultAsync(x => x.Id == professionalId, cancellationToken);
        if (professional is null) return NotFound(new { message = "Profesional no encontrado." });

        var previous = await db.ProfessionalWeeklySlots
            .Where(x => x.ProfessionalId == professionalId).ToListAsync(cancellationToken);
        db.ProfessionalWeeklySlots.RemoveRange(previous);
        await db.SaveChangesAsync(cancellationToken);
        db.ProfessionalWeeklySlots.AddRange(parsed);
        professional.ScheduleConfigured = true;

        var now = DateTimeOffset.UtcNow;
        var future = await db.AvailabilitySlots
            .Where(x => x.ProfessionalId == professionalId && x.StartAt > now)
            .ToListAsync(cancellationToken);
        var disabledIds = new List<Guid>();
        foreach (var slot in future)
        {
            slot.IsEnabled = WeeklySchedule.Allows(professional, slot.StartAt, parsed);
            if (!slot.IsEnabled) disabledIds.Add(slot.Id);
        }

        // La cita ya reservada no se modifica. Una espera para un horario retirado
        // se cierra y se avisa al paciente, para que no quede una cola imposible.
        if (disabledIds.Count > 0)
        {
            var waiting = await db.WaitlistEntries.Include(x => x.Patient)
                .Include(x => x.Slot)
                .Where(x => disabledIds.Contains(x.SlotId) && x.Status == WaitlistStatus.Activa)
                .ToListAsync(cancellationToken);
            foreach (var entry in waiting)
            {
                entry.Status = WaitlistStatus.Cerrada;
                db.NotificationOutbox.Add(new NotificationOutbox
                {
                    RecipientUserId = entry.PatientId,
                    RecipientEmail = entry.Patient.Email,
                    Type = "WaitlistSlotDisabled",
                    Subject = "Tu horario en lista de espera ya no está disponible",
                    Body = $"Hola {entry.Patient.FullName}, el horario del {entry.Slot.StartAt:dd/MM/yyyy} " +
                           $"a las {entry.Slot.StartAt:HH:mm} dejó de ofrecerse. Se cerró tu solicitud de espera; " +
                           "puedes elegir otro horario en la plataforma."
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var created = await DbInitializer.EnsureAvailabilitySlotsAsync(db, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new { professional.Id, professional.FullName, professional.ScheduleConfigured,
            Slots = parsed.Select(x => new ScheduleEntry(x.DayOfWeek, x.StartTime.ToString(@"hh\:mm"), x.IsEnabled)),
            CreatedSlots = created });
    }
}
