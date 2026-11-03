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
    [MaxLength(100, ErrorMessage = "El correo no puede superar los 100 caracteres.")]
    [RegularExpression(@"^\s*[^@\s<>()\[\],;:""]+@[^@\s<>()\[\],;:""]+\.[A-Za-z]{2,}\s*$", ErrorMessage = "Ingresa un correo válido.")]
    public string Email { get; init; } = string.Empty;

    // HU-37: el nombre se toma del servicio de DNI al registrar; este valor solo se usa para mostrarlo.
    [MaxLength(150, ErrorMessage = "El nombre completo no puede superar los 150 caracteres.")]
    public string FullName { get; init; } = string.Empty;

    // HU-38: celular peruano.
    [Required(ErrorMessage = "El celular es obligatorio.")]
    [RegularExpression(@"^9\d{8}$", ErrorMessage = "Debe tener 9 dígitos y empezar con 9.")]
    public string Phone { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [MaxLength(64, ErrorMessage = "La contraseña no puede superar los 64 caracteres.")]
    [RegularExpression(@"^(?=.*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])(?=.*\d).+$", ErrorMessage = "La contraseña debe tener al menos una letra y un número.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Confirma tu contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public record UserDto(Guid Id, string Dni, string Email, string FullName, string Role);
public record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);
