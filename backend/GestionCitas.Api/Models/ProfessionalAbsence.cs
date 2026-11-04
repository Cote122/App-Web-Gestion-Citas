using System.ComponentModel.DataAnnotations;
using GestionCitas.Api.Common;

namespace GestionCitas.Api.Models;

public enum AbsenceType
{
    Vacaciones,
    Licencia
}

/// <summary>
/// HU-14: periodo de vacaciones o licencia de un profesional.
/// StartAt es el inicio del primer día y EndAt el inicio del día siguiente al último (hora de Perú).
/// </summary>
public class ProfessionalAbsence
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProfessionalId { get; set; }
    public Professional Professional { get; set; } = null!;

    public AbsenceType Type { get; set; } = AbsenceType.Vacaciones;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }

    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = PeruClock.Now;
    public DateTimeOffset? CancelledAt { get; set; }
}
