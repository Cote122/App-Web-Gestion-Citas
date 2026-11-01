using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Services;

/// <summary>HU-39: agenda del profesional autenticado, limitada a sus propias citas.</summary>
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
            .Where(x => x.Slot.ProfessionalId == professional.Id && x.Status != AppointmentStatus.Atendida)
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

    private Task<Professional?> LinkedProfessionalAsync(Guid userId) =>
        db.Professionals.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);

    public static DateOnly TodayInClinic() =>
        DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(ClinicOffset).DateTime);

    private static DateTimeOffset StartOfDay(DateOnly day) =>
        new(day.Year, day.Month, day.Day, 0, 0, 0, ClinicOffset);

    private static ProfessionalAgendaItemDto ToItem(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.Code,
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
