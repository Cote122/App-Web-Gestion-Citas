using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class Professional
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Office { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public ICollection<AvailabilitySlot> Slots { get; set; } = new List<AvailabilitySlot>();
}
