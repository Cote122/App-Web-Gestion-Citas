namespace GestionCitas.Api.Dtos;

public record CreateAppointmentRequest(Guid SlotId);
public record RescheduleAppointmentRequest(Guid SlotId);
public record CreateWaitlistRequest(Guid SlotId);

public record AppointmentDto(
    Guid Id,
    string Code,
    string Specialty,
    string Professional,
    string Office,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    decimal Price);

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
