using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionCitas.Api.Tests;

public class PatientReschedulingTests
{
    private static AppDbContext Memory(string name) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(name).ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    [Fact]
    public async Task TwoChangesPersistAcrossNewCodesAndContexts_ThirdChangesNothing()
    {
        var name = Guid.NewGuid().ToString();
        await using var db = Memory(name); var data = TestAppointments.Seed(db); var slots = await AddSlots(db, data, 3);
        var service = new AppointmentService(db);
        var first = await service.RescheduleAsync(data.PatientId, data.AppointmentId, slots[0]);
        Assert.Null(first.Error); Assert.Equal(1, first.Appointment!.Rescheduling!.Used); Assert.Equal(1, first.Appointment.Rescheduling.Remaining);
        await using var reopened = Memory(name); var nextService = new AppointmentService(reopened);
        var second = await nextService.RescheduleAsync(data.PatientId, first.Appointment.Id, slots[1]);
        Assert.Null(second.Error); Assert.Equal(2, second.Appointment!.Rescheduling!.Used); Assert.Equal(0, second.Appointment.Rescheduling.Remaining); Assert.False(second.Appointment.Rescheduling.CanReschedule);
        var third = await nextService.RescheduleAsync(data.PatientId, second.Appointment.Id, slots[2]);
        Assert.Null(third.Appointment); Assert.Contains("límite de 2", third.Error);
        Assert.Equal(3, await reopened.Appointments.CountAsync()); Assert.Equal(2, await reopened.AppointmentAuditLogs.CountAsync(x => x.EventType == "Rescheduled"));
        Assert.Equal(2, await reopened.NotificationOutbox.CountAsync());
        Assert.True((await reopened.AvailabilitySlots.SingleAsync(x => x.Id == slots[2])).IsAvailable);
        Assert.Equal(AppointmentStatus.Confirmada, (await reopened.Appointments.SingleAsync(x => x.Id == second.Appointment.Id)).Status);
        var mine = await nextService.MineAsync(data.PatientId); Assert.Equal(2, mine.Single(x => x.Id == second.Appointment.Id).Rescheduling!.Maximum);
    }

    [Fact]
    public async Task FailedAndUnauthorizedAttempts_DoNotConsumeQuotaOrTakeSlots()
    {
        await using var db = Memory(Guid.NewGuid().ToString()); var data = TestAppointments.Seed(db); var slots = await AddSlots(db, data, 1);
        var service = new AppointmentService(db);
        Assert.Null((await service.RescheduleAsync(Guid.NewGuid(), data.AppointmentId, slots[0])).Appointment);
        Assert.Null((await service.RescheduleAsync(data.PatientId, data.AppointmentId, Guid.NewGuid())).Appointment);
        Assert.Empty(await db.AppointmentAuditLogs.ToListAsync());
        Assert.Equal(0, (await service.MineAsync(data.PatientId)).Single().Rescheduling!.Used);
        Assert.True((await db.AvailabilitySlots.SingleAsync(x => x.Id == slots[0])).IsAvailable);
    }

