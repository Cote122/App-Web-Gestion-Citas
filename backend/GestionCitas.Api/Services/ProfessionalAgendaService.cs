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
            .Where(s => s.ProfessionalId == professional.Id && s.IsEnabled && s.IsAvailable && s.StartAt > PeruClock.Now
                        && s.Professional.IsActive && s.Professional.Specialty.IsActive)
            .WithoutAbsences(db)
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        var dtos = slots.Select(s => new AvailableSlotDto(s.Id, s.StartAt, s.EndAt)).ToList();

        return (dtos, null);
    }

    public async Task<(AppointmentChangeRequestDto? Request, string? Error)>
        CancelAppointmentAsync(Guid userId, Guid appointmentId, string? reason = null)
    {
        var professional = await LinkedProfessionalAsync(userId);

        if (professional is null)
            return (null, "Tu usuario no está vinculado a un profesional.");

        var now = PeruClock.Now;

        var appointment = await db.Appointments
            .Include(x => x.Slot)
            .FirstOrDefaultAsync(x =>
                x.Id == appointmentId &&
                x.Slot.ProfessionalId == professional.Id);

        if (appointment is null)
            return (null, "Cita no encontrada o no te pertenece.");

        if (appointment.Status != AppointmentStatus.Confirmada)
            return (null, "La cita ya no puede cancelarse.");

        if (appointment.Slot.StartAt <= now)
            return (null, "No se puede cancelar una cita cuyo horario ya inició.");

        if (reason?.Length > 500)
            return (null, "El motivo no puede superar los 500 caracteres.");

        if (await HasPendingChangeRequestAsync(appointment.Id))
            return (null, "La cita ya tiene una solicitud pendiente de aprobación.");

        var request = new AppointmentChangeRequest
        {
            AppointmentId = appointment.Id,
            RequestedByProfessionalId = professional.Id,
            Type = AppointmentChangeRequestType.Cancelacion,
            Reason = NormalizeReason(reason),
            Status = AppointmentChangeRequestStatus.PendienteAprobacion,
            CreatedAt = now
        };

        var error = await SavePendingRequestAsync(request);

        if (error is not null)
            return (null, error);

        return (
            ToChangeRequestDto(
                request,
                appointment.Code,
                appointment.Slot.StartAt,
                null
            ),
            null
        );
    }

    public async Task<(AppointmentChangeRequestDto? Request, string? Error)>
        RescheduleAppointmentAsync(
            Guid userId,
            Guid appointmentId,
            Guid newSlotId,
            string? reason = null)
    {
        var professional = await LinkedProfessionalAsync(userId);

        if (professional is null)
            return (null, "Tu usuario no está vinculado a un profesional.");

        var now = PeruClock.Now;

        var current = await db.Appointments
            .Include(x => x.Slot)
            .FirstOrDefaultAsync(x =>
                x.Id == appointmentId &&
                x.Slot.ProfessionalId == professional.Id);

        if (current is null)
            return (null, "Cita no encontrada o no te pertenece.");

        if (current.Status != AppointmentStatus.Confirmada)
            return (null, "La cita ya no puede reprogramarse.");

        if (current.Slot.StartAt <= now)
            return (null, "No se puede reprogramar una cita cuyo horario ya inició.");

        if (current.SlotId == newSlotId)
            return (null, "Selecciona un horario diferente al de tu cita actual.");

        if (reason?.Length > 500)
            return (null, "El motivo no puede superar los 500 caracteres.");

        if (await HasPendingChangeRequestAsync(current.Id))
            return (null, "La cita ya tiene una solicitud pendiente de aprobación.");

        var targetSlot = await db.AvailabilitySlots
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == newSlotId &&
                x.ProfessionalId == professional.Id);

        if (targetSlot is null ||
            !targetSlot.IsEnabled ||
            !targetSlot.IsAvailable ||
            targetSlot.StartAt <= now ||
            await AbsenceRules.IsInAbsenceAsync(db, targetSlot))
        {
            return (
                null,
                "El nuevo horario seleccionado no está disponible o no te pertenece."
            );
        }

        var request = new AppointmentChangeRequest
        {
            AppointmentId = current.Id,
            RequestedByProfessionalId = professional.Id,
            Type = AppointmentChangeRequestType.Reprogramacion,
            Reason = NormalizeReason(reason),
            TargetSlotId = targetSlot.Id,
            Status = AppointmentChangeRequestStatus.PendienteAprobacion,
            CreatedAt = now
        };

        var error = await SavePendingRequestAsync(request);

        if (error is not null)
            return (null, error);

        return (
            ToChangeRequestDto(
                request,
                current.Code,
                current.Slot.StartAt,
                targetSlot.StartAt
            ),
            null
        );
    }

    public async Task<(
        List<AppointmentChangeRequestDto>? Requests,
        string? Error
    )> ListChangeRequestsAsync(Guid userId)
    {
        var professional = await LinkedProfessionalAsync(userId);

        if (professional is null)
            return (null, "Tu usuario no está vinculado a un profesional.");

        var requests = await db.AppointmentChangeRequests
            .AsNoTracking()
            .Where(x => x.RequestedByProfessionalId == professional.Id)
            .Include(x => x.Appointment)
                .ThenInclude(x => x.Slot)
            .Include(x => x.TargetSlot)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var result = requests.Select(x =>
            ToChangeRequestDto(
                x,
                x.Appointment.Code,
                x.Appointment.Slot.StartAt,
                x.TargetSlot?.StartAt
            )
        ).ToList();

        return (result, null);
    }

    private Task<Professional?> LinkedProfessionalAsync(Guid userId) =>
        db.Professionals.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);

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

    private async Task<bool> HasPendingChangeRequestAsync(Guid appointmentId)
    {
        return await db.AppointmentChangeRequests.AnyAsync(x =>
            x.AppointmentId == appointmentId &&
            x.Status == AppointmentChangeRequestStatus.PendienteAprobacion
        );
    }

    private async Task<string?> SavePendingRequestAsync(
        AppointmentChangeRequest request)
    {
        db.AppointmentChangeRequests.Add(request);

        try
        {
            await db.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateException)
        {
            db.Entry(request).State = EntityState.Detached;

            // Puede ocurrir si otra solicitud se registra simultáneamente.
            if (await HasPendingChangeRequestAsync(request.AppointmentId))
            {
                return "La cita ya tiene una solicitud pendiente de aprobación.";
            }

            throw;
        }
    }

    private static string? NormalizeReason(string? reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim();
    }

    private static AppointmentChangeRequestDto ToChangeRequestDto(
        AppointmentChangeRequest request,
        string appointmentCode,
        DateTimeOffset originalSlotStartAt,
        DateTimeOffset? targetSlotStartAt)
    {
        return new AppointmentChangeRequestDto(
            request.Id,
            request.AppointmentId,
            appointmentCode,
            request.Type.ToString(),
            request.Status.ToString(),
            request.Reason,
            request.TargetSlotId,
            originalSlotStartAt,
            targetSlotStartAt,
            request.CreatedAt,
            request.ReviewedAt,
            request.AdminComment
        );
    }
}
