namespace GestionCitas.Api.Dtos;

public record ProfessionalAgendaItemDto(
    Guid Id,
    string Code,
    string PatientName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Specialty,
    string? Reason,
    string Status,
    string Office);

public record ProfessionalPatientDto(
    Guid Id,
    string Dni,
    string FullName,
    string Email);

public record ProfessionalAppointmentDetailDto(
    Guid Id,
    string Code,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Specialty,
    string Office,
    string? Reason,
    string Status,
    ProfessionalPatientDto Patient);
