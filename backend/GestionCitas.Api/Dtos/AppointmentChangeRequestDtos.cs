namespace GestionCitas.Api.Dtos;

public record AppointmentChangeRequestDto(
    Guid Id,
    Guid AppointmentId,
    string AppointmentCode,
    string Type,
    string Status,
    string? Reason,
    Guid? TargetSlotId,
    DateTimeOffset OriginalSlotStartAt,
    DateTimeOffset? TargetSlotStartAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? AdminComment
);

public record AdminAppointmentChangeRequestDto(
    Guid Id,
    Guid AppointmentId,
    string AppointmentCode,
    string PatientName,
    string ProfessionalName,
    string SpecialtyName,
    string Type,
    string Status,
    string? Reason,
    DateTimeOffset OriginalSlotStartAt,
    DateTimeOffset? TargetSlotStartAt,
    DateTimeOffset CreatedAt
);

public record ReviewAppointmentChangeRequest(
    string? AdminComment
);