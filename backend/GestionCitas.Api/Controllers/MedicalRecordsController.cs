using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MedicalRecordsController(AppDbContext context) : ControllerBase
{
    [HttpGet("appointment/{appointmentId:guid}")]
    public async Task<ActionResult<MedicalRecordDto>> GetByAppointment(Guid appointmentId)
    {
        var record = await context.MedicalRecords
            .AsNoTracking()
            .Include(x => x.Appointment) // <--- Incluimos la cita para leer su Reason
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);

        if (record is null)
        {
            var appointment = await context.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == appointmentId);

            if (appointment is null)
            {
                return NotFound(new { message = "No se encontró la cita." });
            }

            return Ok(new MedicalRecordDto(
                Guid.Empty,
                appointmentId,
                appointment.PatientId,
                Guid.Empty,
                appointment.Reason, // <--- Motivo de la cita
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
            record.Appointment?.Reason,
            record.Observations,
            record.Diagnosis,
            record.Recommendations,
            record.AttentionDate
        ));
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] CreateMedicalRecordDto dto)
    {
        var appointment = await context.Appointments
            .Include(x => x.Slot)
            .FirstOrDefaultAsync(x => x.Id == dto.AppointmentId);

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