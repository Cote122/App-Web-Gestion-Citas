using System.Data;
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
            .Where(s => s.ProfessionalId == professional.Id && s.IsAvailable && s.StartAt > DateTimeOffset.UtcNow)
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        var dtos = slots.Select(s => new AvailableSlotDto(s.Id, s.StartAt, s.EndAt)).ToList();

        return (dtos, null);
    }

    public async Task<(bool Success, string? Error)> CancelAppointmentAsync(Guid userId, Guid appointmentId)
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

        if (appointment.Slot.StartAt <= DateTimeOffset.UtcNow)
            return (false, "No se puede cancelar una cita cuyo horario ya inició.");

        appointment.Status = AppointmentStatus.Cancelada;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        appointment.Slot.IsAvailable = true;

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return (true, null);
    }

    public async Task<(AppointmentDto? Appointment, string? Error)> RescheduleAppointmentAsync(Guid userId, Guid appointmentId, Guid newSlotId)
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

        if (current.Slot.StartAt <= DateTimeOffset.UtcNow)
            return (null, "No se puede reprogramar una cita cuyo horario ya inició.");

        if (current.SlotId == newSlotId)
            return (null, "Selecciona un horario diferente al de tu cita actual.");

        var newSlot = await db.AvailabilitySlots
            .Include(x => x.Professional)
            .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == newSlotId && x.ProfessionalId == professional.Id);

        if (newSlot is null || !newSlot.IsAvailable || newSlot.StartAt <= DateTimeOffset.UtcNow)
            return (null, "El nuevo horario seleccionado no está disponible o no te pertenece.");

        var now = DateTimeOffset.UtcNow;

        // 1. Marcar la actual como Reprogramada
        current.Status = AppointmentStatus.Reprogramada;
        current.UpdatedAt = now;

        // 2. Ocupar el nuevo slot
        newSlot.IsAvailable = false;

        // 3. Crear la nueva cita ligada a la anterior
        var replacement = new Appointment
        {
            Code = $"CIT-{now:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            PatientId = current.PatientId,
            SlotId = newSlot.Id,
            Status = AppointmentStatus.Confirmada,
            PreviousAppointmentId = current.Id
        };

        db.Appointments.Add(replacement);

        // 4. Liberar el slot anterior de la cita original
        current.Slot.IsAvailable = true;

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

    private Task<Professional?> LinkedProfessionalAsync(Guid userId) =>
        db.Professionals.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);

    private static AppointmentDto ToAppointmentDto(Appointment appointment, AvailabilitySlot slot) =>
        new(
            appointment.Id,
            appointment.Code,
            slot.Professional.Specialty.Name,
            slot.Professional.FullName,
            slot.Professional.Office,
            slot.StartAt,
            slot.EndAt,
            appointment.Status.ToString(),
            slot.Professional.Specialty.Price
        );

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
                appointment.Patient.Email));
}