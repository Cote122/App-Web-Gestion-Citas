using System.Security.Claims;
using GestionCitas.Api.Controllers;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionCitas.Api.Tests;

public class PrescriptionCorrectionSqlTests
{
    private static AppDbContext Db(string connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
    private static T AsUser<T>(T controller, Guid id, string role) where T : ControllerBase {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)], "Test")) } }; return controller;
    }
    private static AdminPrescriptionsController Admin(AppDbContext db, Guid id) => AsUser(new AdminPrescriptionsController(db, NullLogger<AdminPrescriptionsController>.Instance), id, "Administrador");
    private static async Task<(Guid AdminId, Guid PrescriptionId)> Setup(MedicalDocumentSqlSchemaTests.TestDatabase database)
    {
        var data = await PrescriptionSqlSchemaTests.Setup(database); await database.RunScript("12_correcciones_recetas.sql");
        await using var db = Db(database.ConnectionString);
        var admin = new User { Dni = "70000004", Email = "admin@example.test", FullName = "Administrador SQL", Role = "Administrador", PasswordHash = "test" }; db.Users.Add(admin); await db.SaveChangesAsync();
        var issue = AsUser(new PrescriptionsController(db, NullLogger<PrescriptionsController>.Instance), data.OwnerUserId, "Profesional");
        var result = Assert.IsType<ObjectResult>(await issue.Issue(data.AppointmentId, new IssuePrescriptionRequest { Items = [new() { Medication = "Original" }] }, default)); Assert.Equal(201, result.StatusCode);
        return (admin.Id, Assert.IsType<PrescriptionDto>(result.Value).Id);
    }
    [SqlServerFact]
    public async Task NativeCatalogCorrectionAndHistory_PreserveOriginalIssuerAndCanReplayContainedScript()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(contained: true); var data = await Setup(database);
        await using var db = Db(database.ConnectionString); var controller = Admin(db, data.AdminId);
        var original = Assert.IsType<AdminPrescriptionDto>(Assert.IsType<OkObjectResult>(await controller.Detail(data.PrescriptionId, default)).Value);
        var corrected = Assert.IsType<AdminPrescriptionDto>(Assert.IsType<OkObjectResult>(await controller.Correct(data.PrescriptionId, AdminPrescriptionsTests.Request(original.Version), default)).Value);
        Assert.Equal(original.IssuedAt, corrected.IssuedAt); Assert.Equal(original.Professional, corrected.Professional); Assert.NotEqual(original.Version, corrected.Version);
        await database.RunScript("12_correcciones_recetas.sql"); await database.RunScript("12_correcciones_recetas.sql");
        var audit = await db.PrescriptionCorrections.SingleAsync(); Assert.Contains("Original", audit.PreviousContent); Assert.Contains("Nombre corregido", audit.UpdatedContent); Assert.Equal(data.AdminId, audit.CorrectedBy);
        Assert.Equal(9, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.PrescriptionCorrections')"));
        Assert.Equal(7, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Prescriptions')")); Assert.Equal(0, database.LastScriptTransactionCount);
    }
    [SqlServerFact]
    public async Task AuditFailureRollsBackOriginalContentAndItems()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(); var data = await Setup(database);
        await database.Execute("ALTER TABLE dbo.PrescriptionCorrections ADD CONSTRAINT TestRejectReason CHECK (Reason <> N'Fallo');");
        await using (var db = Db(database.ConnectionString)) {
            var controller = Admin(db, data.AdminId); var original = Assert.IsType<AdminPrescriptionDto>(Assert.IsType<OkObjectResult>(await controller.Detail(data.PrescriptionId, default)).Value);
            var response = Assert.IsType<ObjectResult>(await controller.Correct(data.PrescriptionId, AdminPrescriptionsTests.Request(original.Version, reason: "Fallo"), default)); Assert.Equal(503, response.StatusCode);
        }
        await using var check = Db(database.ConnectionString); Assert.Equal("Original", (await check.PrescriptionItems.SingleAsync()).Medication); Assert.Empty(await check.PrescriptionCorrections.ToListAsync());
    }
    [SqlServerFact]
    public async Task ConcurrentCorrectionsWithSameVersion_AllowOneAndRejectTheStaleWriter()
    {
        await using var database = await MedicalDocumentSqlSchemaTests.TestDatabase.Create(); var data = await Setup(database);
        string version; await using (var db = Db(database.ConnectionString)) version = Assert.IsType<AdminPrescriptionDto>(Assert.IsType<OkObjectResult>(await Admin(db, data.AdminId).Detail(data.PrescriptionId, default)).Value).Version;
        async Task<int> Correct(string name) { await using var db = Db(database.ConnectionString); return Assert.IsAssignableFrom<ObjectResult>(await Admin(db, data.AdminId).Correct(data.PrescriptionId, AdminPrescriptionsTests.Request(version, name), default)).StatusCode ?? 200; }
        var responses = await Task.WhenAll(Correct("Corrección A"), Correct("Corrección B")); Assert.Contains(200, responses); Assert.Contains(409, responses);
        await using var check = Db(database.ConnectionString); Assert.Single(await check.PrescriptionCorrections.ToListAsync()); Assert.Single(await check.PrescriptionItems.ToListAsync());
    }
}
