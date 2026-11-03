using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Services;

/// <summary>HU-14: reglas para no ofrecer ni reservar horarios durante vacaciones o licencias.</summary>
public static class AbsenceRules
{
    public const string AbsenceMessage =
        "El profesional no atiende en esa fecha por vacaciones o licencia. Elige otro horario.";

    /// <summary>Excluye los horarios que se cruzan con una ausencia activa de su profesional.</summary>
    public static IQueryable<AvailabilitySlot> WithoutAbsences(this IQueryable<AvailabilitySlot> slots, AppDbContext db) =>
        slots.Where(slot => !db.ProfessionalAbsences.Any(absence =>
            absence.IsActive &&
            absence.ProfessionalId == slot.ProfessionalId &&
            absence.StartAt < slot.EndAt &&
            absence.EndAt > slot.StartAt));

    public static Task<bool> IsInAbsenceAsync(AppDbContext db, AvailabilitySlot slot) =>
        db.ProfessionalAbsences.AnyAsync(absence =>
            absence.IsActive &&
            absence.ProfessionalId == slot.ProfessionalId &&
            absence.StartAt < slot.EndAt &&
            absence.EndAt > slot.StartAt);
}
