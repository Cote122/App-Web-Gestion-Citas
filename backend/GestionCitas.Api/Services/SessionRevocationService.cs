using Microsoft.Extensions.Caching.Memory;

namespace GestionCitas.Api.Services;

// HU-45: al cerrar sesión se anota en memoria el momento de salida del usuario.
// Los tokens emitidos antes de ese momento se rechazan hasta que caducan.
public class SessionRevocationService(IMemoryCache cache)
{
    public const string IssuedAtClaim = "issued_ms";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(2);

    // Margen para cubrir la tolerancia de reloj (ClockSkew) de la validación del JWT.
    private static readonly TimeSpan KeepFor = TokenLifetime + TimeSpan.FromMinutes(2);

    public void RevokeAll(Guid userId) =>
        cache.Set(Key(userId), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), KeepFor);

    public bool IsRevoked(Guid userId, string? issuedAtMs)
    {
        if (!cache.TryGetValue(Key(userId), out long logoutAt))
            return false;

        return !long.TryParse(issuedAtMs, out var issuedAt) || issuedAt <= logoutAt;
    }

    private static string Key(Guid userId) => $"session-logout:{userId}";
}
