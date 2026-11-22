using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<ProfessionalWeeklySlot> ProfessionalWeeklySlots => Set<ProfessionalWeeklySlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentAuditLog> AppointmentAuditLogs => Set<AppointmentAuditLog>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();
    public DbSet<ClinicalResult> ClinicalResults => Set<ClinicalResult>();
    public DbSet<ClinicalResultFile> ClinicalResultFiles => Set<ClinicalResultFile>();
    public DbSet<ClinicalEncounter> ClinicalEncounters => Set<ClinicalEncounter>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<PrescriptionCorrection> PrescriptionCorrections => Set<PrescriptionCorrection>();
    public DbSet<NotificationOutbox> NotificationOutbox => Set<NotificationOutbox>();
    public DbSet<ProfessionalAbsence> ProfessionalAbsences => Set<ProfessionalAbsence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PrescriptionCorrection>(entity =>
        {
            entity.HasOne<Prescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Administrator).WithMany().HasForeignKey(x => x.CorrectedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PrescriptionId, x.CorrectedAt });
        });
        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Professional).WithMany().HasForeignKey(x => x.ProfessionalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PatientId, x.IssuedAt });
            entity.Property(x => x.IsActive).IsConcurrencyToken();
        });
        modelBuilder.Entity<PrescriptionItem>().HasIndex(x => new { x.PrescriptionId, x.SortOrder });

        modelBuilder.Entity<ClinicalResult>(entity =>
        {
            entity.HasIndex(x => new { x.PatientId, x.PublishedAt });
            entity.HasIndex(x => x.AppointmentId);
            entity.HasMany(x => x.Files).WithOne().HasForeignKey(x => x.ClinicalResultId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Status).IsConcurrencyToken();
            entity.Property(x => x.IsVisibleToPatient).IsConcurrencyToken();
            entity.Property(x => x.PublishedAt).IsConcurrencyToken();
            entity.Property(x => x.Summary).IsConcurrencyToken();
        });

        modelBuilder.Entity<MedicalDocument>(entity =>
        {
            entity.HasIndex(x => new { x.AppointmentId, x.CreatedAt });
            entity.HasIndex(x => new { x.PatientId, x.CreatedAt });
            entity.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

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

        modelBuilder.Entity<ProfessionalWeeklySlot>(entity =>
        {
            entity.HasOne(x => x.Professional).WithMany(x => x.WeeklySlots)
                .HasForeignKey(x => x.ProfessionalId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ProfessionalId, x.DayOfWeek, x.StartTime }).IsUnique();
            entity.Property(x => x.StartTime).HasColumnType("time(0)");
        });

        // HU-14: vacaciones y licencias del profesional.
        modelBuilder.Entity<ProfessionalAbsence>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.ProfessionalId, x.StartAt, x.EndAt });
        });

        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.HasOne(x => x.Patient)
                .WithMany()
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Professional)
                .WithMany()
                .HasForeignKey(x => x.ProfessionalId)
                .OnDelete(DeleteBehavior.Restrict);
        }); // <-- Llave faltante agregada aquí

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
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.AppointmentId);
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
