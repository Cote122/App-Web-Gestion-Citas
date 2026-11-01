using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionCitas.Api.Tests;

public class ProfessionalAgendaServiceTests
{
    [Fact]
    public async Task ListAsync_ReturnsOnlyAppointmentsOfTheAuthenticatedProfessional()
    {
        await using var db = CreateDb();
        var data = Seed(db);
        var service = new ProfessionalAgendaService(db);

        var (items, error) = await service.ListAsync(data.RosaUserId);

        Assert.Null(error);
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Contains(item.Id, new[] { data.RosaMorningId, data.RosaControlId }));
        Assert.DoesNotContain(items, item => item.Id == data.CarlaMorningId);
        Assert.Equal("María Fernández", items[0].PatientName);
        Assert.Equal("Confirmada", items[0].Status);
        Assert.Equal("Control prenatal", items[1].Reason);
    }

    [Fact]
    public async Task ListAsync_WhenNoAppointments_ReturnsEmptyList()
    {
        await using var db = CreateDb();
        var service = new ProfessionalAgendaService(db);

        var (items, error) = await service.ListAsync(Guid.NewGuid());

        Assert.Null(error);
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task DetailAsync_ReturnsAuthorizedPatientData()
    {
        await using var db = CreateDb();
        var data = Seed(db);
        var service = new ProfessionalAgendaService(db);

        var (detail, error) = await service.DetailAsync(data.RosaUserId, data.RosaControlId);

        Assert.Null(error);
        Assert.NotNull(detail);
        Assert.Equal("70000011", detail.Patient.Dni);
        Assert.Equal("María Fernández", detail.Patient.FullName);
        Assert.Equal("paciente@demo.local", detail.Patient.Email);
        Assert.Equal("Control prenatal", detail.Reason);
        Assert.Equal("Obstetricia", detail.Specialty);
    }

    [Fact]
    public async Task DetailAsync_WhenAppointmentBelongsToAnotherProfessional_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var data = Seed(db);
        var service = new ProfessionalAgendaService(db);

        var (detail, error) = await service.DetailAsync(data.RosaUserId, data.CarlaMorningId);

        Assert.Null(detail);
        Assert.Equal("Cita no encontrada.", error);
    }

    [Fact]
    public async Task ListAsync_WhenUserIsNotLinkedToAProfessional_ReturnsError()
    {
        await using var db = CreateDb();
        Seed(db);
        var service = new ProfessionalAgendaService(db);

        var (items, error) = await service.ListAsync(Guid.NewGuid());

        Assert.Null(items);
        Assert.Equal("Tu usuario no está vinculado a un profesional.", error);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static SeedData Seed(AppDbContext db)
    {
        var today = new DateOnly(2026, 10, 30);
        var offset = ProfessionalAgendaService.ClinicOffset;

        var specialty = new Specialty { Name = "Obstetricia", Description = "Controles", Price = 25 };
        var psychology = new Specialty { Name = "Psicología", Description = "Evaluación", Price = 20 };
        var patient = new User { Dni = "70000011", Email = "paciente@demo.local", FullName = "María Fernández", Role = "Paciente", PasswordHash = "x" };
        var rosaUser = new User { Dni = "70000003", Email = "obstetricia@demo.local", FullName = "Dra. Rosa Mendoza", Role = "Profesional", PasswordHash = "x" };
        var carlaUser = new User { Dni = "70000004", Email = "psicologia@demo.local", FullName = "Lic. Carla Ramos", Role = "Profesional", PasswordHash = "x" };
        var rosa = new Professional { FullName = rosaUser.FullName, Office = "Consultorio 2", Specialty = specialty, UserId = rosaUser.Id };
        var carla = new Professional { FullName = carlaUser.FullName, Office = "Consultorio 4", Specialty = psychology, UserId = carlaUser.Id };

        DateTimeOffset At(int hour) => new(today.Year, today.Month, today.Day, hour, 0, 0, offset);

        var rosaMorning = new AvailabilitySlot { Professional = rosa, StartAt = At(9), EndAt = At(9).AddMinutes(45), IsAvailable = false };
        var rosaControl = new AvailabilitySlot { Professional = rosa, StartAt = At(10), EndAt = At(10).AddMinutes(45), IsAvailable = false };
        var carlaMorning = new AvailabilitySlot { Professional = carla, StartAt = At(9), EndAt = At(9).AddMinutes(45), IsAvailable = false };

        var rosaMorningAppt = new Appointment { Code = "CIT-1", Patient = patient, Slot = rosaMorning, Status = AppointmentStatus.Confirmada };
        var rosaControlAppt = new Appointment { Code = "CIT-2", Patient = patient, Slot = rosaControl, Status = AppointmentStatus.Confirmada, Reason = "Control prenatal" };
        var carlaAppt = new Appointment { Code = "CIT-3", Patient = patient, Slot = carlaMorning, Status = AppointmentStatus.Confirmada };

        db.AddRange(specialty, psychology, patient, rosaUser, carlaUser, rosa, carla, rosaMorning, rosaControl, carlaMorning, rosaMorningAppt, rosaControlAppt, carlaAppt);
        db.SaveChanges();

        return new SeedData(rosaUser.Id, today, rosaMorningAppt.Id, rosaControlAppt.Id, carlaAppt.Id);
    }

    private sealed record SeedData(
        Guid RosaUserId,
        DateOnly Today,
        Guid RosaMorningId,
        Guid RosaControlId,
        Guid CarlaMorningId);
}
