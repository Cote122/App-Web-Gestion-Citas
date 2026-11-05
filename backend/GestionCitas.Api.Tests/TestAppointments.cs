using GestionCitas.Api.Data;
using GestionCitas.Api.Models;

namespace GestionCitas.Api.Tests;

internal sealed record TestAppointments(
    Guid OwnerUserId,
    Guid OtherUserId,
    Guid PatientId,
    Guid ProfessionalId,
    Guid AppointmentId)
{
    public static TestAppointments Seed(AppDbContext db, bool withRecord = false)
    {
        var patient = new User { Dni = "70000001", Email = "patient@example.test", FullName = "Paciente de prueba", Role = "Paciente", PasswordHash = "test" };
        var owner = new User { Dni = "70000002", Email = "owner@example.test", FullName = "Profesional de prueba", Role = "Profesional", PasswordHash = "test" };
        var other = new User { Dni = "70000003", Email = "other@example.test", FullName = "Otro profesional", Role = "Profesional", PasswordHash = "test" };
        var specialty = new Specialty { Name = "Especialidad de prueba", Price = 25 };
        var professional = new Professional { FullName = owner.FullName, User = owner, Specialty = specialty, Office = "Consultorio 1" };
        var otherProfessional = new Professional { FullName = other.FullName, User = other, Specialty = specialty, Office = "Consultorio 2" };
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var slot = new AvailabilitySlot { Professional = professional, StartAt = start, EndAt = start.AddMinutes(30), IsAvailable = false };
        var appointment = new Appointment { Code = "CIT-TEST", Patient = patient, Slot = slot, Reason = "Motivo original" };
        db.AddRange(patient, owner, other, specialty, professional, otherProfessional, slot, appointment);
        if (withRecord)
        {
            db.MedicalRecords.Add(new MedicalRecord
            {
                Appointment = appointment,
                Patient = patient,
                Professional = professional,
                Diagnosis = "Diagnóstico previo"
            });
        }
        db.SaveChanges();
        return new(owner.Id, other.Id, patient.Id, professional.Id, appointment.Id);
    }
}
