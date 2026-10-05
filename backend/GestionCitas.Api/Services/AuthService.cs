using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GestionCitas.Api.Services;

public class AuthService(AppDbContext db, IConfiguration configuration)
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var identifier = request.Identifier.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x =>
            x.IsActive && (x.Dni == request.Identifier.Trim() || x.Email.ToLower() == identifier));

        if (user is null) return null;

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed) return null;

        return BuildLoginResponse(user);
    }

    public async Task<(LoginResponse? Response, string? Error)> RegisterPatientAsync(RegisterPatientRequest request)
    {
        var dni = request.Dni.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(x => x.Dni == dni || x.Email.ToLower() == email))
            return (null, "Ya existe una cuenta asociada al DNI o correo indicado.");

        if (request.Password.Length < 8)
            return (null, "La contraseña debe tener al menos 8 caracteres.");

        var user = new User
        {
            Dni = dni,
            Email = email,
            FullName = request.FullName.Trim(),
            Role = "Paciente"
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (BuildLoginResponse(user), null);
    }

    private LoginResponse BuildLoginResponse(User user)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(2);
        var key = configuration["Jwt:Key"]!;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("dni", user.Dni)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return new LoginResponse(
            tokenValue,
            expires,
            new UserDto(user.Id, user.Dni, user.Email, user.FullName, user.Role));
    }
}
