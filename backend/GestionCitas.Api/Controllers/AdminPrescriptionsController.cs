using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController, Route("api/admin/prescriptions"), Authorize(Roles = "Administrador")]
public class AdminPrescriptionsController(AppDbContext db, ILogger<AdminPrescriptionsController> logger) : ControllerBase
{
    private IQueryable<Prescription> Query(IQueryable<Prescription>? source = null) => (source ?? db.Prescriptions)
        .Include(x => x.Items).Include(x => x.Professional).Where(x => x.PatientId == x.Encounter.PatientId && x.ProfessionalId == x.Encounter.ProfessionalId &&
            db.Appointments.Any(a => a.Id == x.Encounter.AppointmentId && a.PatientId == x.PatientId && a.Slot.ProfessionalId == x.ProfessionalId));
    private async Task<AdminPrescriptionDto> Dto(Prescription row, CancellationToken token)
    {
        var patient = await db.Users.AsNoTracking().Where(x => x.Id == row.PatientId).Select(x => new { x.FullName, x.Dni }).SingleAsync(token);
        return new(row.Id, row.IssuedAt, patient.FullName, patient.Dni, row.Professional.FullName, row.IsActive,
            row.GeneralIndications, PrescriptionContent.Items(row), PrescriptionContent.Version(PrescriptionContent.Snapshot(row)));
    }
    private IActionResult PrivateOk(object value) { Response.Headers.CacheControl = "private, no-store"; return Ok(value); }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? patientDni, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out _)) return Unauthorized();
        if (!Regex.IsMatch(patientDni ?? "", "^[0-9]{8}$")) return BadRequest(new { message = "Ingresa el DNI del paciente de 8 dígitos." });
        var patient = await db.Users.AsNoTracking().Where(x => x.Dni == patientDni).Select(x => new { x.Id, x.FullName, x.Dni }).SingleOrDefaultAsync(token);
        if (patient is null) return PrivateOk(Array.Empty<AdminPrescriptionDto>());
        var rows = await Query().AsNoTracking().Where(x => x.PatientId == patient.Id).OrderByDescending(x => x.IssuedAt).Take(50).ToListAsync(token);
        return PrivateOk(rows.Select(x => new AdminPrescriptionDto(x.Id, x.IssuedAt, patient.FullName, patient.Dni, x.Professional.FullName, x.IsActive,
            x.GeneralIndications, PrescriptionContent.Items(x), PrescriptionContent.Version(PrescriptionContent.Snapshot(x)))));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out _)) return Unauthorized();
        var row = await Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token);
        return row is null ? NotFound() : PrivateOk(await Dto(row, token));
    }
    [HttpGet("{id:guid}/corrections")]
    public async Task<IActionResult> History(Guid id, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out _)) return Unauthorized();
        if (!await Query().AnyAsync(x => x.Id == id, token)) return NotFound();
        var rows = await db.PrescriptionCorrections.AsNoTracking().Where(x => x.PrescriptionId == id).OrderByDescending(x => x.CorrectedAt)
            .Select(x => new PrescriptionCorrectionDto(x.Id, x.CorrectedAt, x.Administrator.FullName, x.Reason)).ToListAsync(token);
        return PrivateOk(rows);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Correct(Guid id, CorrectPrescriptionRequest dto, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Content.Items.Any(x => x is null || string.IsNullOrWhiteSpace(x.Medication)) ||
            (dto.Content.Items.Count == 0 && string.IsNullOrWhiteSpace(dto.Content.GeneralIndications)))
            return BadRequest(new { message = "Registra el motivo de corrección y al menos un medicamento o indicaciones generales." });
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            IQueryable<Prescription> source = db.Prescriptions;
            if (db.Database.IsSqlServer()) source = db.Prescriptions.FromSqlInterpolated($"SELECT * FROM dbo.Prescriptions WITH (UPDLOCK,HOLDLOCK) WHERE Id = {id}");
            var row = await Query(source).SingleOrDefaultAsync(x => x.Id == id, token);
            if (row is null) return NotFound();
            if (!row.IsActive) return Conflict(new { message = "Las recetas anuladas conservan su contenido histórico y no admiten correcciones." });
            var previous = PrescriptionContent.Snapshot(row);
            var previousVersion = PrescriptionContent.Version(previous);
            if (!string.Equals(previousVersion, dto.ExpectedVersion, StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "La receta cambió. Vuelve a abrirla y revisa el contenido antes de guardar." });
            db.PrescriptionItems.RemoveRange(row.Items);
            row.Items = dto.Content.Items.Select((x, index) => new PrescriptionItem { PrescriptionId = row.Id, Medication = x.Medication.Trim(),
                Dose = Clean(x.Dose), Frequency = Clean(x.Frequency), Duration = Clean(x.Duration), Instructions = Clean(x.Instructions), SortOrder = index + 1 }).ToList();
            db.PrescriptionItems.AddRange(row.Items);
            row.GeneralIndications = Clean(dto.Content.GeneralIndications);
            db.Entry(row).Property(x => x.IsActive).IsModified = true;
            var updated = PrescriptionContent.Snapshot(row);
            db.PrescriptionCorrections.Add(new PrescriptionCorrection { PrescriptionId = row.Id, CorrectedBy = adminId,
                Reason = dto.Reason.Trim(), PreviousContent = previous, UpdatedContent = updated,
                PreviousVersion = previousVersion, UpdatedVersion = PrescriptionContent.Version(updated) });
            await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
            return PrivateOk(await Dto(row, token));
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "La receta cambió. Vuelve a abrirla antes de guardar." }); }
        catch (SqlException error) when (error.Number == 1205) { return Conflict(new { message = "Otra operación modificó la receta. Actualiza la información." }); }
        catch (DbUpdateException error)
        {
            logger.LogError(error, "No se pudo corregir la receta {PrescriptionId}.", id);
            return StatusCode(503, new { message = "No se pudo guardar la corrección. Intenta nuevamente más tarde." });
        }
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
