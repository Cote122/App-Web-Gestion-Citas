namespace GestionCitas.Api.Dtos;

public record CreateMedicalRecordDto(
    Guid AppointmentId,
    Guid PatientId,
    Guid ProfessionalId,
    DateTimeOffset AttentionDate,
    string? Reason,
    string? Observations,
    string? Diagnosis,
    string? Recommendations
);

public record MedicalRecordDto(
    Guid Id,
    Guid AppointmentId,
    Guid PatientId,
    Guid ProfessionalId,
    string? Reason,
    string? Observations,
    string? Diagnosis,
    string? Recommendations,
    DateTimeOffset AttentionDate
);