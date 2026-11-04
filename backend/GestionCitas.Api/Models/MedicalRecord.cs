using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class MedicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PatientId { get; set; }
    public User Patient { get; set; } = null!;

    public Guid ProfessionalId { get; set; }
    public Professional Professional { get; set; } = null!;

    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    [MaxLength(2000)]
    public string? Observations { get; set; }

    [MaxLength(500)]
    public string? Diagnosis { get; set; }

    [MaxLength(1000)]
    public string? Recommendations { get; set; }

    public DateTimeOffset AttentionDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
