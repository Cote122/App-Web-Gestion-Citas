using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Dtos;

public record MedicalDocumentDto(
    Guid Id, Guid AppointmentId, string FileName, string ContentType,
    long? SizeBytes, DateTimeOffset CreatedAt, bool IsVisibleToPatient, bool CanChangeVisibility);

public record UpdateMedicalDocumentVisibilityDto([property: Required] bool? IsVisibleToPatient);
