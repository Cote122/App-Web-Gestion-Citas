namespace GestionCitas.Api.Dtos;

public record CreateAppointmentRequest(Guid SlotId);
public record RescheduleAppointmentRequest(Guid SlotId);
public record CreateWaitlistRequest(Guid SlotId);
public record AttendanceResponseRequest(string Response);

public record AppointmentDto(
    Guid Id,
    string Code,
    Guid SpecialtyId,
    string Specialty,
    Guid ProfessionalId,
    string Professional,
    string Office,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    decimal Price,
    string AttendanceStatus,
    DateTimeOffset? AttendanceRequestedAt,
    DateTimeOffset? AttendanceRespondedAt);

public record WaitlistItemDto(
    Guid Id,
    Guid SlotId,
    string Specialty,
    string Professional,
    string Office,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    int Position,
    DateTimeOffset CreatedAt);

public record CommunicationProcessResult(
    int RemindersCreated,
    int AttendanceRequestsCreated);

public record AdminAttendanceDto(
    Guid AppointmentId,
    string Code,
    Guid PatientId,
    string Patient,
    string PatientEmail,
    string Specialty,
    string Professional,
    DateTimeOffset StartAt,
    string AttendanceStatus,
    DateTimeOffset? AttendanceRequestedAt,
    DateTimeOffset? AttendanceRespondedAt);
