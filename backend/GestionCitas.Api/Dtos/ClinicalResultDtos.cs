using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Dtos;

public record ClinicalResultFileDto(Guid Id, string FileName, string ContentType, long? SizeBytes, DateTimeOffset UploadedAt);
public record ClinicalResultDto(Guid Id, string ResultType, string Title, string? Summary,
    string Status, DateTimeOffset? ResultDate, DateTimeOffset? PublishedAt,
    bool IsVisibleToPatient, bool CanManage, IReadOnlyList<ClinicalResultFileDto> Files);

public sealed class SaveClinicalResultDto
{
    [Required, StringLength(100)] public string ResultType { get; init; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; init; } = string.Empty;
    [StringLength(10000)] public string? Summary { get; init; }
    public DateTimeOffset? ResultDate { get; init; }
}

public sealed class PublishClinicalResultDto
{
    [Required] public bool? Publish { get; init; }
}