    [Fact]
    public async Task ProfessionalChangeRequest_DoesNotConsumeOrResetPatientQuota()
    {
        await using var db = Memory(Guid.NewGuid().ToString());
        var data = TestAppointments.Seed(db);
        var slots = await AddSlots(db, data, 3);

        var patient = new AppointmentService(db);

        var first = (await patient.RescheduleAsync(
            data.PatientId,
            data.AppointmentId,
            slots[0]
        )).Appointment!;

        var professional = new ProfessionalAgendaService(db);

        var (request, error) = await professional.RescheduleAppointmentAsync(
            data.OwnerUserId,
            first.Id,
            slots[1]
        );

        Assert.Null(error);
        Assert.NotNull(request);

        Assert.Equal(first.Id, request.AppointmentId);
        Assert.Equal("Reprogramacion", request.Type);
        Assert.Equal("PendienteAprobacion", request.Status);

        var current = await db.Appointments.SingleAsync(x => x.Id == first.Id);

        Assert.Equal(AppointmentStatus.Confirmada, current.Status);
        Assert.Equal(slots[0], current.SlotId);

        Assert.True((await db.AvailabilitySlots.SingleAsync(
            x => x.Id == slots[1]
        )).IsAvailable);

        var policy = (await patient.MineAsync(data.PatientId))
            .Single(x => x.Id == first.Id)
            .Rescheduling!;

        Assert.Equal(1, policy.Used);
        Assert.Equal(1, policy.Remaining);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrokenOrCyclicHistory_FailsClosed(bool cycle)
    {
        await using var db = Memory(Guid.NewGuid().ToString()); var data = TestAppointments.Seed(db); var slots = await AddSlots(db, data, 1);
        var appointment = await db.Appointments.SingleAsync(); appointment.PreviousAppointmentId = cycle ? appointment.Id : Guid.NewGuid(); await db.SaveChangesAsync();
        var service = new AppointmentService(db);
        var policy = (await service.MineAsync(data.PatientId)).Single().Rescheduling!;
        Assert.False(policy.HistoryComplete); Assert.False(policy.CanReschedule);
        var failed = await service.RescheduleAsync(data.PatientId, appointment.Id, slots[0]); Assert.Null(failed.Appointment); Assert.Equal(PatientReschedulingPolicy.IncompleteHistoryMessage, failed.Error);
        Assert.Empty(await db.AppointmentAuditLogs.ToListAsync());
    }

    [Fact]
    public async Task LimitIsConfigurable_AndHistoricalPatientEventsAreCounted()
    {
        await using var db = Memory(Guid.NewGuid().ToString()); var data = TestAppointments.Seed(db); var slots = await AddSlots(db, data, 2);
        var service = new AppointmentService(db, Options.Create(new AppointmentPolicyOptions { MaxPatientReschedules = 1 }));
        var first = (await service.RescheduleAsync(data.PatientId, data.AppointmentId, slots[0])).Appointment!;
        Assert.Equal(1, first.Rescheduling!.Maximum); Assert.Equal(0, first.Rescheduling.Remaining);
        Assert.Null((await service.RescheduleAsync(data.PatientId, first.Id, slots[1])).Appointment);
    }

    [SqlServerFact]
    public async Task SqlServer_ConcurrentLastAttemptSucceedsOnce_AndPreservesSlotsAndQuota()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(database.ConnectionString).Options;
        await using var seed = new AppDbContext(options); await seed.Database.EnsureCreatedAsync();
        var data = TestAppointments.Seed(seed); var slots = await AddSlots(seed, data, 3);
        var first = (await new AppointmentService(seed).RescheduleAsync(data.PatientId, data.AppointmentId, slots[0])).Appointment!;
        async Task<bool> Attempt(Guid slot) { await using var db = new AppDbContext(options); return (await new AppointmentService(db).RescheduleAsync(data.PatientId, first.Id, slot)).Appointment is not null; }
        var attempts = await Task.WhenAll(Attempt(slots[1]), Attempt(slots[2])); Assert.Single(attempts.Where(x => x));
        seed.ChangeTracker.Clear();
        Assert.Equal(2, await seed.AppointmentAuditLogs.CountAsync(x => x.EventType == "Rescheduled")); Assert.Equal(3, await seed.Appointments.CountAsync());
        Assert.Equal(1, await seed.AvailabilitySlots.CountAsync(x => (x.Id == slots[1] || x.Id == slots[2]) && x.IsAvailable));
        var current = (await new AppointmentService(seed).MineAsync(data.PatientId)).Single(x => x.Status == "Confirmada");
        Assert.Equal(0, current.Rescheduling!.Remaining); Assert.False(current.Rescheduling.CanReschedule);
    }

    private static async Task<Guid[]> AddSlots(AppDbContext db, TestAppointments data, int count)
    {
        var start = (await db.AvailabilitySlots.SingleAsync()).StartAt;
        var slots = Enumerable.Range(1, count).Select(i => new AvailabilitySlot
        {
            ProfessionalId = data.ProfessionalId,
            StartAt = start.AddDays(i),
            EndAt = start.AddDays(i).AddMinutes(30),
            IsAvailable = true
        }).ToArray();
        db.AvailabilitySlots.AddRange(slots); await db.SaveChangesAsync(); return slots.Select(x => x.Id).ToArray();
    }
}
