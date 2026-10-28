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

        var slot = await db.AvailabilitySlots
            .AsNoTracking()
            .Include(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == request.SlotId);

        if (slot is null)
            return NotFound(new { message = "El horario seleccionado no existe." });

        if (slot.StartAt <= DateTimeOffset.UtcNow)
            return BadRequest(new { message = "No puedes ingresar a la lista de espera de un horario que ya inició." });

        if (!slot.Professional.IsActive || !slot.Professional.Specialty.IsActive)
            return BadRequest(new { message = "El profesional o la especialidad ya no se encuentran disponibles." });

        if (slot.IsAvailable)
            return BadRequest(new { message = "El horario está disponible. Puedes reservarlo directamente." });

        var ownsCurrentAppointment = await db.Appointments.AnyAsync(x =>
            x.PatientId == patientId &&
            x.SlotId == slot.Id &&
            x.Status == AppointmentStatus.Confirmada);

        if (ownsCurrentAppointment)
            return Conflict(new { message = "Ese horario ya está reservado a tu nombre." });

        var duplicated = await db.WaitlistEntries.AnyAsync(x =>
            x.PatientId == patientId &&
            x.SlotId == slot.Id &&
            x.Status == WaitlistStatus.Activa);

        if (duplicated)
            return Conflict(new { message = "Ya estás registrado en la lista de espera de este horario." });

        var entry = new WaitlistEntry
        {
            PatientId = patientId,
            SlotId = slot.Id,
            SpecialtyId = slot.Professional.SpecialtyId,
            ProfessionalId = slot.ProfessionalId,
            PreferredFrom = slot.StartAt,
            PreferredTo = slot.EndAt,
            Notes = "Espera automática para el horario seleccionado."
        };

        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var position = await db.WaitlistEntries.CountAsync(x =>
            x.SlotId == slot.Id &&
            x.Status == WaitlistStatus.Activa &&
            x.CreatedAt <= entry.CreatedAt);

        return Ok(new
        {
            entry.Id,
            Status = entry.Status.ToString(),
            Position = position,
            entry.CreatedAt,
            message = "Te registraste en la lista de espera. Si se libera este horario y eres el primero de la cola, la cita se registrará automáticamente a tu nombre y se generará una notificación."
        });
    }
}
