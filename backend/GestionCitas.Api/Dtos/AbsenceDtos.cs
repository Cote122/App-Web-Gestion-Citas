using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Dtos;

public sealed class CreateAbsenceRequest
{
    [Required(ErrorMessage = "Elige el tipo de ausencia.")]
    [RegularExpression("^(Vacaciones|Licencia)$", ErrorMessage = "El tipo debe ser Vacaciones o Licencia.")]
    public string Type { get; init; } = string.Empty;

    [Required(ErrorMessage = "Indica la fecha de inicio.")]
    public DateOnly? StartDate { get; init; }

    [Required(ErrorMessage = "Indica la fecha de fin.")]
    public DateOnly? EndDate { get; init; }

    [MaxLength(200, ErrorMessage = "El motivo no puede superar los 200 caracteres.")]
    [RegularExpression(@"^[^<>{}\[\]\\]*$", ErrorMessage = "El motivo tiene caracteres no permitidos.")]
    public string? Reason { get; init; }
}

public record AbsenceDto(
    Guid Id,
    string Type,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    string Status,
    int AffectedCount,
    bool CanCancel);

public record AffectedAppointmentDto(
    Guid Id,
    string Code,
    string PatientName,
    string PatientEmail,
    DateTimeOffset StartAt);

public record CreateAbsenceResponse(AbsenceDto Absence, List<AffectedAppointmentDto> AffectedAppointments);
