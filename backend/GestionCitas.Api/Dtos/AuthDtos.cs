namespace GestionCitas.Api.Dtos;

public record LoginRequest(string Identifier, string Password);
public record RegisterPatientRequest(string Dni, string Email, string FullName, string Password);
public record UserDto(Guid Id, string Dni, string Email, string FullName, string Role);
public record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);
