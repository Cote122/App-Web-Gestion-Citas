namespace GestionCitas.Api.Dtos;

public record SaveSpecialtyRequest(string? Name, string? Description, decimal Price);
public record SaveProfessionalRequest(string? FullName, string? Office, Guid SpecialtyId);
public record SetActiveRequest(bool IsActive);
public record SendTestEmailRequest(string Email);

public record AdminSpecialtyDto(Guid Id, string Name, string Description, decimal Price, bool IsActive, int ProfessionalCount);
// HU-44: UserEmail y UserDni son la cuenta con la que inicia sesión (null en profesionales antiguos de prueba).
public record AdminProfessionalDto(Guid Id, string FullName, string Office, Guid SpecialtyId, string SpecialtyName, bool SpecialtyIsActive, bool IsActive, string? UserEmail, string? UserDni);

// HU-44: el profesional se registra junto con su cuenta de acceso (rol Profesional).
public sealed class CreateProfessionalRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El DNI es obligatorio.")]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe tener exactamente 8 números.")]
    public string Dni { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El correo es obligatorio.")]
    [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "El correo no puede superar los 100 caracteres.")]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^\s*[^@\s<>()\[\],;:""]+@[^@\s<>()\[\],;:""]+\.[A-Za-z]{2,}\s*$", ErrorMessage = "Ingresa un correo válido.")]
    public string Email { get; init; } = string.Empty;

    // Mismas reglas de contraseña que el registro de pacientes.
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "La contraseña es obligatoria.")]
    [System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [System.ComponentModel.DataAnnotations.MaxLength(64, ErrorMessage = "La contraseña no puede superar los 64 caracteres.")]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^(?=.*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])(?=.*\d).+$", ErrorMessage = "La contraseña debe tener al menos una letra y un número.")]
    public string Password { get; init; } = string.Empty;

    // Nombre que verán los pacientes: llega lleno con el DNI y se le puede agregar "Dr." o "Dra." al registrar.
    public string? FullName { get; init; }
    public string? Office { get; init; }
    public Guid SpecialtyId { get; init; }
}
