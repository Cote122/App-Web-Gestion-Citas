using System.ComponentModel.DataAnnotations;
namespace GestionCitas.Api.Dtos;

public sealed class PrescriptionItemRequest
{
    [Required, StringLength(200)] public string Medication { get; init; } = string.Empty;
    [StringLength(100)] public string? Dose { get; init; }
    [StringLength(100)] public string? Frequency { get; init; }
    [StringLength(100)] public string? Duration { get; init; }
    [StringLength(500)] public string? Instructions { get; init; }
}
public sealed class IssuePrescriptionRequest
{
    [StringLength(10000)] public string? GeneralIndications { get; init; }
    [Required, MaxLength(20)] public List<PrescriptionItemRequest> Items { get; init; } = [];
}
public record PrescriptionItemDto(string Medication, string? Dose, string? Frequency, string? Duration, string? Instructions);
public record PrescriptionDto(Guid Id, DateTimeOffset IssuedAt, string Professional, string? GeneralIndications,
    bool IsActive, bool CanDeactivate, IReadOnlyList<PrescriptionItemDto> Items);
public record PatientIndicationDto(Guid Id, DateTimeOffset AttentionDate, string Professional, string Content);

public sealed class CorrectPrescriptionRequest
{
    [Required, RegularExpression("^[0-9A-Fa-f]{64}$")] public string ExpectedVersion { get; init; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; init; } = string.Empty;
    [Required] public IssuePrescriptionRequest Content { get; init; } = new();
}
public record AdminPrescriptionDto(Guid Id, DateTimeOffset IssuedAt, string Patient, string PatientDni,
    string Professional, bool IsActive, string? GeneralIndications, IReadOnlyList<PrescriptionItemDto> Items, string Version);
public record PrescriptionCorrectionDto(Guid Id, DateTimeOffset CorrectedAt, string Administrator, string Reason);
