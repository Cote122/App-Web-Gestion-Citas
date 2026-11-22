using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.Users.AnyAsync())
        {
            var demoPassword = Environment.GetEnvironmentVariable("GESTIONCITAS_DEMO_PASSWORD");
            if (string.IsNullOrWhiteSpace(demoPassword))
                throw new InvalidOperationException(
                    "Para crear usuarios de demostración en una base vacía, configura GESTIONCITAS_DEMO_PASSWORD.");

            var hasher = new PasswordHasher<User>();
            var users = new[]
            {
                new User { Dni = "70000011", Email = "paciente@demo.local", FullName = "María Fernández", Role = "Paciente" },
                new User { Dni = "70000001", Email = "admin@demo.local", FullName = "Coordinador Demo", Role = "Administrador" },
                new User { Dni = "70000002", Email = "caja@demo.local", FullName = "Carlos Salinas", Role = "Caja" },
                new User { Dni = "70000003", Email = "obstetricia@demo.local", FullName = "Dra. Rosa Mendoza", Role = "Profesional" },
                new User { Dni = "70000004", Email = "psicologia@demo.local", FullName = "Lic. Carla Ramos", Role = "Profesional" }
            };

            foreach (var user in users)
            {
                user.PasswordHash = hasher.HashPassword(user, demoPassword);
            }

            db.Users.AddRange(users);
            await db.SaveChangesAsync();
        }

        if (!await db.Specialties.AnyAsync())
        {
            db.Specialties.AddRange(
                new Specialty { Name = "Obstetricia", Description = "Controles y atención obstétrica", Price = 25 },
                new Specialty { Name = "Psicología", Description = "Evaluación y atención psicológica", Price = 20 },
                new Specialty { Name = "Medicina General", Description = "Consulta médica general", Price = 20 },
                new Specialty { Name = "Ecografía", Description = "Estudios ecográficos", Price = 60 },
                new Specialty { Name = "Laboratorio", Description = "Análisis clínicos", Price = 35 },
                new Specialty { Name = "Rehabilitación", Description = "Medicina física y rehabilitación", Price = 30 }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Professionals.AnyAsync())
        {
            var specialties = await db.Specialties.ToDictionaryAsync(x => x.Name);
            var users = await db.Users.ToDictionaryAsync(x => x.Dni);

            db.Professionals.AddRange(
                new Professional { FullName = "Dra. Rosa Mendoza", Office = "Consultorio 2", SpecialtyId = specialties["Obstetricia"].Id, UserId = users["70000003"].Id },
                new Professional { FullName = "Dra. Carmen Salazar", Office = "Consultorio 3", SpecialtyId = specialties["Obstetricia"].Id },
                new Professional { FullName = "Lic. Carla Ramos", Office = "Consultorio 4", SpecialtyId = specialties["Psicología"].Id, UserId = users["70000004"].Id },
                new Professional { FullName = "Lic. Elena Vega", Office = "Consultorio 5", SpecialtyId = specialties["Psicología"].Id },
                new Professional { FullName = "Dra. Ana López", Office = "Consultorio 1", SpecialtyId = specialties["Medicina General"].Id },
                new Professional { FullName = "Dr. Marco Ruiz", Office = "Consultorio 6", SpecialtyId = specialties["Medicina General"].Id },
                new Professional { FullName = "Dr. Luis Salazar", Office = "Sala de Ecografía", SpecialtyId = specialties["Ecografía"].Id },
                new Professional { FullName = "Tec. José Campos", Office = "Laboratorio", SpecialtyId = specialties["Laboratorio"].Id },
                new Professional { FullName = "Lic. Elena Vargas", Office = "Sala de Rehabilitación", SpecialtyId = specialties["Rehabilitación"].Id }
            );
            await db.SaveChangesAsync();
        }

        await EnsureAvailabilitySlotsAsync(db);
    }

    // Mantiene una ventana de 21 días por profesional, sin modificar cupos existentes u ocupados.
    public static async Task<int> EnsureAvailabilitySlotsAsync(
        AppDbContext db,
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default)
    {
        var offset = TimeSpan.FromHours(-5);
        var today = (now ?? DateTimeOffset.UtcNow).ToOffset(offset).Date;
        var firstDay = today.AddDays(1);
        var lastDay = today.AddDays(22);
        var first = new DateTimeOffset(firstDay, offset);
        var endExclusive = new DateTimeOffset(lastDay, offset);

        var professionals = await db.Professionals
            .AsNoTracking()
            .Where(x => x.IsActive && x.Specialty.IsActive)
            .Select(x => new { x.Id, x.ScheduleConfigured })
            .ToListAsync(cancellationToken);

        var weeklySlots = await db.ProfessionalWeeklySlots.AsNoTracking()
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);

        var existing = await db.AvailabilitySlots
            .AsNoTracking()
            .Where(x => x.StartAt >= first && x.StartAt < endExclusive)
            .Select(x => new { x.ProfessionalId, x.StartAt, x.EndAt, x.IsAvailable, x.IsEnabled })
            .ToListAsync(cancellationToken);
        var existingKeys = existing.Select(x => (x.ProfessionalId, x.StartAt)).ToHashSet();

        var absences = await db.ProfessionalAbsences
            .AsNoTracking()
            .Where(x => x.IsActive && x.StartAt < endExclusive && x.EndAt > first)
            .Select(x => new { x.ProfessionalId, x.StartAt, x.EndAt })
            .ToListAsync(cancellationToken);

        var slots = new List<AvailabilitySlot>();
        foreach (var professional in professionals)
        {
            for (var dayOffset = 1; dayOffset <= 21; dayOffset++)
            {
                var day = today.AddDays(dayOffset);
                IEnumerable<TimeSpan> hours = professional.ScheduleConfigured
                    ? weeklySlots.Where(x => x.ProfessionalId == professional.Id && x.DayOfWeek == (int)day.DayOfWeek)
                        .Select(x => x.StartTime)
                    : day.DayOfWeek == DayOfWeek.Sunday ? [] : WeeklySchedule.LegacyHours;

                foreach (var hour in hours)
                {
                    var start = new DateTimeOffset(day.Add(hour), offset);
                    var end = start.Add(WeeklySchedule.Duration);
                    if (existingKeys.Contains((professional.Id, start))) continue;
                    // Una cita anterior conserva su hora aunque el administrador cambie
                    // la plantilla; nunca ofrecer un cupo nuevo que se cruce con ella.
                    if (existing.Any(x => x.ProfessionalId == professional.Id &&
                                          (!x.IsAvailable || x.IsEnabled) &&
                                          x.StartAt < end && x.EndAt > start)) continue;
                    if (absences.Any(a => a.ProfessionalId == professional.Id &&
                                          a.StartAt < end && a.EndAt > start)) continue;

                    existingKeys.Add((professional.Id, start));

                    slots.Add(new AvailabilitySlot
                    {
                        ProfessionalId = professional.Id,
                        StartAt = start,
                        EndAt = end,
                        IsAvailable = true
                    });
                }
            }
        }

        if (slots.Count == 0) return 0;
        db.AvailabilitySlots.AddRange(slots);
        await db.SaveChangesAsync(cancellationToken);
        return slots.Count;
    }
}
