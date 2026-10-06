using GestionCitas.Api.Dtos;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
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
