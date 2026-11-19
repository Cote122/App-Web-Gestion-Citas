using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Dtos;

// HU-05: DNI, nombre y rol se muestran, pero no se pueden editar.
public record ProfileDto(string Dni, string FullName, string Role, string Email, string? Phone);

public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El correo no puede superar los 100 caracteres.")]
    [RegularExpression(@"^\s*[^@\s<>()\[\],;:""]+@[^@\s<>()\[\],;:""]+\.[A-Za-z]{2,}\s*$", ErrorMessage = "Ingresa un correo válido.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "El celular es obligatorio.")]
    [RegularExpression(@"^9\d{8}$", ErrorMessage = "Debe tener 9 dígitos y empezar con 9.")]
    public string Phone { get; init; } = string.Empty;

    // Solo se pide si cambia el correo, porque el correo sirve para iniciar sesión.
    [MaxLength(64)]
    public string? CurrentPassword { get; init; }
}
