using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Administrador")]
public class AdminUsersController(AppDbContext db) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string[]> RolePermissions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Paciente"] = ["Citas.VerPropias", "Citas.Crear", "Citas.Cancelar", "Citas.Reprogramar", "Disponibilidad.Consultar"],
            ["Profesional"] = ["Pacientes.VerAsignados", "Atenciones.Registrar", "Historial.VerAutorizado"],
            ["Caja"] = ["Citas.Ver", "Citas.Registrar", "Pagos.Gestionar", "Disponibilidad.Gestionar"],
            ["Administrador"] = ["Usuarios.Gestionar", "Roles.Gestionar", "Especialidades.Gestionar", "Profesionales.Gestionar", "Auditoria.Ver"]
        };

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new AdminUserDto(x.Id, x.Dni, x.Email, x.FullName, x.Role, x.IsActive))
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        var roles = RolePermissions
            .OrderBy(x => x.Key)
            .Select(x => new RoleDefinitionDto(x.Key, x.Value))
            .ToList();

        return Ok(roles);
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, UpdateUserRoleRequest request)
    {
        var role = request.Role?.Trim() ?? string.Empty;
        if (!RolePermissions.ContainsKey(role))
            return BadRequest(new { message = "El rol indicado no es válido." });

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
            return NotFound(new { message = "Usuario no encontrado." });

        user.Role = RolePermissions.Keys.First(x => x.Equals(role, StringComparison.OrdinalIgnoreCase));
        await db.SaveChangesAsync();

        return Ok(new AdminUserDto(user.Id, user.Dni, user.Email, user.FullName, user.Role, user.IsActive));
    }
}
