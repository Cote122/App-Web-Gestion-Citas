using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(20)]
    public string Dni { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Role { get; set; } = "Paciente";

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
