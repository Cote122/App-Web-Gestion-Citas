using System.Data;
using GestionCitas.Api.Common;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace GestionCitas.Api.Services;

public class AppointmentService(AppDbContext db, IOptions<AppointmentPolicyOptions>? options = null)
{
    private readonly PatientReschedulingPolicy policy = new(db, options ?? Options.Create(new AppointmentPolicyOptions()));
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
        if (await AbsenceRules.IsInAbsenceAsync(db, slot))
            return (null, AbsenceRules.AbsenceMessage);
        if (!slot.IsEnabled || !slot.IsAvailable || slot.StartAt <= DateTimeOffset.UtcNow)
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

        return (await WithPolicyAsync(appointment, slot), null);
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

        var policies = await policy.BuildAsync(patientId, rows);
        return rows.Select(x => ToDto(x, x.Slot) with { Rescheduling = policies[x.Id] }).ToList();
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
        var cancelledAt = PeruClock.Now;

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
            RelatedAppointmentId = appointment.Id,
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
        try { return await RescheduleCoreAsync(patientId, appointmentId, newSlotId); }
        catch (SqlException error) when (error.Number == 1205) { return (null, "La cita cambió durante la operación. Actualiza e intenta nuevamente."); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 1205 })
        { return (null, "La cita cambió durante la operación. Actualiza e intenta nuevamente."); }
    }

    private async Task<(AppointmentDto? Appointment, string? Error)> RescheduleCoreAsync(Guid patientId, Guid appointmentId, Guid newSlotId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Serializar cambios de la misma cita antes de contar y ocupar otro cupo.
        IQueryable<Appointment> query = db.Appointments;
        if (db.Database.IsSqlServer())
            query = db.Appointments.FromSqlInterpolated($"SELECT * FROM dbo.Appointments WITH (UPDLOCK, HOLDLOCK) WHERE Id = {appointmentId} AND PatientId = {patientId}");
        var current = await query
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
        var allowance = (await policy.BuildAsync(patientId))[current.Id];
        if (!allowance.HistoryComplete) return (null, PatientReschedulingPolicy.IncompleteHistoryMessage);
        if (allowance.Remaining == 0)
            return (null, $"Alcanzaste el límite de {allowance.Maximum} reprogramaciones permitidas para esta cita.");
        if (current.SlotId == newSlotId)
            return (null, "Selecciona un horario diferente al de tu cita actual.");

        var newSlot = await db.AvailabilitySlots
            .Include(x => x.Professional)
            .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == newSlotId);

        if (newSlot is not null && !IsBookable(newSlot.Professional))
            return (null, ProfessionalUnavailableMessage);
        if (newSlot is not null && await AbsenceRules.IsInAbsenceAsync(db, newSlot))
            return (null, AbsenceRules.AbsenceMessage);
        if (newSlot is null || !newSlot.IsEnabled || !newSlot.IsAvailable || newSlot.StartAt <= DateTimeOffset.UtcNow)
            return (null, "El nuevo horario ya no está disponible.");
        if (newSlot.ProfessionalId != current.Slot.ProfessionalId)
            return (null, "La reprogramación debe mantener el mismo profesional de la cita.");
        if (newSlot.StartAt == current.Slot.StartAt)
            return (null, "Selecciona una fecha u hora diferente a la de tu cita actual.");

        var previousSlotId = current.SlotId;
        var previousStartAt = current.Slot.StartAt;
        var changedAt = PeruClock.Now;

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
            RelatedAppointmentId = current.Id,
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

        return (await WithPolicyAsync(replacement, newSlot), null);
    }

    public async Task<(AppointmentDto? Appointment, string? Error)> RespondAttendanceAsync(
        Guid patientId,
        Guid appointmentId,
        string response)
    {
        var appointment = await db.Appointments
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == appointmentId && x.PatientId == patientId);

        if (appointment is null)
            return (null, "Cita no encontrada.");

        if (appointment.Status != AppointmentStatus.Confirmada ||
            appointment.Slot.StartAt <= DateTimeOffset.UtcNow)
            return (null, "Esta cita ya no admite confirmación de asistencia.");

        if (appointment.AttendanceRequestedAt is null)
            return (null, "Aún no se ha solicitado confirmación de asistencia para esta cita.");

        if (appointment.AttendanceStatus != AttendanceConfirmationStatus.Pendiente)
            return (null, "Ya registraste una respuesta de asistencia para esta cita.");

        AttendanceConfirmationStatus status;
        if (string.Equals(response, "Confirmada", StringComparison.OrdinalIgnoreCase))
            status = AttendanceConfirmationStatus.Confirmada;
        else if (string.Equals(response, "NoAsistira", StringComparison.OrdinalIgnoreCase))
            status = AttendanceConfirmationStatus.NoAsistira;
        else
            return (null, "La respuesta de asistencia no es válida.");

        var now = PeruClock.Now;
        appointment.AttendanceStatus = status;
        appointment.AttendanceRespondedAt = now;
        appointment.UpdatedAt = now;

        db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            Appointment = appointment,
            PatientId = appointment.PatientId,
            EventType = status == AttendanceConfirmationStatus.Confirmada
                ? "AttendanceConfirmed"
                : "AttendanceDeclined",
            PreviousSlotId = appointment.SlotId,
            Details = status == AttendanceConfirmationStatus.Confirmada
                ? $"El paciente confirmó asistencia a la cita {appointment.Code}."
                : $"El paciente indicó que no asistirá a la cita {appointment.Code}."
        });

        await db.SaveChangesAsync();
        return (await WithPolicyAsync(appointment, appointment.Slot), null);
    }

    private async Task<AppointmentDto> WithPolicyAsync(Appointment appointment, AvailabilitySlot slot) =>
        ToDto(appointment, slot) with { Rescheduling = (await policy.BuildAsync(appointment.PatientId))[appointment.Id] };

    private async Task AssignReleasedSlotOrMakeAvailableAsync(AvailabilitySlot slot)
    {
        if (!slot.IsEnabled)
        {
            slot.IsAvailable = true;
            return;
        }

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
        $"CIT-{PeruClock.Now:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

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
            slot.Professional.Specialty.Price,
            appointment.AttendanceStatus.ToString(),
            appointment.AttendanceRequestedAt,
            appointment.AttendanceRespondedAt);
}

