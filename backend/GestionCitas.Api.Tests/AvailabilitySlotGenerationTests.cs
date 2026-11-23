using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionCitas.Api.Tests;

public class AvailabilitySlotGenerationTests
{
    [Fact]
    public async Task EnsureAvailabilitySlots_UsesAdministratorsWeeklyHoursAndRespectsAbsence()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var professional = new Professional
        {
            FullName = "Agenda configurada", Specialty = new Specialty { Name = "General" },
            ScheduleConfigured = true
        };
        var monday = new ProfessionalWeeklySlot
        {
            Professional = professional, DayOfWeek = 1, StartTime = TimeSpan.FromHours(8)
        };
        var disabled = new ProfessionalWeeklySlot
        {
            Professional = professional, DayOfWeek = 1, StartTime = TimeSpan.FromHours(9), IsEnabled = false
        };
        var absenceStart = new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.FromHours(-5));
        db.AddRange(professional, monday, disabled,
            new ProfessionalAbsence { Professional = professional, StartAt = absenceStart, EndAt = absenceStart.AddDays(1) });
        await db.SaveChangesAsync();

        var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.FromHours(-5));
        Assert.Equal(2, await DbInitializer.EnsureAvailabilitySlotsAsync(db, now));
        Assert.All(await db.AvailabilitySlots.ToListAsync(), slot =>
        {
            Assert.Equal(DayOfWeek.Monday, slot.StartAt.DayOfWeek);
            Assert.Equal(8, slot.StartAt.Hour);
            Assert.True(WeeklySchedule.Allows(professional, slot.StartAt, [monday, disabled]));
        });
        Assert.Equal(0, await DbInitializer.EnsureAvailabilitySlotsAsync(db, now));
    }

    [Fact]
    public async Task EnsureAvailabilitySlots_DoesNotOfferAnOverlappingTimeForAnExistingBooking()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var professional = new Professional
        {
            FullName = "Con cita anterior", Specialty = new Specialty { Name = "General" },
            ScheduleConfigured = true
        };
        var bookedStart = new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(-5));
        db.AddRange(professional,
            new ProfessionalWeeklySlot { Professional = professional, DayOfWeek = 1, StartTime = new TimeSpan(9, 15, 0) },
            new AvailabilitySlot { Professional = professional, StartAt = bookedStart,
                EndAt = bookedStart.AddMinutes(45), IsAvailable = false, IsEnabled = false });
        await db.SaveChangesAsync();

        var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.FromHours(-5));
        Assert.Equal(2, await DbInitializer.EnsureAvailabilitySlotsAsync(db, now));
        Assert.DoesNotContain(db.AvailabilitySlots, slot => slot.StartAt == bookedStart.AddMinutes(15));
    }

    [Fact]
    public async Task EnsureAvailabilitySlots_FillsMissingDaysPerProfessionalWithoutChangingBookedSlots()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var activeSpecialty = new Specialty { Name = "General" };
        var inactiveSpecialty = new Specialty { Name = "Suspendida", IsActive = false };
        var existingProfessional = new Professional { FullName = "Con reserva", Specialty = activeSpecialty };
        var newProfessional = new Professional { FullName = "Sin cupos", Specialty = activeSpecialty };
        var inactiveProfessional = new Professional { FullName = "Inactivo", Specialty = activeSpecialty, IsActive = false };
        var inactiveSpecialtyProfessional = new Professional { FullName = "Especialidad inactiva", Specialty = inactiveSpecialty };
        var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.FromHours(-5));
        var bookedStart = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(-5));
        var booked = new AvailabilitySlot
        {
            Professional = existingProfessional,
            StartAt = bookedStart,
            EndAt = bookedStart.AddMinutes(45),
            IsAvailable = false
        };
        var absenceStart = new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.FromHours(-5));
        var absence = new ProfessionalAbsence
        {
            Professional = existingProfessional,
            StartAt = absenceStart,
            EndAt = absenceStart.AddDays(1),
            IsActive = true
        };

        db.AddRange(activeSpecialty, inactiveSpecialty, existingProfessional, newProfessional,
            inactiveProfessional, inactiveSpecialtyProfessional, booked, absence);
        await db.SaveChangesAsync();

        var created = await DbInitializer.EnsureAvailabilitySlotsAsync(db, now);
        Assert.Equal(174, created); // 18 días laborables x 5 horas; una ausencia y una reserva ya existente.
        Assert.Equal(85, await db.AvailabilitySlots.CountAsync(x => x.ProfessionalId == existingProfessional.Id));
        Assert.Equal(90, await db.AvailabilitySlots.CountAsync(x => x.ProfessionalId == newProfessional.Id));
        Assert.False(booked.IsAvailable);
        Assert.DoesNotContain(db.AvailabilitySlots, x =>
            x.ProfessionalId == existingProfessional.Id &&
            x.StartAt >= absenceStart && x.StartAt < absenceStart.AddDays(1));
        Assert.DoesNotContain(db.AvailabilitySlots, x =>
            x.ProfessionalId == inactiveProfessional.Id ||
            x.ProfessionalId == inactiveSpecialtyProfessional.Id);

        Assert.Equal(0, await DbInitializer.EnsureAvailabilitySlotsAsync(db, now));
        Assert.Equal(10, await DbInitializer.EnsureAvailabilitySlotsAsync(db, now.AddDays(1)));
    }
}
