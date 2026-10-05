using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class Specialty
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Professional> Professionals { get; set; } = new List<Professional>();
}
