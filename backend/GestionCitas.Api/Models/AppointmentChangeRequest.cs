using System.ComponentModel.DataAnnotations;
using GestionCitas.Api.Common;

namespace GestionCitas.Api.Models;

public class AppointmentChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public Guid RequestedByProfessionalId { get; set; }
    public Professional RequestedByProfessional { get; set; } = null!;

    public AppointmentChangeRequestType Type { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    public Guid? TargetSlotId { get; set; }
    public AvailabilitySlot? TargetSlot { get; set; }

    public AppointmentChangeRequestStatus Status { get; set; } =
        AppointmentChangeRequestStatus.PendienteAprobacion;

    public DateTimeOffset CreatedAt { get; set; } = PeruClock.Now;

    public Guid? ReviewedByAdminId { get; set; }
    public User? ReviewedByAdmin { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    [MaxLength(500)]
    public string? AdminComment { get; set; }
}