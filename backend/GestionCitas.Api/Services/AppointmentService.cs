using System.Data;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Services;

public class AppointmentService(AppDbContext db)
{
    public async Task<(AppointmentDto? Appointment, string? Error)> CreateAsync(Guid patientId, Guid slotId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var slot = await db.AvailabilitySlots
            .Include(x => x.Professional)
            .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == slotId);

        if (slot is null) return (null, "El horario seleccionado no existe.");
        if (!IsBookable(slot.Professional))
            return (null, ProfessionalUnavailableMessage);
        if (!slot.IsAvailable || slot.StartAt <= DateTimeOffset.UtcNow)
            return (null, "El horario ya no está disponible.");

        slot.IsAvailable = false;
        var appointment = new Appointment
        {
            Code = GenerateCode(),
            PatientId = patientId,
            SlotId = slot.Id,
            Status = AppointmentStatus.Confirmada
        };
        db.Appointments.Add(appointment);

        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (null, "Otro usuario tomó el horario. Selecciona otro cupo.");
        }

        return (ToDto(appointment, slot), null);
    }

    public async Task<List<AppointmentDto>> MineAsync(Guid patientId)
    {
        var rows = await db.Appointments
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .OrderByDescending(x => x.Slot.StartAt)
            .ToListAsync();

        return rows.Select(x => ToDto(x, x.Slot)).ToList();
    }

    public async Task<(bool Ok, string? Error)> CancelAsync(Guid patientId, Guid appointmentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var appointment = await db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == appointmentId && x.PatientId == patientId);

        if (appointment is null) return (false, "Cita no encontrada.");
        if (appointment.Status != AppointmentStatus.Confirmada)
            return (false, "La cita ya no puede cancelarse.");
        if (appointment.Slot.StartAt <= DateTimeOffset.UtcNow)
            return (false, "No se puede cancelar una cita cuyo horario ya inició.");

        var cancelledSlotId = appointment.SlotId;
        var cancelledAt = DateTimeOffset.UtcNow;

        appointment.Status = AppointmentStatus.Cancelada;
        appointment.UpdatedAt = cancelledAt;

        db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            Appointment = appointment,
            PatientId = appointment.PatientId,
            EventType = "Cancelled",
            PreviousSlotId = cancelledSlotId,
            Details =
                $"Cita {appointment.Code} cancelada por el paciente. " +
                $"Horario original: {appointment.Slot.StartAt:dd/MM/yyyy HH:mm}."
        });

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = appointment.PatientId,
            RecipientEmail = appointment.Patient.Email,
            Type = "AppointmentCancelled",
            Subject = "Tu cita fue cancelada",
            Body =
                $"Hola {appointment.Patient.FullName}, tu cita {appointment.Code} del " +
                $"{appointment.Slot.StartAt:dd/MM/yyyy} a las {appointment.Slot.StartAt:HH:mm} " +
                "fue cancelada correctamente."
        });

        await AssignReleasedSlotOrMakeAvailableAsync(appointment.Slot);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (true, null);
    }

    public async Task<(AppointmentDto? Appointment, string? Error)> RescheduleAsync(Guid patientId, Guid appointmentId, Guid newSlotId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var current = await db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == appointmentId && x.PatientId == patientId);

        if (current is null) return (null, "Cita no encontrada.");
        if (current.Status != AppointmentStatus.Confirmada)
            return (null, "La cita ya no puede reprogramarse.");
        if (current.Slot.StartAt <= DateTimeOffset.UtcNow)
            return (null, "No se puede reprogramar una cita cuyo horario ya inició.");
        if (current.SlotId == newSlotId)
            return (null, "Selecciona un horario diferente al de tu cita actual.");

        var newSlot = await db.AvailabilitySlots
            .Include(x => x.Professional)
            .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == newSlotId);

        if (newSlot is not null && !IsBookable(newSlot.Professional))
            return (null, ProfessionalUnavailableMessage);
        if (newSlot is null || !newSlot.IsAvailable || newSlot.StartAt <= DateTimeOffset.UtcNow)
            return (null, "El nuevo horario ya no está disponible.");
        if (newSlot.ProfessionalId != current.Slot.ProfessionalId)
            return (null, "La reprogramación debe mantener el mismo profesional de la cita.");
        if (newSlot.StartAt == current.Slot.StartAt)
            return (null, "Selecciona una fecha u hora diferente a la de tu cita actual.");

        var previousSlotId = current.SlotId;
        var previousStartAt = current.Slot.StartAt;
        var changedAt = DateTimeOffset.UtcNow;

        current.Status = AppointmentStatus.Reprogramada;
        current.UpdatedAt = changedAt;

        newSlot.IsAvailable = false;

        var replacement = new Appointment
        {
            Code = GenerateCode(),
            PatientId = patientId,
            SlotId = newSlot.Id,
            Status = AppointmentStatus.Confirmada,
            PreviousAppointmentId = current.Id
        };
        db.Appointments.Add(replacement);

        db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            Appointment = current,
            PatientId = current.PatientId,
            EventType = "Rescheduled",
            PreviousSlotId = previousSlotId,
            NewSlotId = newSlot.Id,
            Details =
                $"Cita {current.Code} reprogramada. Horario anterior: " +
                $"{previousStartAt:dd/MM/yyyy HH:mm}. Nuevo horario: " +
                $"{newSlot.StartAt:dd/MM/yyyy HH:mm}. Nueva cita: {replacement.Code}."
        });

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = current.PatientId,
            RecipientEmail = current.Patient.Email,
            Type = "AppointmentRescheduled",
            Subject = "Tu cita fue reprogramada",
            Body =
                $"Hola {current.Patient.FullName}, tu cita fue reprogramada correctamente. " +
                $"Nuevo horario: {newSlot.StartAt:dd/MM/yyyy} a las {newSlot.StartAt:HH:mm}. " +
                $"Nuevo código: {replacement.Code}."
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
            return (null, "Otro usuario tomó el nuevo horario. Selecciona otro cupo.");
        }

        return (ToDto(replacement, newSlot), null);
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
        next.AssignedAt = DateTimeOffset.UtcNow;

        slot.IsAvailable = false;

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = next.PatientId,
            RecipientEmail = next.Patient.Email,
            Type = "WaitlistSlotAssigned",
            Subject = "Se liberó un cupo y tu cita fue registrada",
            Body =
                $"Hola {next.Patient.FullName}, se liberó el horario del {slot.StartAt:dd/MM/yyyy} " +
                $"a las {slot.StartAt:HH:mm}. Como eras el primero de la lista de espera, " +
                $"la cita fue registrada automáticamente a tu nombre con código {appointment.Code}."
        });
    }

    private const string ProfessionalUnavailableMessage =
        "El profesional ya no está disponible para nuevas reservas. Elige otro profesional.";

    private static bool IsBookable(Professional professional) =>
        professional.IsActive && professional.Specialty.IsActive;

    private static string GenerateCode() =>
        $"CIT-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static AppointmentDto ToDto(Appointment appointment, AvailabilitySlot slot) =>
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
            slot.Professional.Specialty.Price);
}
