using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class WaitlistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PatientId { get; set; }
    public User Patient { get; set; } = null!;

    // HU-13: la espera pertenece a un horario concreto, no solo a una especialidad.
    public Guid SlotId { get; set; }
    public AvailabilitySlot Slot { get; set; } = null!;

    // Se conservan como datos de apoyo/compatibilidad para reportes.
    public Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;

    public Guid? ProfessionalId { get; set; }
    public Professional? Professional { get; set; }

    public DateTimeOffset? PreferredFrom { get; set; }
    public DateTimeOffset? PreferredTo { get; set; }

    [MaxLength(250)]
    public string Notes { get; set; } = string.Empty;

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Activa;

    public Guid? AssignedAppointmentId { get; set; }
    public Appointment? AssignedAppointment { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
