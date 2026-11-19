using GestionCitas.Api.Dtos;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestionCitas.Api.Controllers;

/// <summary>HU-04: recuperación de contraseña por correo.</summary>
[ApiController]
[Route("api/auth/password")]
[EnableRateLimiting("password-reset")]
public class PasswordResetController(PasswordResetService passwordReset) : ControllerBase
{
    // Siempre responde lo mismo, exista o no el correo, para no revelar qué cuentas hay.
    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await passwordReset.RequestAsync(request.Email, cancellationToken);
        return Ok(new { message = "Si el correo está registrado, te enviaremos un enlace para restablecer tu contraseña." });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var error = await passwordReset.ResetAsync(request.Token, request.Password, cancellationToken);
        return error is null
            ? Ok(new { message = "Tu contraseña se actualizó. Ya puedes iniciar sesión." })
            : BadRequest(new { message = error });
    }
}
