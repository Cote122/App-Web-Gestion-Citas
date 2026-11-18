using System.Data;
using System.Security.Claims;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController, Route("api/prescriptions"), Authorize(Roles = "Paciente,Profesional")]
public class PrescriptionsController(AppDbContext db, ILogger<PrescriptionsController> logger) : ControllerBase
{
    private IQueryable<Prescription> Authorized(Guid userId)
    {
        var rows = db.Prescriptions.Where(x => x.PatientId == x.Encounter.PatientId && x.ProfessionalId == x.Encounter.ProfessionalId &&
            db.Appointments.Any(a => a.Id == x.Encounter.AppointmentId && a.PatientId == x.PatientId && a.Slot.ProfessionalId == x.ProfessionalId));
        if (User.IsInRole("Paciente"))
        {
            var now = DateTimeOffset.UtcNow;
            return rows.Where(x => x.PatientId == userId && x.IsActive && x.IssuedAt <= now);
        }
        return rows.Where(x => x.Professional.UserId == userId);
    }
    private PrescriptionDto Dto(Prescription x) => new(x.Id, x.IssuedAt, x.Professional.FullName, x.GeneralIndications,
        x.IsActive, User.IsInRole("Profesional") && x.IsActive,
        x.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => new PrescriptionItemDto(i.Medication, i.Dose, i.Frequency, i.Duration, i.Instructions)).ToList());
    private IQueryable<Prescription> WithDetail(Guid id) => Authorized(id).Include(x => x.Professional).Include(x => x.Items);
    private IActionResult PrivateOk(object value) { Response.Headers.CacheControl = "private, no-store"; return Ok(value); }

    [HttpGet("mine"), Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Mine(CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var rows = await WithDetail(userId).AsNoTracking().OrderByDescending(x => x.IssuedAt).ToListAsync(token);
        return PrivateOk(rows.Select(Dto));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var row = await WithDetail(userId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token);
        return row is null ? NotFound(new { message = "Receta no encontrada." }) : PrivateOk(Dto(row));
    }
    [HttpGet("appointments/{id:guid}"), Authorize(Roles = "Profesional")]
    public async Task<IActionResult> ByAppointment(Guid id, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (!await db.Appointments.AnyAsync(x => x.Id == id && x.Slot.Professional.UserId == userId, token)) return NotFound();
        var rows = await WithDetail(userId).AsNoTracking().Where(x => x.Encounter.AppointmentId == id).OrderByDescending(x => x.IssuedAt).ToListAsync(token);
        return PrivateOk(rows.Select(Dto));
    }
    [HttpPost("appointments/{id:guid}"), Authorize(Roles = "Profesional")]
    public async Task<IActionResult> Issue(Guid id, IssuePrescriptionRequest dto, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (dto.Items.Any(x => x is null || string.IsNullOrWhiteSpace(x.Medication)) ||
            (dto.Items.Count == 0 && string.IsNullOrWhiteSpace(dto.GeneralIndications)))
            return BadRequest(new { message = "Registra al menos un medicamento o indicaciones generales." });
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            IQueryable<Appointment> query = db.Appointments;
            if (db.Database.IsSqlServer()) query = db.Appointments.FromSqlInterpolated($"SELECT * FROM dbo.Appointments WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}");
            var appointment = await query.Include(x => x.Slot).ThenInclude(x => x.Professional)
                .SingleOrDefaultAsync(x => x.Id == id && x.Slot.Professional.UserId == userId, token);
            if (appointment is null) return NotFound();
            if (appointment.Status != AppointmentStatus.Atendida) return BadRequest(new { message = "Registra la atención antes de emitir la receta." });
            var encounters = await db.ClinicalEncounters.Where(x => x.AppointmentId == id).Take(2).ToListAsync(token);
            if (encounters.Count > 1) return Conflict(new { message = "La cita tiene varias atenciones. Revisa cuál corresponde antes de emitir la receta." });
            var encounter = encounters.SingleOrDefault();
            if (encounter is not null && (encounter.PatientId != appointment.PatientId || encounter.ProfessionalId != appointment.Slot.ProfessionalId)) return NotFound();
            if (encounter is null)
            {
                // Puente con la atención ya registrada: conservar la captura actual de HU-17.
                var record = await db.MedicalRecords.AsNoTracking().FirstOrDefaultAsync(x => x.AppointmentId == id &&
                    x.PatientId == appointment.PatientId && x.ProfessionalId == appointment.Slot.ProfessionalId, token);
                if (record is null) return BadRequest(new { message = "No se encontró una atención clínica registrada para emitir la receta." });
                encounter = new ClinicalEncounter { AppointmentId = id, PatientId = record.PatientId, ProfessionalId = record.ProfessionalId,
                    StartedAt = record.AttentionDate, FinishedAt = record.AttentionDate, Notes = record.Observations,
                    DiagnosisSummary = record.Diagnosis, Indications = record.Recommendations, Status = "Cerrada" };
                db.ClinicalEncounters.Add(encounter);
            }
            var prescription = new Prescription { Encounter = encounter, EncounterId = encounter.Id, PatientId = appointment.PatientId,
                Professional = appointment.Slot.Professional, ProfessionalId = appointment.Slot.ProfessionalId,
                GeneralIndications = Clean(dto.GeneralIndications), Items = dto.Items.Select((x, index) => new PrescriptionItem {
                    Medication = x.Medication.Trim(), Dose = Clean(x.Dose), Frequency = Clean(x.Frequency), Duration = Clean(x.Duration),
                    Instructions = Clean(x.Instructions), SortOrder = index + 1 }).ToList() };
            db.Prescriptions.Add(prescription); await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
            Response.Headers.CacheControl = "private, no-store";
            return StatusCode(201, Dto(prescription));
        }
        catch (DbUpdateException error) { logger.LogError(error, "No se pudo emitir la receta de la cita {AppointmentId}.", id); return StatusCode(503, new { message = "No se pudo emitir la receta. Intenta nuevamente más tarde." }); }
    }
    [HttpDelete("{id:guid}"), Authorize(Roles = "Profesional")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var row = await WithDetail(userId).SingleOrDefaultAsync(x => x.Id == id, token);
        if (row is null) return NotFound();
        row.IsActive = false;
        try { await db.SaveChangesAsync(token); return PrivateOk(Dto(row)); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "La receta cambió. Actualiza la información." }); }
        catch (DbUpdateException error) { logger.LogError(error, "No se pudo anular la receta {PrescriptionId}.", id); return StatusCode(503, new { message = "No se pudo anular la receta. Intenta nuevamente más tarde." }); }
    }
    private IQueryable<MedicalRecord> PatientIndications(Guid userId) => db.MedicalRecords.Where(x => x.PatientId == userId &&
        x.Appointment.PatientId == userId && x.Appointment.Slot.ProfessionalId == x.ProfessionalId && x.Appointment.Status == AppointmentStatus.Atendida &&
        x.Recommendations != null && x.Recommendations.Trim() != "");
    [HttpGet("indications/mine"), Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Indications(CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var rows = await PatientIndications(userId).AsNoTracking().OrderByDescending(x => x.AttentionDate)
            .Select(x => new PatientIndicationDto(x.Id, x.AttentionDate, x.Professional.FullName, x.Recommendations!)).ToListAsync(token);
        return PrivateOk(rows);
    }
    [HttpGet("indications/{id:guid}"), Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Indication(Guid id, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var row = await PatientIndications(userId).AsNoTracking().Where(x => x.Id == id)
            .Select(x => new PatientIndicationDto(x.Id, x.AttentionDate, x.Professional.FullName, x.Recommendations!)).SingleOrDefaultAsync(token);
        return row is null ? NotFound(new { message = "Indicación no encontrada." }) : PrivateOk(row);
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
