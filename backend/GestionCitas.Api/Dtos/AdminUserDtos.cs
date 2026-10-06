namespace GestionCitas.Api.Dtos;

public record AdminUserDto(Guid Id, string Dni, string Email, string FullName, string Role, bool IsActive);
public record UpdateUserRoleRequest(string Role);
public record RoleDefinitionDto(string Role, IReadOnlyList<string> Permissions);
