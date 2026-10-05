using System.Security.Claims;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/waitlist")]
[Authorize(Roles = "Paciente")]
public class WaitlistController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateWaitlistRequest request)
    {
        var patientId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var duplicated = await db.WaitlistEntries.AnyAsync(x =>
            x.PatientId == patientId &&
            x.SpecialtyId == request.SpecialtyId &&
            x.ProfessionalId == request.ProfessionalId &&
            x.Status == WaitlistStatus.Activa);

        if (duplicated)
            return Conflict(new { message = "Ya existe una solicitud activa equivalente en la lista de espera." });

        var entry = new WaitlistEntry
        {
            PatientId = patientId,
            SpecialtyId = request.SpecialtyId,
            ProfessionalId = request.ProfessionalId,
            PreferredFrom = request.PreferredFrom,
            PreferredTo = request.PreferredTo,
            Notes = request.Notes?.Trim() ?? string.Empty
        };

        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();
        return Ok(new { entry.Id, Status = entry.Status.ToString(), entry.CreatedAt });
    }
}
