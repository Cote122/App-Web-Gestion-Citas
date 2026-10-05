namespace GestionCitas.Api.Dtos;

public record CreateAppointmentRequest(Guid SlotId);
public record RescheduleAppointmentRequest(Guid SlotId);
public record CreateWaitlistRequest(Guid SpecialtyId, Guid? ProfessionalId, DateTimeOffset? PreferredFrom, DateTimeOffset? PreferredTo, string? Notes);

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
