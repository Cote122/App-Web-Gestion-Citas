using System.Security.Claims;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/admin/appointment-requests")]
[Authorize(Roles = "Administrador")]
public class AdminAppointmentRequestsController(
    AdminAppointmentChangeService service) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> ListPending()
    {
        var requests = await service.ListPendingAsync();
        return Ok(requests);
    }

    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ReviewAppointmentChangeRequest request)
    {
        var (success, error) = await service.ApproveAsync(
            CurrentUserId(),
            id,
            request.AdminComment
        );

        if (success)
            return Ok(new { message = "Solicitud aprobada correctamente." });

        if (error == "Solicitud no encontrada.")
            return NotFound(new { message = error });

        if (error == "No tienes permisos para aprobar solicitudes.")
            return Forbid();

        return Conflict(new { message = error });
    }

    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] ReviewAppointmentChangeRequest request)
    {
        var (success, error) = await service.RejectAsync(
            CurrentUserId(),
            id,
            request.AdminComment
        );

        if (success)
            return Ok(new { message = "Solicitud rechazada correctamente." });

        if (error == "Solicitud no encontrada.")
            return NotFound(new { message = error });

        if (error == "No tienes permisos para rechazar solicitudes.")
            return Forbid();

        return Conflict(new { message = error });
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Identidad de usuario inválida.");

        return userId;
    }
}