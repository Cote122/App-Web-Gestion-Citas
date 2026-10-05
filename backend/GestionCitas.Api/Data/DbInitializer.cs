using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.Users.AnyAsync())
        {
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
                user.PasswordHash = hasher.HashPassword(user, "Demo#2026");
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

        if (!await db.AvailabilitySlots.AnyAsync(x => x.StartAt >= DateTimeOffset.UtcNow.AddDays(-1)))
        {
            var professionals = await db.Professionals.Where(x => x.IsActive).ToListAsync();
            var offset = TimeSpan.FromHours(-5);
            var today = DateTime.Today;
            var hours = new[] { 9, 10, 11, 15, 16 };

            var slots = new List<AvailabilitySlot>();
            foreach (var professional in professionals)
            {
                for (var dayOffset = 1; dayOffset <= 21; dayOffset++)
                {
                    var day = today.AddDays(dayOffset);
                    if (day.DayOfWeek == DayOfWeek.Sunday) continue;

                    foreach (var hour in hours)
                    {
                        var start = new DateTimeOffset(day.Year, day.Month, day.Day, hour, 0, 0, offset);
                        slots.Add(new AvailabilitySlot
                        {
                            ProfessionalId = professional.Id,
                            StartAt = start,
                            EndAt = start.AddMinutes(45),
                            IsAvailable = true
                        });
                    }
                }
            }

            db.AvailabilitySlots.AddRange(slots);
            await db.SaveChangesAsync();
        }
    }
}
