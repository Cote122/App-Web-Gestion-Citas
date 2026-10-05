using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class WaitlistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public User Patient { get; set; } = null!;

    public Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;

    public Guid? ProfessionalId { get; set; }
    public Professional? Professional { get; set; }

    public DateTimeOffset? PreferredFrom { get; set; }
    public DateTimeOffset? PreferredTo { get; set; }

    [MaxLength(250)]
    public string Notes { get; set; } = string.Empty;

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Activa;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
