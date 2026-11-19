using System.Security.Claims;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

// HU-05: cada usuario consulta y edita sus propios datos de contacto.
[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController(AppDbContext db) : ControllerBase
{
    private static readonly PasswordHasher<User> Hasher = new();

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await FindCurrentUserAsync();
        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateProfileRequest request)
    {
        var user = await FindCurrentUserAsync();
        if (user is null) return Unauthorized();

        var email = request.Email.Trim().ToLowerInvariant();
        var phone = request.Phone.Trim();
        var emailChanged = !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase);

        if (emailChanged)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                Hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                return BadRequest(new { field = "currentPassword", message = "La contraseña actual no es correcta." });
            }

            if (await db.Users.AnyAsync(x => x.Id != user.Id && x.Email.ToLower() == email))
                return Conflict(new { field = "email", message = "Ese correo ya está registrado en otra cuenta." });
        }

        user.Email = email;
        user.Phone = phone;
        await db.SaveChangesAsync();

        return Ok(ToDto(user));
    }

    private async Task<User?> FindCurrentUserAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return null;

        return await db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);
    }

    private static ProfileDto ToDto(User user) => new(user.Dni, user.FullName, user.Role, user.Email, user.Phone);
}
