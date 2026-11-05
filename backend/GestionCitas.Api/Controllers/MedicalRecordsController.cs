using System.Security.Claims;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Profesional")]
public class MedicalRecordsController(AppDbContext context) : ControllerBase
{
    [HttpGet("appointment/{appointmentId:guid}")]
    public async Task<ActionResult<MedicalRecordDto>> GetByAppointment(Guid appointmentId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        // Verificar la propiedad antes de consultar cualquier dato clínico.
        var appointment = await context.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId && x.Slot.Professional.UserId == userId)
            .Select(x => new { x.PatientId, x.Reason, x.Slot.ProfessionalId })
            .FirstOrDefaultAsync();

        if (appointment is null)
            return NotFound(new { message = "Cita no encontrada." });

        var record = await context.MedicalRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);

        if (record is null)
        {
            return Ok(new MedicalRecordDto(
                Guid.Empty,
                appointmentId,
                appointment.PatientId,
                appointment.ProfessionalId,
                appointment.Reason,
                null,
                null,
                null,
                DateTimeOffset.UtcNow
            ));
        }

        return Ok(new MedicalRecordDto(
            record.Id,
            record.AppointmentId,
            record.PatientId,
            record.ProfessionalId,
            appointment.Reason,
            record.Observations,
            record.Diagnosis,
            record.Recommendations,
            record.AttentionDate
        ));
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] CreateMedicalRecordDto dto)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var appointment = await context.Appointments
            .Include(x => x.Slot)
            .FirstOrDefaultAsync(x => x.Id == dto.AppointmentId && x.Slot.Professional.UserId == userId);

        if (appointment is null)
        {
            return NotFound(new { message = "Cita no encontrada." });
        }

        // Guardamos/Actualizamos el motivo de consulta directamente en la Cita
        appointment.Reason = dto.Reason;

        // Buscamos si ya existe el registro médico de esta cita
        var record = await context.MedicalRecords
            .FirstOrDefaultAsync(x => x.AppointmentId == dto.AppointmentId);

        if (record is null)
        {
            record = new MedicalRecord
            {
                Id = Guid.NewGuid(),
                AppointmentId = dto.AppointmentId,
                PatientId = appointment.PatientId,
                ProfessionalId = appointment.Slot.ProfessionalId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            context.MedicalRecords.Add(record);
        }
        else
        {
            record.PatientId = appointment.PatientId;
            record.ProfessionalId = appointment.Slot.ProfessionalId;
        }

        // Asignamos los campos clínicos al MedicalRecord (sin Reason, porque ya vive en Appointment)
        record.Observations = dto.Observations;
        record.Diagnosis = dto.Diagnosis;
        record.Recommendations = dto.Recommendations;
        record.AttentionDate = dto.AttentionDate;
        record.UpdatedAt = DateTimeOffset.UtcNow;

        // Marcamos la cita como atendida
        appointment.Status = AppointmentStatus.Atendida;

        await context.SaveChangesAsync();

        return Ok(new { message = "Atención registrada con éxito." });
    }
}
