using System.Security.Claims;
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
[Route("api/medical-documents")]
[Authorize(Roles = "Paciente,Profesional")]
public class MedicalDocumentsController(
    AppDbContext db, IDocumentStorage storage, IOptions<MedicalDocumentOptions> options,
    ILogger<MedicalDocumentsController> logger) : ControllerBase
{
    private IQueryable<Appointment> AuthorizedAppointments(Guid userId) => User.IsInRole("Paciente")
        ? db.Appointments.Where(x => x.PatientId == userId)
        : db.Appointments.Where(x => x.Slot.Professional.UserId == userId);

    private IQueryable<MedicalDocument> AuthorizedDocuments(Guid userId)
    {
        var appointments = AuthorizedAppointments(userId);
        var documents = db.MedicalDocuments.Where(x => x.AppointmentId != null &&
            appointments.Any(a => a.Id == x.AppointmentId && a.PatientId == x.PatientId));
        return User.IsInRole("Paciente") ? documents.Where(x => x.IsVisibleToPatient) : documents;
    }

    [HttpGet("limits")]
    public IActionResult Limits() => Ok(new
    {
        maxFileSizeBytes = options.Value.MaxFileSizeBytes,
        extensions = DocumentFileValidator.Extensions
    });

    [HttpGet("appointments/{appointmentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MedicalDocumentDto>>> List(Guid appointmentId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (!await AuthorizedAppointments(userId).AnyAsync(x => x.Id == appointmentId, cancellationToken))
            return NotFound(new { message = "Cita no encontrada." });

        var isProfessional = User.IsInRole("Profesional");
        var documents = await AuthorizedDocuments(userId).AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MedicalDocumentDto(x.Id, appointmentId, x.FileName,
                x.ContentType ?? "application/octet-stream", x.SizeBytes, x.CreatedAt, x.IsVisibleToPatient,
                isProfessional && x.UploadedByUserId == userId))
            .ToListAsync(cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(documents);
    }

    [HttpPost("appointments/{appointmentId:guid}")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<ActionResult<MedicalDocumentDto>> Upload(
        Guid appointmentId, [FromForm] IFormFile? file, [FromForm] bool? isVisibleToPatient, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var appointment = await AuthorizedAppointments(userId).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);
        if (appointment is null) return NotFound(new { message = "Cita no encontrada." });
        if (appointment.Status is not (AppointmentStatus.Confirmada or AppointmentStatus.Atendida))
            return BadRequest(new { message = "Solo puedes adjuntar documentos a citas confirmadas o atendidas." });
        if (file is null) return BadRequest(new { message = "Selecciona un archivo." });
        var isProfessional = User.IsInRole("Profesional");
        if (!isProfessional && isVisibleToPatient == false)
            return BadRequest(new { message = "Los documentos que adjunta el paciente deben ser visibles para él." });

        try
        {
            var validated = await DocumentFileValidator.ValidateAsync(file, options.Value.MaxFileSizeBytes, cancellationToken);
            var document = new MedicalDocument
            {
                AppointmentId = appointmentId,
                PatientId = appointment.PatientId,
                UploadedByUserId = userId,
                DocumentType = "Documento",
                Title = Path.GetFileNameWithoutExtension(validated.FileName)[..Math.Min(
                    200, Path.GetFileNameWithoutExtension(validated.FileName).Length)],
                IsVisibleToPatient = isProfessional ? isVisibleToPatient ?? true : true,
                FileName = validated.FileName,
                ContentType = validated.ContentType,
                SizeBytes = file.Length,
                StorageKey = Guid.NewGuid().ToString("N")
            };
            await using (var input = file.OpenReadStream())
                await storage.SaveAsync(document.StorageKey, input, cancellationToken);

            try
            {
                db.MedicalDocuments.Add(document);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // Si SQL falla o se cancela la solicitud, no dejar el archivo huérfano.
                try { await storage.DeleteAsync(document.StorageKey, CancellationToken.None); }
                catch (Exception cleanupError) { logger.LogError(cleanupError, "No se pudo retirar el documento huérfano {DocumentId}.", document.Id); }
                throw;
            }

            Response.Headers.CacheControl = "private, no-store";
            return StatusCode(StatusCodes.Status201Created, new MedicalDocumentDto(
                document.Id, appointmentId, document.FileName, document.ContentType ?? "application/octet-stream",
                document.SizeBytes, document.CreatedAt, document.IsVisibleToPatient, isProfessional));
        }
        catch (DocumentValidationException error) { return BadRequest(new { message = error.Message }); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            logger.LogError(error, "No se pudo guardar un documento de la cita {AppointmentId}.", appointmentId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "No se pudo guardar el documento. Intenta nuevamente más tarde." });
        }
    }

    [HttpPatch("{documentId:guid}/visibility")]
    [Authorize(Roles = "Profesional")]
    public async Task<ActionResult<MedicalDocumentDto>> UpdateVisibility(
        Guid documentId, [FromBody] UpdateMedicalDocumentVisibilityDto dto, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (dto.IsVisibleToPatient is not bool visibility)
            return BadRequest(new { message = "Indica si el documento será visible para el paciente." });
        // Solo el autor profesional de una cita que sigue teniendo asignada puede publicar u ocultar su documento.
        var document = await AuthorizedDocuments(userId)
            .FirstOrDefaultAsync(x => x.Id == documentId && x.UploadedByUserId == userId, cancellationToken);
        if (document is null) return NotFound(new { message = "Documento no encontrado o sin permiso para cambiar su visibilidad." });

        document.IsVisibleToPatient = visibility;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            Response.Headers.CacheControl = "private, no-store";
            return Ok(new MedicalDocumentDto(document.Id, document.AppointmentId!.Value, document.FileName,
                document.ContentType ?? "application/octet-stream", document.SizeBytes, document.CreatedAt,
                document.IsVisibleToPatient, true));
        }
        catch (DbUpdateException error)
        {
            logger.LogError(error, "No se pudo cambiar la visibilidad del documento {DocumentId}.", documentId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "No se pudo cambiar la visibilidad del documento. Intenta nuevamente más tarde." });
        }
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var document = await AuthorizedDocuments(userId).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return NotFound(new { message = "Documento no encontrado." });

        // No interpretar rutas físicas de registros previos como claves de este proveedor.
        if (!Guid.TryParseExact(document.StorageKey, "N", out _))
            return NotFound(new { message = "Este archivo requiere revisar su ubicación de almacenamiento. Contacta al administrador." });

        try
        {
            var content = await storage.OpenReadAsync(document.StorageKey, cancellationToken);
            if (content is null) return NotFound(new { message = "El archivo no está disponible. Contacta al administrador." });
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            var contentType = document.ContentType is "application/pdf" or "image/jpeg" or "image/png"
                ? document.ContentType : "application/octet-stream";
            return File(content, contentType, document.FileName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogError(error, "No se pudo leer el documento {DocumentId}.", documentId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "No se pudo descargar el documento. Intenta nuevamente más tarde." });
        }
    }
}
