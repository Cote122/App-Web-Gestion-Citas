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
