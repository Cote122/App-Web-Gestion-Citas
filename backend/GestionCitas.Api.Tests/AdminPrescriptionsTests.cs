using System.Net;
using System.Net.Http.Json;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestionCitas.Api.Tests;

public class AdminPrescriptionsTests
{
    internal static CorrectPrescriptionRequest Request(string version, string medication = "Nombre corregido", string reason = "Corrección verificada") => new() {
        ExpectedVersion = version, Reason = reason, Content = new() { GeneralIndications = "Indicaciones corregidas", Items = [new() { Medication = medication, Dose = "Dosis corregida", Frequency = "Frecuencia corregida", Duration = "Duración corregida", Instructions = "Instrucción corregida" }] }
    };

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Paciente", HttpStatusCode.Forbidden)]
    [InlineData("Profesional", HttpStatusCode.Forbidden)]
    [InlineData("Caja", HttpStatusCode.Forbidden)]
    public async Task OnlyAdministratorCanReadSearchCorrectAndSeeHistory(string? role, HttpStatusCode expected)
    {
        using var app = new PrescriptionsTests.TestApp(); var recipe = await app.Create();
        if (role is null) { app.Client.DefaultRequestHeaders.Remove("X-Test-UserId"); app.Client.DefaultRequestHeaders.Remove("X-Test-Role"); }
        else app.SignIn(app.Data.OwnerUserId, role);
        using var search = await app.Client.GetAsync("/api/admin/prescriptions?patientDni=70000001");
        using var detail = await app.Client.GetAsync($"/api/admin/prescriptions/{recipe.Id}");
        using var history = await app.Client.GetAsync($"/api/admin/prescriptions/{recipe.Id}/corrections");
        using var update = await app.Client.PutAsJsonAsync($"/api/admin/prescriptions/{recipe.Id}", Request(new string('A',64)));
        foreach (var response in new[] { search, detail, history, update }) Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task CorrectionPreservesIssuerDateAndOwner_UpdatesPatientView_AndKeepsBeforeAfterAudit()
    {
        using var app = new PrescriptionsTests.TestApp(); var recipe = await app.Create(); app.SignIn(app.AdminId, "Administrador");
        var original = (await app.Client.GetFromJsonAsync<AdminPrescriptionDto>($"/api/admin/prescriptions/{recipe.Id}"))!;
        using var update = await app.Client.PutAsJsonAsync($"/api/admin/prescriptions/{recipe.Id}", new {
            expectedVersion = original.Version, reason = "Corrección verificada", content = Request(original.Version).Content,
            patientId = Guid.NewGuid(), professionalId = Guid.NewGuid(), issuedAt = DateTimeOffset.MinValue, correctedBy = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode); var corrected = (await update.Content.ReadFromJsonAsync<AdminPrescriptionDto>())!;
        Assert.Equal(original.Professional, corrected.Professional); Assert.Equal(original.IssuedAt, corrected.IssuedAt); Assert.Equal(original.Patient, corrected.Patient); Assert.NotEqual(original.Version, corrected.Version);
        var history = await app.Client.GetFromJsonAsync<List<PrescriptionCorrectionDto>>($"/api/admin/prescriptions/{recipe.Id}/corrections");
        var entry = Assert.Single(history!); Assert.Equal("Administrador de prueba", entry.Administrator); Assert.Equal("Corrección verificada", entry.Reason);
        app.SignIn(app.Data.PatientId, "Paciente"); var patientView = await app.Client.GetFromJsonAsync<PrescriptionDto>($"/api/prescriptions/{recipe.Id}"); Assert.Equal("Nombre corregido", Assert.Single(patientView!.Items).Medication); Assert.False(patientView.CanDeactivate);
        using var scope = app.Host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.PrescriptionCorrections.SingleAsync(); Assert.Equal(app.AdminId, audit.CorrectedBy); Assert.Contains("Medicamento de prueba", audit.PreviousContent); Assert.Contains("Nombre corregido", audit.UpdatedContent); Assert.Equal(original.Version, audit.PreviousVersion); Assert.Equal(corrected.Version, audit.UpdatedVersion);
        var persisted = await db.Prescriptions.SingleAsync(); Assert.Equal(app.Data.PatientId, persisted.PatientId); Assert.Equal(app.Data.ProfessionalId, persisted.ProfessionalId); Assert.Equal(recipe.IssuedAt, persisted.IssuedAt);
    }

    [Fact]
    public async Task StaleVersionAndAnnulledPrescriptionCannotBeCorrected()
    {
        using var app = new PrescriptionsTests.TestApp(); var recipe = await app.Create(); app.SignIn(app.AdminId, "Administrador");
        var original = (await app.Client.GetFromJsonAsync<AdminPrescriptionDto>($"/api/admin/prescriptions/{recipe.Id}"))!;
        using var first = await app.Client.PutAsJsonAsync($"/api/admin/prescriptions/{recipe.Id}", Request(original.Version)); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var stale = await app.Client.PutAsJsonAsync($"/api/admin/prescriptions/{recipe.Id}", Request(original.Version, "Otra corrección")); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        app.SignIn(app.Data.OwnerUserId, "Profesional"); using var deactivate = await app.Client.DeleteAsync($"/api/prescriptions/{recipe.Id}"); Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        app.SignIn(app.AdminId, "Administrador"); var annulled = (await app.Client.GetFromJsonAsync<AdminPrescriptionDto>($"/api/admin/prescriptions/{recipe.Id}"))!;
        using var rejected = await app.Client.PutAsJsonAsync($"/api/admin/prescriptions/{recipe.Id}", Request(annulled.Version)); Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var scope = app.Host.Services.CreateScope(); Assert.Single(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PrescriptionCorrections.ToListAsync());
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("empty")]
    [InlineData("version")]
    public async Task InvalidCorrectionCreatesNoAudit(string scenario)
    {
        using var app = new PrescriptionsTests.TestApp(); var recipe = await app.Create(); app.SignIn(app.AdminId, "Administrador");
        var original = (await app.Client.GetFromJsonAsync<AdminPrescriptionDto>($"/api/admin/prescriptions/{recipe.Id}"))!;
        var request = scenario == "reason" ? Request(original.Version, reason: " ") : scenario == "version" ? Request("invalid") : new CorrectPrescriptionRequest { ExpectedVersion = original.Version, Reason = "Motivo" };
        using var response = await app.Client.PutAsJsonAsync($"/api/admin/prescriptions/{recipe.Id}", request); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = app.Host.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PrescriptionCorrections.ToListAsync());
    }

    [Fact]
    public async Task WriteFailurePreservesRecipeAndCreatesNoAudit_AndSearchRequiresPatientDni()
    {
        using var app = new PrescriptionsTests.TestApp(); var recipe = await app.Create(); app.SignIn(app.AdminId, "Administrador");
        using var missing = await app.Client.GetAsync("/api/admin/prescriptions"); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var search = await app.Client.GetFromJsonAsync<List<AdminPrescriptionDto>>("/api/admin/prescriptions?patientDni=70000001"); var original = Assert.Single(search!);
        Assert.Equal("Paciente de prueba", original.Patient);
        app.Failures.Fail = true; using var failed = await app.Client.PutAsJsonAsync($"/api/admin/prescriptions/{recipe.Id}", Request(original.Version)); Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var unchanged = await app.Client.GetFromJsonAsync<AdminPrescriptionDto>($"/api/admin/prescriptions/{recipe.Id}"); Assert.Equal(original.Version, unchanged!.Version);
        using var scope = app.Host.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PrescriptionCorrections.ToListAsync());
    }
}
