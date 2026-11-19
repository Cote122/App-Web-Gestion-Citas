using System.Data;
using GestionCitas.Api.Common;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Services;

public class ProfessionalAgendaService(AppDbContext db)
{
    public static readonly TimeSpan ClinicOffset = TimeSpan.FromHours(-5);

    public async Task<(List<ProfessionalAgendaItemDto>? Items, string? Error)> ListAsync(Guid userId)
    {
        var professional = await LinkedProfessionalAsync(userId);
        if (professional is null)
            return (null, "Tu usuario no está vinculado a un profesional.");

        var rows = await db.Appointments
            .AsNoTracking()
            .Where(x => x.Slot.ProfessionalId == professional.Id)
            .Include(x => x.Patient)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .OrderBy(x => x.Slot.StartAt)
            .ToListAsync();

        return (rows.Select(ToItem).ToList(), null);
    }

    public async Task<(ProfessionalAppointmentDetailDto? Detail, string? Error)> DetailAsync(Guid userId, Guid appointmentId)
    {
        var professional = await LinkedProfessionalAsync(userId);
        if (professional is null)
            return (null, "Tu usuario no está vinculado a un profesional.");

        var appointment = await db.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Slot.ProfessionalId == professional.Id);

        if (appointment is null)
            return (null, "Cita no encontrada.");

