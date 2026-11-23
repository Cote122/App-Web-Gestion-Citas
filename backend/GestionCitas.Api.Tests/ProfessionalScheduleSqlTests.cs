using Xunit;

namespace GestionCitas.Api.Tests;

public class ProfessionalScheduleSqlTests
{
    [SqlServerFact]
    public async Task Migration_CanRunTwiceOnContainedDatabaseWithoutChangingExistingBookings()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(contained: true);
        await database.Execute("""
            CREATE TABLE dbo.Professionals (Id uniqueidentifier NOT NULL PRIMARY KEY);
            CREATE TABLE dbo.AvailabilitySlots (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                ProfessionalId uniqueidentifier NOT NULL,
                StartAt datetimeoffset NOT NULL,
                EndAt datetimeoffset NOT NULL,
                IsAvailable bit NOT NULL
            );
            INSERT INTO dbo.Professionals(Id) VALUES ('00000000-0000-0000-0000-000000000001');
            INSERT INTO dbo.AvailabilitySlots(Id, ProfessionalId, StartAt, EndAt, IsAvailable)
            VALUES ('00000000-0000-0000-0000-000000000002',
                '00000000-0000-0000-0000-000000000001', '2026-11-23T09:00:00-05:00',
                '2026-11-23T09:45:00-05:00', 0);
            """);

        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "SqlScripts", "14_horarios_administrador.sql"));
        // El nombre de la BD de producción se verifica en el script original;
        // esta prueba usa una base descartable con nombre aleatorio.
        script = script.Replace("IF DB_NAME() <> N'GestionCitasDb'", "IF DB_NAME() IS NULL");
        await database.Execute(script);
        await database.Execute(script);

        Assert.Equal(1, await database.Scalar("SELECT COUNT(*) FROM dbo.AvailabilitySlots WHERE IsAvailable=0 AND IsEnabled=1"));
        Assert.Equal(0, await database.Scalar("SELECT COUNT(*) FROM dbo.Professionals WHERE ScheduleConfigured=1"));
        Assert.Equal(1, await database.Scalar("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ProfessionalWeeklySlots') AND name='IX_ProfessionalWeeklySlots_ProfessionalId_DayOfWeek_StartTime'"));
        Assert.Equal(0, await database.Scalar("SELECT @@TRANCOUNT"));
    }
}
