using System.Data;
using GestionCitas.Api.Common;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Services;

public class AdminAppointmentChangeService(AppDbContext db)
{
    public async Task<List<AdminAppointmentChangeRequestDto>> ListPendingAsync()
    {
        var requests = await db.AppointmentChangeRequests
            .AsNoTracking()
            .Where(x => x.Status == AppointmentChangeRequestStatus.PendienteAprobacion)
            .Include(x => x.Appointment)
                .ThenInclude(x => x.Patient)
            .Include(x => x.Appointment)
                .ThenInclude(x => x.Slot)
                    .ThenInclude(x => x.Professional)
                        .ThenInclude(x => x.Specialty)
            .Include(x => x.TargetSlot)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        return requests.Select(x => new AdminAppointmentChangeRequestDto(
            x.Id,
            x.AppointmentId,
            x.Appointment.Code,
            x.Appointment.Patient.FullName,
            x.Appointment.Slot.Professional.FullName,
            x.Appointment.Slot.Professional.Specialty.Name,
            x.Type.ToString(),
            x.Status.ToString(),
            x.Reason,
            x.Appointment.Slot.StartAt,
            x.TargetSlot?.StartAt,
            x.CreatedAt
        )).ToList();
    }

    public async Task<(bool Success, string? Error)> ApproveAsync(
        Guid adminId,
        Guid requestId,
        string? adminComment)
    {
        var admin = await GetAuthorizedAdminAsync(adminId);

        if (admin is null)
            return (false, "No tienes permisos para aprobar solicitudes.");

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        try
        {
            var request = await LoadRequestAsync(requestId);

            if (request is null)
                return (false, "Solicitud no encontrada.");

            if (request.Status != AppointmentChangeRequestStatus.PendienteAprobacion)
                return (false, "La solicitud ya fue resuelta.");

            var appointment = request.Appointment;

            if (appointment.Status != AppointmentStatus.Confirmada)
                return (false, "La cita original ya no está disponible para modificación.");

            if (appointment.Slot.StartAt <= PeruClock.Now)
                return (false, "El horario original de la cita ya inició.");

            if (request.Type == AppointmentChangeRequestType.Cancelacion)
            {
                await ApplyCancellationAsync(request);
            }
            else if (request.Type == AppointmentChangeRequestType.Reprogramacion)
            {
                var error = await ApplyReschedulingAsync(request);

                if (error is not null)
                {
                    if (tx is not null)
                        await tx.RollbackAsync();

                    return (false, error);
                }
            }
            else
            {
                return (false, "Tipo de solicitud no válido.");
            }

            request.Status = AppointmentChangeRequestStatus.Aprobada;
            request.ReviewedByAdminId = adminId;
            request.ReviewedAt = PeruClock.Now;
            request.AdminComment = NormalizeComment(adminComment);

            await db.SaveChangesAsync();

            if (tx is not null)
                await tx.CommitAsync();

            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (tx is not null)
                await tx.RollbackAsync();

            return (false,
                "La solicitud o el horario fueron modificados por otro usuario. Actualiza la información e inténtalo nuevamente.");
        }
        catch
        {
            if (tx is not null)
                await tx.RollbackAsync();

            throw;
        }
    }

    public async Task<(bool Success, string? Error)> RejectAsync(
        Guid adminId,
        Guid requestId,
        string? adminComment)
    {
        var admin = await GetAuthorizedAdminAsync(adminId);

        if (admin is null)
            return (false, "No tienes permisos para rechazar solicitudes.");

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        try
        {
            var request = await db.AppointmentChangeRequests
                .FirstOrDefaultAsync(x => x.Id == requestId);

            if (request is null)
                return (false, "Solicitud no encontrada.");

            if (request.Status != AppointmentChangeRequestStatus.PendienteAprobacion)
                return (false, "La solicitud ya fue resuelta.");

            request.Status = AppointmentChangeRequestStatus.Rechazada;
            request.ReviewedByAdminId = adminId;
            request.ReviewedAt = PeruClock.Now;
            request.AdminComment = NormalizeComment(adminComment);

            await db.SaveChangesAsync();

            if (tx is not null)
                await tx.CommitAsync();

            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (tx is not null)
                await tx.RollbackAsync();

            return (false, "La solicitud fue modificada por otro administrador.");
        }
        catch
        {
            if (tx is not null)
                await tx.RollbackAsync();

            throw;
        }
    }

    private async Task<User?> GetAuthorizedAdminAsync(Guid adminId)
    {
        return await db.Users.FirstOrDefaultAsync(x =>
            x.Id == adminId &&
            x.Role == "Administrador" &&
            x.IsActive);
    }

    private async Task<AppointmentChangeRequest?> LoadRequestAsync(Guid requestId)
    {
        return await db.AppointmentChangeRequests
            .Include(x => x.Appointment)
                .ThenInclude(x => x.Patient)
            .Include(x => x.Appointment)
                .ThenInclude(x => x.Slot)
                    .ThenInclude(x => x.Professional)
                        .ThenInclude(x => x.Specialty)
            .Include(x => x.TargetSlot)
            .FirstOrDefaultAsync(x => x.Id == requestId);
    }

