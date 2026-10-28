using GestionCitas.Api.Common;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestionCitas.Api.Services;

public class AppointmentCommunicationService(
    AppDbContext db,
    IOptions<CommunicationSettings> options)
{
    private readonly CommunicationSettings settings = options.Value;

    public async Task<CommunicationProcessResult> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var now = PeruClock.Now;
        var reminderHours = Math.Max(1, settings.ReminderWindowHours);
        var attendanceHours = Math.Max(1, settings.AttendanceConfirmationWindowHours);
        var maxWindowHours = Math.Max(reminderHours, attendanceHours);

        var candidates = await db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .Where(x =>
                x.Status == AppointmentStatus.Confirmada &&
                x.Slot.StartAt > now &&
                x.Slot.StartAt <= now.AddHours(maxWindowHours))
            .OrderBy(x => x.Slot.StartAt)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return new CommunicationProcessResult(0, 0);

        var ids = candidates.Select(x => x.Id).ToList();

        var existing = await db.NotificationOutbox
            .AsNoTracking()
            .Where(x =>
                x.RelatedAppointmentId.HasValue &&
                ids.Contains(x.RelatedAppointmentId.Value) &&
                (x.Type == "AppointmentReminder" ||
                 x.Type == "AttendanceConfirmationRequest"))
            .Select(x => new { x.RelatedAppointmentId, x.Type })
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Select(x => $"{x.RelatedAppointmentId}:{x.Type}")
            .ToHashSet(StringComparer.Ordinal);

        var remindersCreated = 0;
        var attendanceRequestsCreated = 0;

        foreach (var appointment in candidates)
        {
            var hoursUntil = (appointment.Slot.StartAt - now).TotalHours;

            if (hoursUntil <= reminderHours)
            {
                var key = $"{appointment.Id}:AppointmentReminder";
                if (!existingKeys.Contains(key))
                {
                    db.NotificationOutbox.Add(new NotificationOutbox
                    {
                        RecipientUserId = appointment.PatientId,
                        RelatedAppointmentId = appointment.Id,
                        RecipientEmail = appointment.Patient.Email,
                        Type = "AppointmentReminder",
                        Subject = "Recordatorio de tu cita",
                        Body =
                            $"Hola {appointment.Patient.FullName}, te recordamos que tienes una cita " +
                            $"de {appointment.Slot.Professional.Specialty.Name} con " +
                            $"{appointment.Slot.Professional.FullName} el " +
                            $"{appointment.Slot.StartAt:dd/MM/yyyy} a las {appointment.Slot.StartAt:HH:mm} " +
                            $"en {appointment.Slot.Professional.Office}. Código: {appointment.Code}."
                    });

                    existingKeys.Add(key);
                    remindersCreated++;
                }
            }

            if (hoursUntil <= attendanceHours &&
                appointment.AttendanceStatus == AttendanceConfirmationStatus.Pendiente &&
                appointment.AttendanceRequestedAt is null)
            {
                var key = $"{appointment.Id}:AttendanceConfirmationRequest";
                if (!existingKeys.Contains(key))
                {
                    appointment.AttendanceRequestedAt = now;

                    db.NotificationOutbox.Add(new NotificationOutbox
                    {
                        RecipientUserId = appointment.PatientId,
                        RelatedAppointmentId = appointment.Id,
                        RecipientEmail = appointment.Patient.Email,
                        Type = "AttendanceConfirmationRequest",
                        Subject = "Confirma tu asistencia a la cita",
                        Body =
                            $"Hola {appointment.Patient.FullName}, por favor confirma si asistirás a tu cita " +
                            $"de {appointment.Slot.Professional.Specialty.Name} del " +
                            $"{appointment.Slot.StartAt:dd/MM/yyyy} a las {appointment.Slot.StartAt:HH:mm}. " +
                            "Puedes responder desde la sección Mis citas."
                    });

                    existingKeys.Add(key);
                    attendanceRequestsCreated++;
                }
            }
        }

        if (remindersCreated > 0 || attendanceRequestsCreated > 0)
            await db.SaveChangesAsync(cancellationToken);

        return new CommunicationProcessResult(remindersCreated, attendanceRequestsCreated);
    }

    public async Task<bool> RequestAttendanceAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var now = PeruClock.Now;

        var appointment = await db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(
                x => x.Id == appointmentId,
                cancellationToken);

        if (appointment is null ||
            appointment.Status != AppointmentStatus.Confirmada ||
            appointment.Slot.StartAt <= now ||
            appointment.AttendanceStatus != AttendanceConfirmationStatus.Pendiente)
            return false;

        var exists = await db.NotificationOutbox.AnyAsync(
            x => x.RelatedAppointmentId == appointment.Id &&
                 x.Type == "AttendanceConfirmationRequest",
            cancellationToken);

        if (exists)
            return true;

        appointment.AttendanceRequestedAt = now;

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = appointment.PatientId,
            RelatedAppointmentId = appointment.Id,
            RecipientEmail = appointment.Patient.Email,
            Type = "AttendanceConfirmationRequest",
            Subject = "Confirma tu asistencia a la cita",
            Body =
                $"Hola {appointment.Patient.FullName}, por favor confirma si asistirás a tu cita " +
                $"de {appointment.Slot.Professional.Specialty.Name} del " +
                $"{appointment.Slot.StartAt:dd/MM/yyyy} a las {appointment.Slot.StartAt:HH:mm}. " +
                "Puedes responder desde la sección Mis citas."
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
