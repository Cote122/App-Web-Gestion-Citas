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

    // --- NUEVOS MÉTODOS PARA RESOLVER EL ERROR ---

    /// HU-41: Obtiene los slots disponibles únicamente del profesional autenticado.
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

    /// HU-41: Cancela una cita del profesional y libera su slot.
    public async Task<(bool Success, string? Error)> CancelAppointmentAsync(Guid userId, Guid appointmentId)
    {
        var professional = await LinkedProfessionalAsync(userId);
        if (professional is null)
            return (false, "Tu usuario no está vinculado a un profesional.");

        var appointment = await db.Appointments
            .Include(x => x.Slot)
            .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Slot.ProfessionalId == professional.Id);

        if (appointment is null)
            return (false, "Cita no encontrada o no te pertenece.");

        if (appointment.Status == AppointmentStatus.Cancelada)
            return (false, "La cita ya se encuentra cancelada.");

        appointment.Slot.IsAvailable = true;
        appointment.Status = AppointmentStatus.Cancelada;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        return (true, null);
    }

    /// HU-41: Reprograma una cita a un nuevo slot disponible del profesional.
    public async Task<(bool Success, string? Error)> RescheduleAppointmentAsync(Guid userId, Guid appointmentId, Guid newSlotId)
    {
        var professional = await LinkedProfessionalAsync(userId);
        if (professional is null)
            return (false, "Tu usuario no está vinculado a un profesional.");

        var appointment = await db.Appointments
            .Include(x => x.Slot)
            .FirstOrDefaultAsync(x => x.Id == appointmentId && x.Slot.ProfessionalId == professional.Id);

        if (appointment is null)
            return (false, "Cita no encontrada o no te pertenece.");

        var newSlot = await db.AvailabilitySlots
            .FirstOrDefaultAsync(s => s.Id == newSlotId && s.ProfessionalId == professional.Id);

        if (newSlot is null || !newSlot.IsAvailable)
            return (false, "El horario seleccionado no está disponible o no te pertenece.");

        appointment.Slot.IsAvailable = true;
        newSlot.IsAvailable = false;
        appointment.SlotId = newSlot.Id;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        return (true, null);
    }

    private Task<Professional?> LinkedProfessionalAsync(Guid userId) =>
        db.Professionals.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);

    public static DateOnly TodayInClinic() =>
        DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(ClinicOffset).DateTime);

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