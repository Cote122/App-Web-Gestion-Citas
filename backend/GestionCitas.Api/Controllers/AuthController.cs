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
        var (response, error) = await authService.RegisterPatientAsync(request);
        return response is null ? BadRequest(new { message = error }) : Ok(response);
    }
}
