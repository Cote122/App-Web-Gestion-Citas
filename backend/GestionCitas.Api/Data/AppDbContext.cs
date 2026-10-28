using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentAuditLog> AppointmentAuditLogs => Set<AppointmentAuditLog>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<NotificationOutbox> NotificationOutbox => Set<NotificationOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Dni).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Specialty>()
            .Property(x => x.Price)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Professional>()
            .HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<Professional>(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AvailabilitySlot>()
            .HasIndex(x => new { x.ProfessionalId, x.StartAt })
            .IsUnique();

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.AttendanceStatus).HasConversion<string>();
        });

        modelBuilder.Entity<AppointmentAuditLog>(entity =>
        {
            entity.HasIndex(x => new { x.AppointmentId, x.CreatedAt });

            entity.HasOne(x => x.Appointment)
                .WithMany()
                .HasForeignKey(x => x.AppointmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>();

            entity.HasIndex(x => new { x.SlotId, x.Status, x.CreatedAt });

            entity.HasOne(x => x.Slot)
                .WithMany()
                .HasForeignKey(x => x.SlotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedAppointment)
                .WithMany()
                .HasForeignKey(x => x.AssignedAppointmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<NotificationOutbox>(entity =>
        {
            entity.HasIndex(x => new { x.Status, x.CreatedAt });

            entity.HasIndex(x => new { x.Type, x.RelatedAppointmentId })
                .IsUnique()
                .HasFilter("[RelatedAppointmentId] IS NOT NULL");

            entity.HasOne<Appointment>()
                .WithMany()
                .HasForeignKey(x => x.RelatedAppointmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
