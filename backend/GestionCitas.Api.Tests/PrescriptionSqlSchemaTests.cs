using System.Security.Claims;
using GestionCitas.Api.Controllers;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionCitas.Api.Tests;

public class PrescriptionSqlSchemaTests
{
    private const string Native = """
        CREATE TABLE dbo.ClinicalEncounters (
          Id uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
          AppointmentId uniqueidentifier NOT NULL REFERENCES dbo.Appointments(Id),
          PatientId uniqueidentifier NOT NULL REFERENCES dbo.Users(Id),
          ProfessionalId uniqueidentifier NOT NULL REFERENCES dbo.Professionals(Id),
          StartedAt datetimeoffset NULL, FinishedAt datetimeoffset NULL, Notes nvarchar(max) NULL,
          DiagnosisSummary nvarchar(max) NULL, Indications nvarchar(max) NULL,
          Status nvarchar(30) NOT NULL DEFAULT N'Abierta', CreatedAt datetimeoffset NOT NULL DEFAULT CONVERT(datetimeoffset,SYSUTCDATETIME()), UpdatedAt datetimeoffset NULL);
        CREATE TABLE dbo.Prescriptions (
          Id uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
          EncounterId uniqueidentifier NOT NULL REFERENCES dbo.ClinicalEncounters(Id),
          PatientId uniqueidentifier NOT NULL REFERENCES dbo.Users(Id), ProfessionalId uniqueidentifier NOT NULL REFERENCES dbo.Professionals(Id),
          GeneralIndications nvarchar(max) NULL, IssuedAt datetimeoffset NOT NULL DEFAULT CONVERT(datetimeoffset,SYSUTCDATETIME()), IsActive bit NOT NULL DEFAULT 1);
        CREATE TABLE dbo.PrescriptionItems (
          Id uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY, PrescriptionId uniqueidentifier NOT NULL REFERENCES dbo.Prescriptions(Id),
          Medication nvarchar(200) NOT NULL, Dose nvarchar(100) NULL, Frequency nvarchar(100) NULL, Duration nvarchar(100) NULL, Instructions nvarchar(500) NULL, SortOrder int NOT NULL DEFAULT 1);
        """;
    private static AppDbContext Context(string connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
    private static PrescriptionsController Controller(AppDbContext db, Guid userId, string role) => new(db, NullLogger<PrescriptionsController>.Instance) {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "Test")) } }
    };
    private static IssuePrescriptionRequest Request(string medication = "Medicamento ficticio") => new() { GeneralIndications = "Contenido de prueba", Items = [new() { Medication = medication, Dose = "Texto de dosis", Frequency = "Texto de frecuencia", Duration = "Texto de duración", Instructions = "Texto de instrucciones" }] };
    private static async Task<TestAppointments> Setup(MedicalDocumentSqlSchemaTests.TestDatabase database)
    {
        await using var db = Context(database.ConnectionString); await db.Database.EnsureCreatedAsync();
        await database.Execute("DROP TABLE dbo.PrescriptionItems; DROP TABLE dbo.Prescriptions; DROP TABLE dbo.ClinicalEncounters;" + Native);
        var data = TestAppointments.Seed(db, withRecord: true); db.Appointments.Single().Status = AppointmentStatus.Atendida;
        db.MedicalRecords.Single().Recommendations = "Indicaciones de atención"; await db.SaveChangesAsync(); return data;
    }

    [SqlServerFact]
    public async Task NativeCatalogSupportsIssueAndPrivateRead_ScriptPreservesDataDefaultsAndCollations()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(contained: true); var data = await Setup(database);
        await database.RunScript("11_recetas_indicaciones.sql");
        await using var db = Context(database.ConnectionString);
        var issue = Assert.IsType<ObjectResult>(await Controller(db, data.OwnerUserId, "Profesional").Issue(data.AppointmentId, Request(), default));
        Assert.Equal(201, issue.StatusCode); var recipe = Assert.IsType<PrescriptionDto>(issue.Value);
        await database.RunScript("11_recetas_indicaciones.sql"); await database.RunScript("11_recetas_indicaciones.sql");
        var patient = Controller(db, data.PatientId, "Paciente");
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<PrescriptionDto>>(Assert.IsType<OkObjectResult>(await patient.Mine(default)).Value));
        Assert.IsType<OkObjectResult>(await patient.Detail(recipe.Id, default)); Assert.IsType<OkObjectResult>(await patient.Indications(default));
        Assert.Equal(7, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Prescriptions')"));
        Assert.Equal(8, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.PrescriptionItems')"));
        Assert.Equal(12, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ClinicalEncounters')"));
        Assert.Equal("(N'Abierta')", await database.TextScalar("SELECT d.definition FROM sys.default_constraints d JOIN sys.columns c ON d.object_id=c.default_object_id WHERE c.object_id=OBJECT_ID('dbo.ClinicalEncounters') AND c.name='Status'"));
        Assert.Equal(0, database.LastScriptTransactionCount);
        Assert.Equal("Medicamento ficticio", (await db.PrescriptionItems.SingleAsync()).Medication);
    }

    [SqlServerFact]
    public async Task FailedItemInsertRollsBackRecipeAndEncounter_WhilePreservingOriginalAttention()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(); var data = await Setup(database);
        await database.Execute("ALTER TABLE dbo.PrescriptionItems ADD CONSTRAINT TestRejectMedication CHECK (Medication <> N'Bloqueado');");
        await using var db = Context(database.ConnectionString);
        var result = Assert.IsType<ObjectResult>(await Controller(db, data.OwnerUserId, "Profesional").Issue(data.AppointmentId, Request("Bloqueado"), default));
        Assert.Equal(503, result.StatusCode); Assert.Equal(0, await db.Prescriptions.CountAsync()); Assert.Equal(0, await db.ClinicalEncounters.CountAsync()); Assert.Equal(1, await db.MedicalRecords.CountAsync());
    }

    [SqlServerFact]
    public async Task ConcurrentIssueReusesSingleEncounter_AndIncompatibleSchemaRollsBackScript()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(); var data = await Setup(database);
        async Task Issue() { await using var db = Context(database.ConnectionString); var value = Assert.IsType<ObjectResult>(await Controller(db, data.OwnerUserId, "Profesional").Issue(data.AppointmentId, Request(), default)); Assert.Equal(201, value.StatusCode); }
        await Task.WhenAll(Issue(), Issue()); await using var check = Context(database.ConnectionString);
        Assert.Equal(1, await check.ClinicalEncounters.CountAsync()); Assert.Equal(2, await check.Prescriptions.CountAsync());
        await database.Execute("EXEC sp_rename 'dbo.PrescriptionItems.Medication', 'PreviousMedication', 'COLUMN';");
        var error = await Assert.ThrowsAsync<SqlException>(() => database.RunScript("11_recetas_indicaciones.sql")); Assert.Equal(50022, error.Number); Assert.Contains("Medication", error.Message);
        Assert.Equal(0, database.LastScriptTransactionCount); Assert.Equal(0, await database.Scalar("SELECT COUNT(*) FROM sys.indexes WHERE name='IX_MedicalRecords_PatientId_AttentionDate'"));
    }
}
