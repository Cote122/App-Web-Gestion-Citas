using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class AvailabilitySlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfessionalId { get; set; }
    public Professional Professional { get; set; } = null!;

    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public bool IsAvailable { get; set; } = true;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
