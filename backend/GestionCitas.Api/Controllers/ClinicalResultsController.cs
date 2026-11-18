using System.Security.Claims;
using System.Security.Cryptography;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/clinical-results")]
[Authorize(Roles = "Paciente,Profesional")]
public class ClinicalResultsController(AppDbContext db, IDocumentStorage storage,
    IOptions<MedicalDocumentOptions> options, ILogger<ClinicalResultsController> logger) : ControllerBase
{
    private IQueryable<Appointment> ProfessionalAppointments(Guid userId) =>
        db.Appointments.Where(x => x.Slot.Professional.UserId == userId);

    private IQueryable<ClinicalResult> AuthorizedResults(Guid userId)
    {
        // Rechazar vínculos inconsistentes incluso si el identificador de paciente fue alterado en SQL.
        var results = db.ClinicalResults.Where(x =>
            (x.AppointmentId == null || db.Appointments.Any(a => a.Id == x.AppointmentId && a.PatientId == x.PatientId)) &&
            (x.EncounterId == null || db.ClinicalEncounters.Any(e => e.Id == x.EncounterId && e.PatientId == x.PatientId &&
                (x.AppointmentId == null || e.AppointmentId == x.AppointmentId) &&
                db.Appointments.Any(a => a.Id == e.AppointmentId && a.PatientId == e.PatientId &&
                    a.Slot.ProfessionalId == e.ProfessionalId))));
        if (User.IsInRole("Paciente"))
        {
            var now = DateTimeOffset.UtcNow;
            return results.Where(x => x.PatientId == userId && x.Status == ClinicalResult.Published &&
                x.IsVisibleToPatient && x.PublishedAt != null && x.PublishedAt <= now);
        }
        var appointments = ProfessionalAppointments(userId);
        return results.Where(x => appointments.Any(a => a.Id == x.AppointmentId && a.PatientId == x.PatientId));
    }

    private bool CanManage(ClinicalResult result, Guid userId) => User.IsInRole("Profesional") && result.CreatedBy == userId;
    private ClinicalResultDto ToDto(ClinicalResult result, Guid userId) => new(result.Id,
        result.ResultType, result.Title, result.Summary, result.Status, result.ResultDate, result.PublishedAt,
        result.IsVisibleToPatient, CanManage(result, userId), result.Files.OrderByDescending(x => x.UploadedAt)
            .Select(x => new ClinicalResultFileDto(x.Id, x.FileName, x.ContentType ?? "application/octet-stream", x.FileSizeBytes, x.UploadedAt)).ToList());

    [HttpGet("mine")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Mine(CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var results = await AuthorizedResults(userId).AsNoTracking().Include(x => x.Files)
            .OrderByDescending(x => x.ResultDate ?? x.PublishedAt).ThenByDescending(x => x.Id).ToListAsync(token);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(results.Select(x => ToDto(x, userId)));
    }

    [HttpGet("appointments/{appointmentId:guid}")]
    [Authorize(Roles = "Profesional")]
    public async Task<IActionResult> ByAppointment(Guid appointmentId, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (!await ProfessionalAppointments(userId).AnyAsync(x => x.Id == appointmentId, token)) return NotFound();
        var results = await AuthorizedResults(userId).AsNoTracking().Include(x => x.Files)
            .Where(x => x.AppointmentId == appointmentId).OrderByDescending(x => x.CreatedAt).ToListAsync(token);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(results.Select(x => ToDto(x, userId)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var result = await AuthorizedResults(userId).AsNoTracking().Include(x => x.Files).SingleOrDefaultAsync(x => x.Id == id, token);
        if (result is null) return NotFound(new { message = "Resultado no encontrado." });
        Response.Headers.CacheControl = "private, no-store";
        return Ok(ToDto(result, userId));
    }

    [HttpPost("appointments/{appointmentId:guid}")]
    [Authorize(Roles = "Profesional")]
    public async Task<IActionResult> Create(Guid appointmentId, SaveClinicalResultDto dto, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var appointment = await ProfessionalAppointments(userId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == appointmentId, token);
        if (appointment is null) return NotFound();
        if (appointment.Status is not (AppointmentStatus.Confirmada or AppointmentStatus.Atendida))
            return BadRequest(new { message = "Solo puedes registrar resultados en citas confirmadas o atendidas." });
        var result = new ClinicalResult { PatientId = appointment.PatientId, AppointmentId = appointment.Id, CreatedBy = userId };
        Apply(result, dto);
        db.ClinicalResults.Add(result);
        return await Save(result, userId, token, created: true);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Profesional")]
    public async Task<IActionResult> Update(Guid id, SaveClinicalResultDto dto, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var result = await AuthorizedResults(userId).Include(x => x.Files).SingleOrDefaultAsync(x => x.Id == id && x.CreatedBy == userId, token);
        if (result is null) return NotFound();
        if (result.Status != ClinicalResult.Pending)
            return Conflict(new { message = "Retira la publicación antes de modificar el resultado." });
        Apply(result, dto);
        return await Save(result, userId, token);
    }

    private static void Apply(ClinicalResult result, SaveClinicalResultDto dto)
    {
        result.ResultType = dto.ResultType.Trim();
        result.Title = dto.Title.Trim();
        result.Summary = string.IsNullOrWhiteSpace(dto.Summary) ? null : dto.Summary.Trim();
        result.ResultDate = dto.ResultDate;
    }

    [HttpPatch("{id:guid}/publication")]
    [Authorize(Roles = "Profesional")]
    public async Task<IActionResult> Publish(Guid id, PublishClinicalResultDto dto, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (dto.Publish is not bool publish) return BadRequest(new { message = "Indica si deseas publicar el resultado." });
        var result = await AuthorizedResults(userId).Include(x => x.Files).SingleOrDefaultAsync(x => x.Id == id && x.CreatedBy == userId, token);
        if (result is null) return NotFound();
        if (result.Status is not (ClinicalResult.Pending or ClinicalResult.Published))
            return Conflict(new { message = "El estado actual del resultado no permite cambiar su publicación." });
        if (publish && string.IsNullOrWhiteSpace(result.Summary) && result.Files.Count == 0)
            return BadRequest(new { message = "Registra el contenido del resultado o adjunta un informe antes de publicarlo." });
        if (publish && result.Status == ClinicalResult.Published && result.IsVisibleToPatient && result.PublishedAt != null)
        {
            Response.Headers.CacheControl = "private, no-store";
            return Ok(ToDto(result, userId));
        }
        result.Status = publish ? ClinicalResult.Published : ClinicalResult.Pending;
        result.IsVisibleToPatient = publish;
        result.PublishedAt = publish ? DateTimeOffset.UtcNow : null;
        return await Save(result, userId, token);
    }

    [HttpPost("{id:guid}/files")]
    [Authorize(Roles = "Profesional")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, [FromForm] IFormFile? file, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var result = await AuthorizedResults(userId).SingleOrDefaultAsync(x => x.Id == id && x.CreatedBy == userId, token);
        if (result is null) return NotFound();
        if (result.Status != ClinicalResult.Pending) return Conflict(new { message = "Retira la publicación antes de adjuntar un informe." });
        if (file is null) return BadRequest(new { message = "Selecciona un archivo." });
        ClinicalResultFile? added = null;
        try
        {
            var validated = await DocumentFileValidator.ValidateAsync(file, options.Value.MaxFileSizeBytes, token);
            await using var hashInput = file.OpenReadStream();
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(hashInput, token));
            added = new ClinicalResultFile { ClinicalResultId = id, FileName = validated.FileName,
                ContentType = validated.ContentType, FileSizeBytes = file.Length, StoragePath = Guid.NewGuid().ToString("N"), FileHash = hash };
            await using (var input = file.OpenReadStream()) await storage.SaveAsync(added.StoragePath, input, token);
            try
            {
                db.ClinicalResultFiles.Add(added);
                // Forzar comprobación de concurrencia del padre: no adjuntar tras una publicación concurrente.
                db.Entry(result).Property(x => x.Status).IsModified = true;
                await db.SaveChangesAsync(token);
            }
            catch
            {
                try { await storage.DeleteAsync(added.StoragePath, CancellationToken.None); }
                catch (Exception error) { logger.LogError(error, "No se pudo retirar el informe huérfano {FileId}.", added.Id); }
                throw;
            }
            Response.Headers.CacheControl = "private, no-store";
            return StatusCode(201, new ClinicalResultFileDto(added.Id, added.FileName, added.ContentType!, added.FileSizeBytes, added.UploadedAt));
        }
        catch (DocumentValidationException error) { return BadRequest(new { message = error.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "El resultado cambió. Actualiza la pantalla antes de continuar." }); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            logger.LogError(error, "No se pudo guardar el informe del resultado {ResultId}.", id);
            return StatusCode(503, new { message = "No se pudo guardar el informe. Intenta nuevamente más tarde." });
        }
    }

    [HttpGet("{id:guid}/files/{fileId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid fileId, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var authorized = AuthorizedResults(userId);
        var file = await db.ClinicalResultFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.ClinicalResultId == id &&
            authorized.Any(r => r.Id == x.ClinicalResultId), token);
        if (file is null) return NotFound(new { message = "Informe no encontrado." });
        if (!Guid.TryParseExact(file.StoragePath, "N", out _)) return NotFound(new { message = "El informe requiere revisar su ubicación. Contacta al administrador." });
        try
        {
            var content = await storage.OpenReadAsync(file.StoragePath, token);
            if (content is null) return NotFound(new { message = "El archivo no está disponible. Contacta al administrador." });
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            var type = file.ContentType is "application/pdf" or "image/png" or "image/jpeg" ? file.ContentType : "application/octet-stream";
            return File(content, type, file.FileName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogError(error, "No se pudo leer el informe {FileId}.", fileId);
            return StatusCode(503, new { message = "No se pudo descargar el informe. Intenta nuevamente más tarde." });
        }
    }

    private async Task<IActionResult> Save(ClinicalResult result, Guid userId, CancellationToken token, bool created = false)
    {
        try
        {
            await db.SaveChangesAsync(token);
            Response.Headers.CacheControl = "private, no-store";
            return StatusCode(created ? 201 : 200, ToDto(result, userId));
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "El resultado cambió. Actualiza la pantalla antes de continuar." }); }
        catch (DbUpdateException error)
        {
            logger.LogError(error, "No se pudo guardar el resultado {ResultId}.", result.Id);
            return StatusCode(503, new { message = "No se pudo guardar el resultado. Intenta nuevamente más tarde." });
        }
    }
}
