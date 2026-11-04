using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace GestionCitas.Api.Services;

public enum DniLookupStatus { Valid, NotFound, Unavailable }

public record DniLookupResult(DniLookupStatus Status, string? FullName = null);

/// <summary>
/// HU-37: consulta el DNI en el servicio externo (apisperu). El token se lee de la configuración
/// (DniApi:Token en User Secrets o variable de entorno) y nunca se envía al navegador.
/// </summary>
public partial class DniLookupService(HttpClient http, IConfiguration configuration, IMemoryCache cache, ILogger<DniLookupService> logger)
{
    public async Task<DniLookupResult> LookupAsync(string dni, CancellationToken cancellationToken = default)
    {
        if (!DniPattern().IsMatch(dni)) return new(DniLookupStatus.NotFound);

        var cacheKey = $"dni:{dni}";
        if (cache.TryGetValue(cacheKey, out DniLookupResult? cached) && cached is not null) return cached;

        var token = configuration["DniApi:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("DniApi:Token no está configurado; no se puede validar el DNI.");
            return new(DniLookupStatus.Unavailable);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"dni/{dni}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return Remember(cacheKey, new(DniLookupStatus.NotFound), TimeSpan.FromMinutes(10));

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("El servicio de DNI respondió {StatusCode}.", (int)response.StatusCode);
                return new(DniLookupStatus.Unavailable);
            }

            var body = await response.Content.ReadFromJsonAsync<ApisPeruDniResponse>(cancellationToken);
            if (body is null || body.Success == false || string.IsNullOrWhiteSpace(body.Nombres))
                return Remember(cacheKey, new(DniLookupStatus.NotFound), TimeSpan.FromMinutes(10));

            var fullName = ToTitleCase($"{body.Nombres} {body.ApellidoPaterno} {body.ApellidoMaterno}");
            return Remember(cacheKey, new(DniLookupStatus.Valid, fullName), TimeSpan.FromHours(12));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "No se pudo consultar el servicio de DNI.");
            return new(DniLookupStatus.Unavailable);
        }
    }

    private DniLookupResult Remember(string key, DniLookupResult result, TimeSpan duration)
    {
        cache.Set(key, result, duration);
        return result;
    }

    // "MARIA DEL PILAR LOPEZ" -> "Maria del Pilar Lopez"
    private static string ToTitleCase(string value)
    {
        var lowerWords = new HashSet<string> { "de", "del", "la", "las", "los", "y" };
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((word, index) =>
            {
                var lower = word.ToLower(CultureInfo.GetCultureInfo("es-PE"));
                return index > 0 && lowerWords.Contains(lower) ? lower : char.ToUpper(lower[0]) + lower[1..];
            });
        return string.Join(' ', words);
    }

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex DniPattern();

    private sealed record ApisPeruDniResponse(bool? Success, string? Nombres, string? ApellidoPaterno, string? ApellidoMaterno);
}
