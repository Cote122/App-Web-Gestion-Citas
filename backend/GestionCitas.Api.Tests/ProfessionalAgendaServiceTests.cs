using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        var data = Seed(db);
        db.Appointments.RemoveRange(db.Appointments.Where(x => x.Slot.Professional.UserId == data.RosaUserId));
        await db.SaveChangesAsync();
        var service = new ProfessionalAgendaService(db);

        var (items, error) = await service.ListAsync(data.RosaUserId);

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

    [Fact]
    public async Task CancelAndReschedule_WithReason_PersistsReasonInPendingRequests()
    {
        await using var db = CreateDb();
        var data = Seed(db, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));
        var service = new ProfessionalAgendaService(db);

        // Solicitud de cancelación.
        var cancelReason = "Emergencia médica del profesional";

        var (cancelRequest, cancelError) =
            await service.CancelAppointmentAsync(
                data.RosaUserId,
                data.RosaMorningId,
                cancelReason
            );

        Assert.Null(cancelError);
        Assert.NotNull(cancelRequest);
        Assert.Equal("Cancelacion", cancelRequest.Type);
        Assert.Equal("PendienteAprobacion", cancelRequest.Status);
        Assert.Equal(cancelReason, cancelRequest.Reason);

        var originalCancellation = await db.Appointments
            .SingleAsync(x => x.Id == data.RosaMorningId);

        Assert.Equal(AppointmentStatus.Confirmada, originalCancellation.Status);
        Assert.Null(originalCancellation.Reason);

        // Solicitud de reprogramación.
        var current = await db.Appointments
            .Include(x => x.Slot)
            .SingleAsync(x => x.Id == data.RosaControlId);

        var originalSlotId = current.SlotId;

        var newSlot = new AvailabilitySlot
        {
            ProfessionalId = current.Slot.ProfessionalId,
            StartAt = current.Slot.StartAt.AddDays(2),
            EndAt = current.Slot.EndAt.AddDays(2),
            IsAvailable = true
        };

        db.AvailabilitySlots.Add(newSlot);
        await db.SaveChangesAsync();

        var rescheduleReason = "Cambio solicitado por el médico tratante";

        var (rescheduleRequest, rescheduleError) =
            await service.RescheduleAppointmentAsync(
                data.RosaUserId,
                data.RosaControlId,
                newSlot.Id,
                rescheduleReason
            );

        Assert.Null(rescheduleError);
        Assert.NotNull(rescheduleRequest);
        Assert.Equal("Reprogramacion", rescheduleRequest.Type);
        Assert.Equal("PendienteAprobacion", rescheduleRequest.Status);
        Assert.Equal(rescheduleReason, rescheduleRequest.Reason);
        Assert.Equal(newSlot.Id, rescheduleRequest.TargetSlotId);

        // La cita sigue confirmada y conserva su horario y motivo originales.
        var originalAppointment = await db.Appointments
            .SingleAsync(x => x.Id == data.RosaControlId);

        Assert.Equal(AppointmentStatus.Confirmada, originalAppointment.Status);
        Assert.Equal(originalSlotId, originalAppointment.SlotId);
        Assert.Equal("Control prenatal", originalAppointment.Reason);

        // El horario solicitado todavía no está reservado.
        Assert.True((await db.AvailabilitySlots
            .SingleAsync(x => x.Id == newSlot.Id)).IsAvailable);

        Assert.Equal(2, await db.AppointmentChangeRequests.CountAsync());
        Assert.Equal(3, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task ReasonOver500Characters_RejectsCancelAndRescheduleWithoutChangingAppointment()
    {
        await using var db = CreateDb();

        var data = Seed(
            db,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))
        );

        var service = new ProfessionalAgendaService(db);
        var tooLong = new string('x', 501);

        var (cancelRequest, cancelError) =
            await service.CancelAppointmentAsync(
                data.RosaUserId,
                data.RosaMorningId,
                tooLong
            );

        var (rescheduleRequest, rescheduleError) =
            await service.RescheduleAppointmentAsync(
                data.RosaUserId,
                data.RosaControlId,
                Guid.NewGuid(),
                tooLong
            );

        Assert.Null(cancelRequest);
        Assert.Null(rescheduleRequest);

        Assert.Equal(
            "El motivo no puede superar los 500 caracteres.",
            cancelError
        );

        Assert.Equal(cancelError, rescheduleError);

        Assert.Equal(
            AppointmentStatus.Confirmada,
            (await db.Appointments.SingleAsync(
                x => x.Id == data.RosaMorningId
            )).Status
        );

        Assert.Equal(
            AppointmentStatus.Confirmada,
            (await db.Appointments.SingleAsync(
                x => x.Id == data.RosaControlId
            )).Status
        );

        Assert.Empty(await db.AppointmentChangeRequests.ToListAsync());
    }

    [Theory]
    [InlineData(false, "Cancelacion")]
    [InlineData(true, "Reprogramacion")]
    public async Task ProfessionalChanges_RegisterPendingRequestWithoutNotifyingPatient(
        bool reschedule,
        string type)
    {
        await using var db = CreateDb();

        var data = Seed(
            db,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))
        );

        var service = new ProfessionalAgendaService(db);

        var appointment = await db.Appointments
            .SingleAsync(x => x.Id == data.RosaMorningId);

        var originalSlotId = appointment.SlotId;
        Guid? targetSlotId = null;

        if (reschedule)
        {
            var current = await db.Appointments
                .Include(x => x.Slot)
                .SingleAsync(x => x.Id == data.RosaMorningId);

            var slot = new AvailabilitySlot
            {
                ProfessionalId = current.Slot.ProfessionalId,
                StartAt = current.Slot.StartAt.AddDays(1),
                EndAt = current.Slot.EndAt.AddDays(1),
                IsAvailable = true
            };

            db.AvailabilitySlots.Add(slot);
            await db.SaveChangesAsync();

            targetSlotId = slot.Id;
        }

        // Registrar la primera solicitud.
        var result = reschedule
            ? await service.RescheduleAppointmentAsync(
                data.RosaUserId,
                data.RosaMorningId,
                targetSlotId!.Value
            )
            : await service.CancelAppointmentAsync(
                data.RosaUserId,
                data.RosaMorningId
            );

        Assert.Null(result.Error);
        Assert.NotNull(result.Request);

        Assert.Equal(type, result.Request.Type);
        Assert.Equal("PendienteAprobacion", result.Request.Status);
        Assert.Equal(data.RosaMorningId, result.Request.AppointmentId);
        Assert.Equal(targetSlotId, result.Request.TargetSlotId);

        // No se permite registrar otra solicitud pendiente para la misma cita.
        var retry = reschedule
            ? await service.RescheduleAppointmentAsync(
                data.RosaUserId,
                data.RosaMorningId,
                targetSlotId!.Value
            )
            : await service.CancelAppointmentAsync(
                data.RosaUserId,
                data.RosaMorningId
            );

        Assert.Null(retry.Request);

        Assert.Equal(
            "La cita ya tiene una solicitud pendiente de aprobación.",
            retry.Error
        );

        db.ChangeTracker.Clear();

        // La solicitud debe estar persistida.
        var savedRequest = Assert.Single(
            await db.AppointmentChangeRequests.ToListAsync()
        );

        Assert.Equal(
            type,
            savedRequest.Type.ToString()
        );

        Assert.Equal(
            AppointmentChangeRequestStatus.PendienteAprobacion,
            savedRequest.Status
        );

        Assert.Equal(data.RosaMorningId, savedRequest.AppointmentId);
        Assert.Equal(targetSlotId, savedRequest.TargetSlotId);

        // No debe generarse auditoría ni notificación antes de la aprobación.
        Assert.Empty(await db.NotificationOutbox.ToListAsync());
        Assert.Empty(await db.AppointmentAuditLogs.ToListAsync());

        // La cita original permanece intacta.
        var unchangedAppointment = await db.Appointments
            .SingleAsync(x => x.Id == data.RosaMorningId);

        Assert.Equal(
            AppointmentStatus.Confirmada,
            unchangedAppointment.Status
        );

        Assert.Equal(originalSlotId, unchangedAppointment.SlotId);

        // El horario solicitado tampoco se reserva.
        if (targetSlotId.HasValue)
        {
            Assert.True(
                (await db.AvailabilitySlots.SingleAsync(
                    x => x.Id == targetSlotId.Value
                )).IsAvailable
            );
        }
    }

    [Fact]
    public async Task RescheduleRequest_RejectsSlotDisabledByAdministrator()
    {
        await using var db = CreateDb();
        var data = Seed(db, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));
        var original = await db.Appointments.Include(x => x.Slot)
            .SingleAsync(x => x.Id == data.RosaMorningId);
        var target = new AvailabilitySlot
        {
            ProfessionalId = original.Slot.ProfessionalId,
            StartAt = original.Slot.StartAt.AddDays(1),
            EndAt = original.Slot.EndAt.AddDays(1),
            IsAvailable = true,
            IsEnabled = false
        };
        db.AvailabilitySlots.Add(target);
        await db.SaveChangesAsync();

        var (request, error) = await new ProfessionalAgendaService(db)
            .RescheduleAppointmentAsync(data.RosaUserId, original.Id, target.Id);

        Assert.Null(request);
        Assert.Equal("El nuevo horario seleccionado no está disponible o no te pertenece.", error);
        Assert.Empty(await db.AppointmentChangeRequests.ToListAsync());
        Assert.Equal(AppointmentStatus.Confirmada, original.Status);
    }

    [Fact]
    public async Task AdminApproval_RejectsTargetDisabledAfterRequestWasCreated()
    {
        await using var db = CreateDb();
        var data = Seed(db, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));
        var original = await db.Appointments.Include(x => x.Slot)
            .SingleAsync(x => x.Id == data.RosaMorningId);
        var target = new AvailabilitySlot
        {
            ProfessionalId = original.Slot.ProfessionalId,
            StartAt = original.Slot.StartAt.AddDays(1),
            EndAt = original.Slot.EndAt.AddDays(1),
            IsAvailable = true
        };
        var admin = new User
        {
            Email = "administrador@demo.local",
            FullName = "Administrador",
            Role = "Administrador"
        };
        db.AddRange(target, admin);
        await db.SaveChangesAsync();

        var (request, error) = await new ProfessionalAgendaService(db)
            .RescheduleAppointmentAsync(data.RosaUserId, original.Id, target.Id);
        Assert.Null(error);
        Assert.NotNull(request);

        target.IsEnabled = false;
        await db.SaveChangesAsync();

        var (success, approvalError) = await new AdminAppointmentChangeService(db)
            .ApproveAsync(admin.Id, request.Id, null);

        Assert.False(success);
        Assert.Equal("El horario solicitado ya no está disponible.", approvalError);
        Assert.Equal(AppointmentStatus.Confirmada, original.Status);
        Assert.Equal(AppointmentChangeRequestStatus.PendienteAprobacion,
            (await db.AppointmentChangeRequests.SingleAsync(x => x.Id == request.Id)).Status);
        Assert.Empty(await db.NotificationOutbox.ToListAsync());
    }

    [Fact]
    public async Task AdminCancellation_DoesNotReassignDisabledSlotToWaitlist()
    {
        await using var db = CreateDb();
        var data = Seed(db, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));
        var original = await db.Appointments.Include(x => x.Slot)
            .SingleAsync(x => x.Id == data.RosaMorningId);
        var admin = new User
        {
            Email = "administrador@demo.local",
            FullName = "Administrador",
            Role = "Administrador"
        };
        var waiting = new User
        {
            Email = "espera@demo.local",
            FullName = "Paciente en espera"
        };
        var entry = new WaitlistEntry
        {
            Patient = waiting,
            Slot = original.Slot,
            SpecialtyId = (await db.Professionals.SingleAsync(x => x.Id == original.Slot.ProfessionalId)).SpecialtyId
        };
        db.AddRange(admin, waiting, entry);
        await db.SaveChangesAsync();

        var (request, error) = await new ProfessionalAgendaService(db)
            .CancelAppointmentAsync(data.RosaUserId, original.Id);
        Assert.Null(error);
        Assert.NotNull(request);

        original.Slot.IsEnabled = false;
        await db.SaveChangesAsync();

        var (success, approvalError) = await new AdminAppointmentChangeService(db)
            .ApproveAsync(admin.Id, request.Id, null);

        Assert.True(success);
        Assert.Null(approvalError);
        Assert.Equal(AppointmentStatus.Cancelada, original.Status);
        Assert.Equal(WaitlistStatus.Activa, entry.Status);
        Assert.Null(entry.AssignedAppointmentId);
        Assert.DoesNotContain(await db.Appointments.ToListAsync(), x => x.PatientId == waiting.Id);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static SeedData Seed(AppDbContext db, DateOnly? day = null)
    {
        var today = day ?? new DateOnly(2026, 10, 30);
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
