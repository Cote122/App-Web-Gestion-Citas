using System.Security.Claims;
using GestionCitas.Api.Controllers;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionCitas.Api.Tests;

public class ClinicalResultSqlSchemaTests
{
    // Esquema reportado por el usuario: DDL independiente del modelo de EF.
    private const string NativeSchema = """
        CREATE TABLE dbo.ClinicalEncounters (
            Id uniqueidentifier NOT NULL PRIMARY KEY, AppointmentId uniqueidentifier NOT NULL,
            PatientId uniqueidentifier NOT NULL, ProfessionalId uniqueidentifier NOT NULL,
            StartedAt datetimeoffset NULL, FinishedAt datetimeoffset NULL, Notes nvarchar(max) NULL,
            DiagnosisSummary nvarchar(max) NULL, Indications nvarchar(max) NULL,
            Status nvarchar(30) NOT NULL, CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NULL);
        CREATE TABLE dbo.ClinicalResults (
            Id uniqueidentifier NOT NULL PRIMARY KEY, PatientId uniqueidentifier NOT NULL,
            EncounterId uniqueidentifier NULL, AppointmentId uniqueidentifier NULL,
            ResultType nvarchar(100) NOT NULL, Title nvarchar(200) NOT NULL, Summary nvarchar(max) NULL,
            Status nvarchar(30) NOT NULL CONSTRAINT ExistingResultStatus DEFAULT(N'Pendiente'),
            IsVisibleToPatient bit NOT NULL, ResultDate datetimeoffset NULL, PublishedAt datetimeoffset NULL,
            CreatedBy uniqueidentifier NULL, CreatedAt datetimeoffset NOT NULL);
        CREATE TABLE dbo.ClinicalResultFiles (
            Id uniqueidentifier NOT NULL PRIMARY KEY, ClinicalResultId uniqueidentifier NOT NULL,
            FileName nvarchar(260) NOT NULL, ContentType nvarchar(120) NULL, FileSizeBytes bigint NULL,
            StoragePath nvarchar(1000) NOT NULL, FileHash nvarchar(128) NULL, UploadedAt datetimeoffset NOT NULL,
            FOREIGN KEY(ClinicalResultId) REFERENCES dbo.ClinicalResults(Id));
        """;
    private const string Script = "10_resultados_clinicos.sql";
    private static AppDbContext Context(string connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);