    private async Task ApplyCancellationAsync(AppointmentChangeRequest request)
    {
        var appointment = request.Appointment;
        var slot = appointment.Slot;
        var cancelledAt = PeruClock.Now;

        appointment.Status = AppointmentStatus.Cancelada;
        appointment.UpdatedAt = cancelledAt;

        if (!string.IsNullOrWhiteSpace(request.Reason))
            appointment.Reason = request.Reason;

        db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            Appointment = appointment,
            PatientId = appointment.PatientId,
            EventType = "CancelledByProfessional",
            PreviousSlotId = slot.Id,
            Details =
                $"Cita {appointment.Code} cancelada por aprobación administrativa. " +
                $"Motivo: {request.Reason ?? "No especificado"}. " +
                $"Horario original: {slot.StartAt:dd/MM/yyyy HH:mm}."
        });

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = appointment.PatientId,
            RelatedAppointmentId = appointment.Id,
            RecipientEmail = appointment.Patient.Email,
            Type = "AppointmentCancelled",
            Subject = "Tu cita fue cancelada",
            Body =
                $"Hola {appointment.Patient.FullName}, te informamos que tu cita " +
                $"{appointment.Code}, programada para el {slot.StartAt:dd/MM/yyyy} " +
                $"a las {slot.StartAt:HH:mm}, ha sido cancelada. " +
                $"Motivo: {request.Reason ?? "No especificado"}."
        });

        await AssignReleasedSlotOrMakeAvailableAsync(slot);
    }

    private async Task<string?> ApplyReschedulingAsync(
        AppointmentChangeRequest request)
    {
        var current = request.Appointment;
        var oldSlot = current.Slot;

        // Volvemos a consultar el slot dentro de la transacción.
        var newSlot = await db.AvailabilitySlots
            .Include(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x =>
                x.Id == request.TargetSlotId &&
                x.ProfessionalId == request.RequestedByProfessionalId);

        if (newSlot is null)
            return "El horario solicitado ya no existe o no pertenece al profesional.";

        if (!newSlot.IsEnabled || !newSlot.IsAvailable || newSlot.StartAt <= PeruClock.Now ||
            await AbsenceRules.IsInAbsenceAsync(db, newSlot))
            return "El horario solicitado ya no está disponible.";

        if (newSlot.Id == oldSlot.Id)
            return "El nuevo horario debe ser diferente al horario original.";

        var previousSlotId = current.SlotId;
        var previousStartAt = oldSlot.StartAt;
        var changedAt = PeruClock.Now;

        current.Status = AppointmentStatus.Reprogramada;
        current.UpdatedAt = changedAt;

        if (!string.IsNullOrWhiteSpace(request.Reason))
            current.Reason = request.Reason;

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
            Details =
                $"Cita {current.Code} reprogramada mediante aprobación administrativa. " +
                $"Motivo: {request.Reason ?? "No especificado"}. " +
                $"Horario anterior: {previousStartAt:dd/MM/yyyy HH:mm}. " +
                $"Nuevo horario: {newSlot.StartAt:dd/MM/yyyy HH:mm}. " +
                $"Nueva cita: {replacement.Code}."
        });

        db.NotificationOutbox.Add(new NotificationOutbox
        {
            RecipientUserId = current.PatientId,
            RelatedAppointmentId = current.Id,
            RecipientEmail = current.Patient.Email,
            Type = "AppointmentRescheduled",
            Subject = "Tu cita fue reprogramada",
            Body =
                $"Hola {current.Patient.FullName}, tu cita fue reprogramada. " +
                $"Nuevo horario: {newSlot.StartAt:dd/MM/yyyy} a las {newSlot.StartAt:HH:mm}. " +
                $"Motivo: {request.Reason ?? "No especificado"}. " +
                $"Nuevo código: {replacement.Code}."
        });

        await AssignReleasedSlotOrMakeAvailableAsync(oldSlot);

        return null;
    }

    private async Task AssignReleasedSlotOrMakeAvailableAsync(AvailabilitySlot slot)
    {
        if (!slot.IsEnabled)
        {
            slot.IsAvailable = true;
            return;
        }

        var next = await db.WaitlistEntries
            .Include(x => x.Patient)
            .Where(x =>
                x.SlotId == slot.Id &&
                x.Status == WaitlistStatus.Activa)
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
                $"Hola {next.Patient.FullName}, se liberó el horario del " +
                $"{slot.StartAt:dd/MM/yyyy} a las {slot.StartAt:HH:mm}. " +
                $"Como eras el primero de la lista de espera, la cita fue " +
                $"registrada automáticamente a tu nombre con código {appointment.Code}."
        });
    }

    private static string GenerateCode() =>
        $"CIT-{PeruClock.Now:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static string? NormalizeComment(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
}
