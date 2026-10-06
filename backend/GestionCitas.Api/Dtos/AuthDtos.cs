using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Dtos;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Ingresa tu DNI o correo.")]
    [MaxLength(200)]
    public string Identifier { get; init; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu contraseña.")]
    [MaxLength(100)]
    public string Password { get; init; } = string.Empty;
}

public sealed class RegisterPatientRequest
{
    [Required(ErrorMessage = "El DNI es obligatorio.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe contener exactamente 8 dígitos.")]
    public string Dni { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [MaxLength(200)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MinLength(3, ErrorMessage = "El nombre completo debe tener al menos 3 caracteres.")]
    [MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [MaxLength(100)]
    public string Password { get; init; } = string.Empty;
}

public record UserDto(Guid Id, string Dni, string Email, string FullName, string Role);
public record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);