    [SqlServerFact]
    public async Task NativeSchemaAndContainedCollations_ScriptReplaysWithoutChangingDefaultsOrRecords()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(contained: true);
        await database.Execute(NativeSchema + """
            CREATE INDEX ExistingClinicalTitle ON dbo.ClinicalResults(Title);
            INSERT INTO dbo.ClinicalResults(Id,PatientId,ResultType,Title,IsVisibleToPatient,CreatedAt)
            VALUES(NEWID(),NEWID(),N'Laboratorio',N'Previo',0,SYSUTCDATETIME());
            """);
        await database.RunScript(Script); await database.RunScript(Script);
        await using var db = Context(database.ConnectionString);
        var result = await db.ClinicalResults.Include(x => x.Files).SingleAsync();
        Assert.Equal("Pendiente", result.Status); Assert.Equal("Previo", result.Title); Assert.Null(result.PublishedAt);
        Assert.Equal(13, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ClinicalResults')"));
        Assert.Equal(8, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ClinicalResultFiles')"));
        Assert.Equal(12, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ClinicalEncounters')"));
        Assert.Equal(1, await database.Scalar("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ClinicalResults') AND name='ExistingClinicalTitle'"));
        Assert.Equal("(N'Pendiente')", await database.TextScalar("SELECT definition FROM sys.default_constraints WHERE name='ExistingResultStatus'"));
        result.Files.Add(new ClinicalResultFile { FileName = "prueba.pdf", StoragePath = Guid.NewGuid().ToString("N"), FileHash = new string('A', 64) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var file = await db.ClinicalResultFiles.SingleAsync(); Assert.Null(file.ContentType); Assert.Null(file.FileSizeBytes); Assert.Equal(64, file.FileHash!.Length);
        Assert.Equal(0, database.LastScriptTransactionCount);
    }

    [SqlServerFact]
    public async Task MissingOrIncompatibleCatalog_IsRejectedWithoutPartialChanges()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create();
        var missing = await Assert.ThrowsAsync<SqlException>(() => database.RunScript(Script)); Assert.Equal(50011, missing.Number);
        await database.Execute(NativeSchema.Replace("PublishedAt datetimeoffset", "PreviousPublishedAt datetimeoffset"));
        var incompatible = await Assert.ThrowsAsync<SqlException>(() => database.RunScript(Script)); Assert.Equal(50012, incompatible.Number);
        Assert.Contains("PublishedAt", incompatible.Message);
        Assert.Equal(0, await database.Scalar("SELECT COUNT(*) FROM sys.indexes WHERE name LIKE 'IX_ClinicalResults_%'"));
        Assert.Equal(0, database.LastScriptTransactionCount);
    }

    [SqlServerFact]
    public async Task PatientQueriesAndDownload_TranslateToSqlAgainstNativeTables()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create();
        await using var db = Context(database.ConnectionString);
        await db.Database.EnsureCreatedAsync();
        await database.Execute("DROP TABLE dbo.ClinicalResultFiles; DROP TABLE dbo.ClinicalResults; DROP TABLE dbo.ClinicalEncounters;" + NativeSchema);
        var data = TestAppointments.Seed(db);
        var encounter = new ClinicalEncounter { AppointmentId = data.AppointmentId, PatientId = data.PatientId, ProfessionalId = data.ProfessionalId };
        var storage = new ClinicalResultsTests.TestStorage(); var key = Guid.NewGuid().ToString("N"); storage.Files[key] = ClinicalResultsTests.Pdf;
        var result = new ClinicalResult { PatientId = data.PatientId, AppointmentId = data.AppointmentId, EncounterId = encounter.Id, CreatedBy = data.OwnerUserId,
            ResultType = "Laboratorio", Title = "Resultado SQL", Summary = "Ficticio", Status = "Publicado", IsVisibleToPatient = true, PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Files = [new ClinicalResultFile { FileName = "sql.pdf", StoragePath = key, ContentType = "application/pdf" }] };
        db.AddRange(encounter, result); await db.SaveChangesAsync(); await database.RunScript(Script);
        var controller = new ClinicalResultsController(db, storage, Options.Create(new MedicalDocumentOptions()), NullLogger<ClinicalResultsController>.Instance) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, data.PatientId.ToString()), new Claim(ClaimTypes.Role, "Paciente")], "Test")) } }
        };
        var list = Assert.IsType<OkObjectResult>(await controller.Mine(default));
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<ClinicalResultDto>>(list.Value));
        var download = Assert.IsType<FileStreamResult>(await controller.Download(result.Id, result.Files.Single().Id, default)); await download.FileStream.DisposeAsync();
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, data.OwnerUserId.ToString()), new Claim(ClaimTypes.Role, "Profesional")], "Test"));
        var byAppointment = Assert.IsType<OkObjectResult>(await controller.ByAppointment(data.AppointmentId, default));
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<ClinicalResultDto>>(byAppointment.Value));
    }

    [SqlServerFact]
    public async Task ConcurrentPublish_PreventsStaleUploadAndEmptyContentPublication()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(); await database.Execute(NativeSchema);
        await using var first = Context(database.ConnectionString); await using var second = Context(database.ConnectionString);
        var result = new ClinicalResult { PatientId = Guid.NewGuid(), ResultType = "Laboratorio", Title = "Concurrencia", Summary = "Ficticio" };
        first.Add(result); await first.SaveChangesAsync();
        var stale = await second.ClinicalResults.SingleAsync();
        result.Status = "Publicado"; result.IsVisibleToPatient = true; result.PublishedAt = DateTimeOffset.UtcNow; await first.SaveChangesAsync();
        second.ClinicalResultFiles.Add(new ClinicalResultFile { ClinicalResultId = result.Id, FileName = "tardio.pdf", StoragePath = Guid.NewGuid().ToString("N") });
        second.Entry(stale).Property(x => x.Status).IsModified = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        Assert.Equal(0, await first.ClinicalResultFiles.CountAsync());
        second.ChangeTracker.Clear(); result.Status = "Pendiente"; result.IsVisibleToPatient = false; result.PublishedAt = null; await first.SaveChangesAsync();
        var stalePublication = await second.ClinicalResults.SingleAsync();
        result.Summary = null; await first.SaveChangesAsync();
        stalePublication.Status = "Publicado"; stalePublication.IsVisibleToPatient = true; stalePublication.PublishedAt = DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        first.ChangeTracker.Clear(); Assert.Equal("Pendiente", (await first.ClinicalResults.SingleAsync()).Status);
    }
}
