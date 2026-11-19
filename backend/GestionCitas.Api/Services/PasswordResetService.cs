using System.Security.Cryptography;
using System.Text;
using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Services;

/// <summary>
/// HU-04: recuperación de contraseña sin cambiar la base de datos.
/// El enlace lleva un token firmado (HMAC) con el usuario, su vencimiento y una
/// "huella" de la contraseña actual. Al cambiar la contraseña, la huella ya no
/// coincide y el enlace deja de servir: por eso se puede usar una sola vez.
/// </summary>
public class PasswordResetService(
    AppDbContext db,
    IConfiguration configuration,
    SmtpEmailSender emailSender,
    SessionRevocationService sessions,
    IHostEnvironment environment,
    ILogger<PasswordResetService> logger)
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(30);
    private const int StampLength = 16;
    private static readonly PasswordHasher<User> Hasher = new();

    /// <summary>
    /// Envía el enlace si el correo pertenece a una cuenta activa. No dice si existe o no.
    /// </summary>
    public async Task RequestAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.Email.ToLower() == normalized, cancellationToken);
        if (user is null) return;

        var token = CreateToken(user, DateTimeOffset.UtcNow.Add(LinkLifetime));
        var frontendUrl = (configuration["App:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
        var link = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(token)}";

        if (!emailSender.IsEnabled)
        {
            // Sin correo configurado (pruebas locales): el enlace se muestra en la ventana de la API.
            if (environment.IsDevelopment())
                logger.LogWarning("HU-04 (solo desarrollo) enlace para restablecer la contraseña de {Email}: {Link}", user.Email, link);
            else
                logger.LogWarning("HU-04: no se envió el enlace de recuperación porque el correo está desactivado (Email:Enabled).");
            return;
        }

        var body =
            $"Hola {user.FullName},\n\n" +
            "Recibimos una solicitud para restablecer tu contraseña en Gestión de Citas.\n" +
            $"Usa este enlace en los próximos {LinkLifetime.TotalMinutes:0} minutos:\n\n{link}\n\n" +
            "Si no lo pediste, ignora este mensaje: tu contraseña no cambiará.";

        try
        {
            await emailSender.SendAsync(user.Email, "Restablece tu contraseña", body, cancellationToken);
        }
        catch (Exception ex)
        {
            // El usuario siempre ve el mismo mensaje; el error queda en el registro de la API.
            logger.LogError(ex, "HU-04: no se pudo enviar el correo de recuperación a {Email}.", user.Email);
        }
    }

    /// <summary>Cambia la contraseña si el token es válido. Devuelve un mensaje de error o null.</summary>
    public async Task<string?> ResetAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        const string invalidLink = "El enlace no es válido o ya venció. Solicita uno nuevo.";

        if (!TryReadToken(token, out var userId, out var expiresAt, out var stamp))
            return invalidLink;
        if (expiresAt <= DateTimeOffset.UtcNow)
            return invalidLink;

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (user is null || !CryptographicOperations.FixedTimeEquals(stamp, PasswordStamp(user.PasswordHash)))
            return invalidLink;

        user.PasswordHash = Hasher.HashPassword(user, newPassword);
        await db.SaveChangesAsync(cancellationToken);
        sessions.RevokeAll(user.Id);
        logger.LogInformation("HU-04: contraseña restablecida para el usuario {UserId}.", user.Id);
        return null;
    }

    // Formato: base64url(userId[16] + vencimiento[8] + huella[16]) "." base64url(firma)
    private string CreateToken(User user, DateTimeOffset expiresAt)
    {
        var payload = new byte[16 + 8 + StampLength];
        user.Id.TryWriteBytes(payload.AsSpan(0, 16));
        BitConverter.TryWriteBytes(payload.AsSpan(16, 8), expiresAt.ToUnixTimeSeconds());
        PasswordStamp(user.PasswordHash).CopyTo(payload.AsSpan(24));

        return $"{WebEncoders.Base64UrlEncode(payload)}.{WebEncoders.Base64UrlEncode(Sign(payload))}";
    }

    private bool TryReadToken(string token, out Guid userId, out DateTimeOffset expiresAt, out byte[] stamp)
    {
        userId = Guid.Empty;
        expiresAt = DateTimeOffset.MinValue;
        stamp = [];

        var parts = token.Split('.');
        if (parts.Length != 2) return false;

        try
        {
            var payload = WebEncoders.Base64UrlDecode(parts[0]);
            var signature = WebEncoders.Base64UrlDecode(parts[1]);
            if (payload.Length != 16 + 8 + StampLength) return false;
            if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payload))) return false;

            userId = new Guid(payload.AsSpan(0, 16));
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToInt64(payload, 16));
            stamp = payload[24..];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // La firma usa una clave derivada de Jwt:Key (no se necesita otro secreto).
    private byte[] Sign(byte[] payload)
    {
        var jwtKey = configuration["Jwt:Key"] ?? string.Empty;
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("password-reset:" + jwtKey));
        return HMACSHA256.HashData(key, payload);
    }

    private static byte[] PasswordStamp(string passwordHash) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash))[..StampLength];
}
