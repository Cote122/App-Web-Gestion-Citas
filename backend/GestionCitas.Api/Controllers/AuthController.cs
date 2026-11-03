using GestionCitas.Api.Dtos;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, DniLookupService dniLookup) : ControllerBase
{
    // HU-37: valida el DNI y devuelve el nombre para autocompletar el registro.
    // Tiene límite de consultas por IP para no agotar el plan del servicio externo.
    [HttpGet("dni/{dni}")]
    [EnableRateLimiting("dni")]
    public async Task<IActionResult> LookupDni(string dni, CancellationToken cancellationToken)
    {
        if (dni.Length != 8 || !dni.All(char.IsAsciiDigit))
            return BadRequest(new { message = "El DNI debe tener exactamente 8 números." });

        var result = await dniLookup.LookupAsync(dni, cancellationToken);
        return result.Status switch
        {
            DniLookupStatus.Valid => Ok(new { valid = true, fullName = result.FullName }),
            DniLookupStatus.NotFound => NotFound(new { message = "No encontramos ese DNI. Verifica el número." }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "No pudimos verificar tu DNI ahora. Intenta en unos minutos." })
        };
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var response = await authService.LoginAsync(request);
        return response is null ? Unauthorized(new { message = "Credenciales no válidas." }) : Ok(response);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterPatientRequest request)
    {
        var (user, error) = await authService.RegisterPatientAsync(request);
        if (user is null)
            return Conflict(new { message = error });

        return Created(string.Empty, new
        {
            message = "Cuenta creada correctamente. Ya puedes iniciar sesión.",
            user
        });
    }
}
