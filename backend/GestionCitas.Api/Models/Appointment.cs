using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    public Guid PatientId { get; set; }
    public User Patient { get; set; } = null!;

    public Guid SlotId { get; set; }
    public AvailabilitySlot Slot { get; set; } = null!;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmada;
    public Guid? PreviousAppointmentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
