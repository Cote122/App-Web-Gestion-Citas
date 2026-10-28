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
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var patientId = CurrentUserId();

        var entries = await db.WaitlistEntries
            .AsNoTracking()
            .Where(x => x.PatientId == patientId && x.Status == WaitlistStatus.Activa)
            .Include(x => x.Slot)
                .ThenInclude(x => x.Professional)
                .ThenInclude(x => x.Specialty)
            .OrderBy(x => x.Slot.StartAt)
            .ToListAsync();

        var result = new List<WaitlistItemDto>(entries.Count);

        foreach (var entry in entries)
        {
            var position = await GetPositionAsync(entry.SlotId, entry.Id);

            result.Add(new WaitlistItemDto(
                entry.Id,
                entry.SlotId,
                entry.Slot.Professional.Specialty.Name,
                entry.Slot.Professional.FullName,
                entry.Slot.Professional.Office,
                entry.Slot.StartAt,
                entry.Slot.EndAt,
                entry.Status.ToString(),
                position,
                entry.CreatedAt));
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWaitlistRequest request)
    {
        var patientId = CurrentUserId();

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

        var position = await GetPositionAsync(entry.SlotId, entry.Id);

        return Ok(new
        {
            entry.Id,
            Status = entry.Status.ToString(),
            Position = position,
            entry.CreatedAt,
            message = "Te registraste en la lista de espera. Si se libera este horario y eres el primero de la cola, la cita se registrará automáticamente a tu nombre y se generará una notificación."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var patientId = CurrentUserId();

        var entry = await db.WaitlistEntries
            .FirstOrDefaultAsync(x => x.Id == id && x.PatientId == patientId);

        if (entry is null)
            return NotFound(new { message = "No se encontró el registro de lista de espera." });

        if (entry.Status != WaitlistStatus.Activa)
            return BadRequest(new { message = "Este registro ya no se encuentra activo en la lista de espera." });

        entry.Status = WaitlistStatus.Cerrada;
        await db.SaveChangesAsync();

        return Ok(new { message = "Saliste correctamente de la lista de espera." });
    }

    private async Task<int> GetPositionAsync(Guid slotId, Guid entryId)
    {
        var orderedIds = await db.WaitlistEntries
            .AsNoTracking()
            .Where(x => x.SlotId == slotId && x.Status == WaitlistStatus.Activa)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync();

        var index = orderedIds.FindIndex(id => id == entryId);
        return index >= 0 ? index + 1 : 0;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
