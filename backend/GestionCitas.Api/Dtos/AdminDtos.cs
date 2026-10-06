namespace GestionCitas.Api.Dtos;

public record SaveSpecialtyRequest(string? Name, string? Description, decimal Price);
public record SaveProfessionalRequest(string? FullName, string? Office, Guid SpecialtyId);
public record SetActiveRequest(bool IsActive);

public record AdminSpecialtyDto(Guid Id, string Name, string Description, decimal Price, bool IsActive, int ProfessionalCount);
public record AdminProfessionalDto(Guid Id, string FullName, string Office, Guid SpecialtyId, string SpecialtyName, bool SpecialtyIsActive, bool IsActive);
