using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class AppointmentAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public Guid PatientId { get; set; }

    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    public Guid? PreviousSlotId { get; set; }
    public Guid? NewSlotId { get; set; }

    [MaxLength(1000)]
    public string Details { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