        return (ToDetail(appointment), null);
    }

    public async Task<(List<AvailableSlotDto>? Slots, string? Error)> GetAvailableSlotsAsync(Guid userId)
    {
        var professional = await LinkedProfessionalAsync(userId);
        if (professional is null)
            return (null, "Tu usuario no está vinculado a un profesional.");

        var slots = await db.AvailabilitySlots
            .AsNoTracking()
            .Where(s => s.ProfessionalId == professional.Id && s.IsAvailable && s.StartAt > PeruClock.Now)
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        var dtos = slots.Select(s => new AvailableSlotDto(s.Id, s.StartAt, s.EndAt)).ToList();

        return (dtos, null);
    }

    public async Task<(bool Success, string? Error)> CancelAppointmentAsync(Guid userId, Guid appointmentId, string? reason = null)
    {
        var professional = await LinkedProfessionalAsync(userId);
        if (professional is null)
            return (false, "Tu usuario no está vinculado a un profesional.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var appointment = await db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
            .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Slot.ProfessionalId == professional.Id);

        if (appointment is null)
            return (false, "Cita no encontrada o no te pertenece.");

        if (appointment.Status != AppointmentStatus.Confirmada)
            return (false, "La cita ya no puede cancelarse.");

        if (appointment.Slot.StartAt <= PeruClock.Now)
            return (false, "No se puede cancelar una cita cuyo horario ya inició.");

        var cancelledSlotId = appointment.SlotId;
        var cancelledAt = PeruClock.Now;

        appointment.Status = AppointmentStatus.Cancelada;
        appointment.UpdatedAt = cancelledAt;

        // Guardamos el motivo en la cita si se proporciona desde el frontend
        if (!string.IsNullOrWhiteSpace(reason))
        {
            appointment.Reason = reason;
        }

        db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            Appointment = appointment,
            PatientId = appointment.PatientId,
            EventType = "CancelledByProfessional",
            PreviousSlotId = cancelledSlotId,
            Details = $"Cita {appointment.Code} cancelada por el profesional. Motivo: {reason ?? "No especificado"}. Horario original: {appointment.Slot.StartAt:dd/MM/yyyy HH:mm}."
        });

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = appointment.PatientId,
            RelatedAppointmentId = appointment.Id,
            RecipientEmail = appointment.Patient.Email,
            Type = "AppointmentCancelled",
            Subject = "Tu cita fue cancelada por el profesional",
            Body = $"Hola {appointment.Patient.FullName}, te informamos que tu cita {appointment.Code} programada para el {appointment.Slot.StartAt:dd/MM/yyyy} a las {appointment.Slot.StartAt:HH:mm} ha sido cancelada. Motivo: {reason ?? "No especificado"}."
        });

        await AssignReleasedSlotOrMakeAvailableAsync(appointment.Slot);

        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Error de concurrencia al cancelar la cita. Inténtalo de nuevo.");
        }

        return (true, null);
    }

    public async Task<(AppointmentDto? Appointment, string? Error)> RescheduleAppointmentAsync(Guid userId, Guid appointmentId, Guid newSlotId, string? reason = null)
    {
        var professional = await LinkedProfessionalAsync(userId);
        if (professional is null)
            return (null, "Tu usuario no está vinculado a un profesional.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var current = await db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Slot.ProfessionalId == professional.Id);

        if (current is null)
            return (null, "Cita no encontrada o no te pertenece.");

        if (current.Status != AppointmentStatus.Confirmada)
            return (null, "La cita ya no puede reprogramarse.");

        if (current.Slot.StartAt <= PeruClock.Now)
            return (null, "No se puede reprogramar una cita cuyo horario ya inició.");

        if (current.SlotId == newSlotId)
            return (null, "Selecciona un horario diferente al de tu cita actual.");

        var newSlot = await db.AvailabilitySlots
            .Include(x => x.Professional)
            .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == newSlotId && x.ProfessionalId == professional.Id);

        if (newSlot is null || !newSlot.IsAvailable || newSlot.StartAt <= PeruClock.Now)
            return (null, "El nuevo horario seleccionado no está disponible o no te pertenece.");

        var previousSlotId = current.SlotId;
        var previousStartAt = current.Slot.StartAt;
        var changedAt = PeruClock.Now;

        current.Status = AppointmentStatus.Reprogramada;
        current.UpdatedAt = changedAt;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            current.Reason = reason;
        }

        newSlot.IsAvailable = false;

        var replacement = new Appointment
        {
            Code = GenerateCode(),
            PatientId = current.PatientId,
            SlotId = newSlot.Id,
            Status = AppointmentStatus.Confirmada,
            PreviousAppointmentId = current.Id
        };

        db.Appointments.Add(replacement);

        db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            Appointment = current,
            PatientId = current.PatientId,
            EventType = "RescheduledByProfessional",
            PreviousSlotId = previousSlotId,
            NewSlotId = newSlot.Id,
            Details = $"Cita {current.Code} reprogramada por el profesional. Motivo: {reason ?? "No especificado"}. Horario anterior: {previousStartAt:dd/MM/yyyy HH:mm}. Nuevo horario: {newSlot.StartAt:dd/MM/yyyy HH:mm}. Nueva cita: {replacement.Code}."
        });

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = current.PatientId,
            RelatedAppointmentId = current.Id,
            RecipientEmail = current.Patient.Email,
            Type = "AppointmentRescheduled",
            Subject = "Tu cita fue reprogramada",
            Body = $"Hola {current.Patient.FullName}, tu cita fue reprogramada por el profesional. Nuevo horario: {newSlot.StartAt:dd/MM/yyyy} a las {newSlot.StartAt:HH:mm}. Motivo: {reason ?? "No especificado"}. Nuevo código: {replacement.Code}."
        });

        await AssignReleasedSlotOrMakeAvailableAsync(current.Slot);

        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (null, "El horario seleccionado acaba de ser tomado por otro usuario. Selecciona otro.");
        }

        return (ToAppointmentDto(replacement, newSlot), null);
    }

    private async Task AssignReleasedSlotOrMakeAvailableAsync(AvailabilitySlot slot)
    {
        var next = await db.WaitlistEntries
            .Include(x => x.Patient)
            .Where(x => x.SlotId == slot.Id && x.Status == WaitlistStatus.Activa)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync();

        if (next is null)
        {
            slot.IsAvailable = true;
            return;
        }

        var appointment = new Appointment
        {
            Code = GenerateCode(),
            PatientId = next.PatientId,
            SlotId = slot.Id,
            Status = AppointmentStatus.Confirmada
        };

        db.Appointments.Add(appointment);

        next.Status = WaitlistStatus.Asignada;
        next.AssignedAppointment = appointment;
        next.AssignedAt = PeruClock.Now;

        slot.IsAvailable = false;

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = next.PatientId,
            RelatedAppointmentId = appointment.Id,
            RecipientEmail = next.Patient.Email,
            Type = "WaitlistSlotAssigned",
            Subject = "Se liberó un cupo y tu cita fue registrada",
            Body = $"Hola {next.Patient.FullName}, se liberó el horario del {slot.StartAt:dd/MM/yyyy} a las {slot.StartAt:HH:mm}. Como eras el primero de la lista de espera, la cita fue registrada automáticamente a tu nombre con código {appointment.Code}."
        });
    }

    private Task<Professional?> LinkedProfessionalAsync(Guid userId) =>
        db.Professionals.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);

    private static string GenerateCode() =>
        $"CIT-{PeruClock.Now:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static AppointmentDto ToAppointmentDto(Appointment appointment, AvailabilitySlot slot) =>
        new(
            appointment.Id,
            appointment.Code,
            slot.Professional.SpecialtyId,
            slot.Professional.Specialty.Name,
            slot.ProfessionalId,
            slot.Professional.FullName,
            slot.Professional.Office,
            slot.StartAt,
            slot.EndAt,
            appointment.Status.ToString(),
            slot.Professional.Specialty.Price,
            appointment.AttendanceStatus.ToString(),
            appointment.AttendanceRequestedAt,
            appointment.AttendanceRespondedAt);

    private static ProfessionalAgendaItemDto ToItem(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.Code,
            appointment.Patient.Id,
            appointment.Patient.FullName,
            appointment.Slot.StartAt,
            appointment.Slot.EndAt,
            appointment.Slot.Professional.Specialty.Name,
            appointment.Reason,
            appointment.Status.ToString(),
            appointment.Slot.Professional.Office);

    private static ProfessionalAppointmentDetailDto ToDetail(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.Code,
            appointment.Slot.StartAt,
            appointment.Slot.EndAt,
            appointment.Slot.Professional.Specialty.Name,
            appointment.Slot.Professional.Office,
            appointment.Reason,
            appointment.Status.ToString(),
            new ProfessionalPatientDto(
                appointment.Patient.Id,
                appointment.Patient.Dni,
                appointment.Patient.FullName,
                appointment.Patient.Email),
            appointment.Slot.Professional.FullName);
}
