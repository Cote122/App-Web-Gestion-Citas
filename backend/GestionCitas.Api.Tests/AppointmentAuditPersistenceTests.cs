using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionCitas.Api.Tests;

public class AppointmentAuditPersistenceTests
{
    [Fact]
    public async Task OneAppointment_CanPersistMultipleAuditEvents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new SqliteTestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var data = TestAppointments.Seed(db);

        db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            AppointmentId = data.AppointmentId,
            PatientId = data.PatientId,
            EventType = "AttendanceConfirmed"
        });
        await db.SaveChangesAsync();
        db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            AppointmentId = data.AppointmentId,
            PatientId = data.PatientId,
            EventType = "Cancelled"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var events = await db.AppointmentAuditLogs
            .Where(x => x.AppointmentId == data.AppointmentId)
            .Select(x => x.EventType)
            .ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Contains("AttendanceConfirmed", events);
        Assert.Contains("Cancelled", events);
    }

    private sealed class SqliteTestDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // SQLite no genera rowversion de SQL Server. Solo se adapta esa propiedad.
            modelBuilder.Entity<AvailabilitySlot>().Property(x => x.RowVersion).ValueGeneratedNever();
        }
    }
}
