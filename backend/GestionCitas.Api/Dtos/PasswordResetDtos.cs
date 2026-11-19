using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Dtos;

// HU-04: recuperación de contraseña por correo.
public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Ingresa tu correo.")]
    [MaxLength(100, ErrorMessage = "El correo no puede superar los 100 caracteres.")]
    [RegularExpression(@"^\s*[^@\s<>()\[\],;:""]+@[^@\s<>()\[\],;:""]+\.[A-Za-z]{2,}\s*$", ErrorMessage = "Ingresa un correo válido.")]
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "El enlace no es válido.")]
    [MaxLength(300, ErrorMessage = "El enlace no es válido.")]
    public string Token { get; init; } = string.Empty;

    // Mismas reglas de contraseña que el registro.
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [MaxLength(64, ErrorMessage = "La contraseña no puede superar los 64 caracteres.")]
    [RegularExpression(@"^(?=.*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])(?=.*\d).+$", ErrorMessage = "La contraseña debe tener al menos una letra y un número.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Confirma tu contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; init; } = string.Empty;
}
