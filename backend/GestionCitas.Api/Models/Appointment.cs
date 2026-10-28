using System.ComponentModel.DataAnnotations;
using GestionCitas.Api.Common;

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

    public AttendanceConfirmationStatus AttendanceStatus { get; set; } =
        AttendanceConfirmationStatus.Pendiente;

    public DateTimeOffset? AttendanceRequestedAt { get; set; }
    public DateTimeOffset? AttendanceRespondedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = PeruClock.Now;
    public DateTimeOffset? UpdatedAt { get; set; }
}
