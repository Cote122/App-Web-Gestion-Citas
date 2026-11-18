using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GestionCitas.Api.Controllers;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionCitas.Api.Tests;

public class PrescriptionsTests
{
    [Fact]
    public async Task IssueCreatesEncounterFromExistingAttention_AndReusesIt_WithServerOwnedIdsAndDate()
    {
        using var app = new TestApp(); app.SignIn(app.Data.OwnerUserId, "Profesional");
        using var first = await app.Client.PostAsJsonAsync(app.AppointmentUrl, new { patientId = Guid.NewGuid(), professionalId = Guid.NewGuid(), encounterId = Guid.NewGuid(), issuedAt = DateTimeOffset.MinValue,
            generalIndications = "Indicaciones de prueba", items = new[] { new { medication = " Medicamento de prueba ", dose = "Dosis registrada", frequency = "Frecuencia registrada", duration = "Duración registrada", instructions = "Instrucción registrada" } } });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); var value = (await first.Content.ReadFromJsonAsync<PrescriptionDto>())!;
        Assert.Equal("Medicamento de prueba", Assert.Single(value.Items).Medication); Assert.Equal("Profesional de prueba", value.Professional); Assert.True(value.IssuedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        using var second = await app.Client.PostAsJsonAsync(app.AppointmentUrl, new { generalIndications = "Solo indicaciones", items = Array.Empty<object>() }); Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var scope = app.Host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var encounters = await db.ClinicalEncounters.ToListAsync(); var encounter = Assert.Single(encounters);
        Assert.Equal("Cerrada", encounter.Status); Assert.Equal(app.Data.PatientId, encounter.PatientId); Assert.Equal(app.Data.ProfessionalId, encounter.ProfessionalId); Assert.Equal(app.Data.AppointmentId, encounter.AppointmentId);
        Assert.Equal(2, await db.Prescriptions.CountAsync()); Assert.All(await db.Prescriptions.ToListAsync(), x => { Assert.Equal(encounter.Id, x.EncounterId); Assert.Equal(app.Data.PatientId, x.PatientId); Assert.True(x.IsActive); });
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Caja", HttpStatusCode.Forbidden)]
    [InlineData("Administrador", HttpStatusCode.Forbidden)]
    public async Task AnonymousAndUnrelatedRoles_CannotConsultOrIssue(string? role, HttpStatusCode expected)
    {
        using var app = new TestApp(); if (role is not null) app.SignIn(app.Data.PatientId, role);
        using var mine = await app.Client.GetAsync("/api/prescriptions/mine"); using var issue = await app.Issue(); using var indications = await app.Client.GetAsync("/api/prescriptions/indications/mine");
        Assert.Equal(expected, mine.StatusCode); Assert.Equal(expected, issue.StatusCode); Assert.Equal(expected, indications.StatusCode);
    }

    [Theory]
    [InlineData("other-patient")]
    [InlineData("inactive")]
    [InlineData("future")]
    [InlineData("wrong-encounter-patient")]
    [InlineData("wrong-encounter-professional")]
    public async Task PatientCannotReadForeignInactiveFutureOrInconsistentPrescription(string scenario)
    {
        using var app = new TestApp(); var value = await app.Create();
        await app.Mutate(db => { var row = db.Prescriptions.Single(); if (scenario == "other-patient") row.PatientId = Guid.NewGuid(); if (scenario == "inactive") row.IsActive = false;
            if (scenario == "future") row.IssuedAt = DateTimeOffset.UtcNow.AddDays(1); if (scenario == "wrong-encounter-patient") db.ClinicalEncounters.Single().PatientId = Guid.NewGuid();
            if (scenario == "wrong-encounter-professional") db.ClinicalEncounters.Single().ProfessionalId = Guid.NewGuid(); });
        app.SignIn(app.Data.PatientId, "Paciente");
        Assert.Empty((await app.Client.GetFromJsonAsync<List<PrescriptionDto>>("/api/prescriptions/mine"))!);
        using var detail = await app.Client.GetAsync($"/api/prescriptions/{value.Id}"); Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task PatientDetailIsReadOnly_AndContainsNoPrivateClinicalNotes()
    {
        using var app = new TestApp(); var value = await app.Create(); app.SignIn(app.Data.PatientId, "Paciente");
        using var detail = await app.Client.GetAsync($"/api/prescriptions/{value.Id}"); Assert.Equal(HttpStatusCode.OK, detail.StatusCode); Assert.Contains("no-store", detail.Headers.CacheControl!.ToString());
        var json = await detail.Content.ReadAsStringAsync(); Assert.DoesNotContain("Nota privada", json); Assert.DoesNotContain("Diagnóstico previo", json); Assert.DoesNotContain("patientId", json, StringComparison.OrdinalIgnoreCase);
        var dto = (await detail.Content.ReadFromJsonAsync<PrescriptionDto>())!; Assert.False(dto.CanDeactivate); Assert.NotEmpty(dto.Items);
        using var issue = await app.Issue(); using var delete = await app.Client.DeleteAsync($"/api/prescriptions/{value.Id}"); using var edit = await app.Client.PutAsJsonAsync($"/api/prescriptions/{value.Id}", new { generalIndications = "Manipulado" });
        Assert.Equal(HttpStatusCode.Forbidden, issue.StatusCode); Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode); Assert.Equal(HttpStatusCode.MethodNotAllowed, edit.StatusCode);
    }

    [Fact]
    public async Task ForeignProfessionalCannotReadIssueOrDeactivate_AndOwnerCanWithdrawWithoutDeletingHistory()
    {
        using var app = new TestApp(); var value = await app.Create(); app.SignIn(app.Data.OtherUserId, "Profesional");
        using var detail = await app.Client.GetAsync($"/api/prescriptions/{value.Id}"); using var issue = await app.Issue(); using var delete = await app.Client.DeleteAsync($"/api/prescriptions/{value.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode); Assert.Equal(HttpStatusCode.NotFound, issue.StatusCode); Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        app.SignIn(app.Data.OwnerUserId, "Profesional"); using var deactivate = await app.Client.DeleteAsync($"/api/prescriptions/{value.Id}"); Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        app.SignIn(app.Data.PatientId, "Paciente"); using var hidden = await app.Client.GetAsync($"/api/prescriptions/{value.Id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var scope = app.Host.Services.CreateScope(); Assert.False((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Prescriptions.SingleAsync()).IsActive);
    }

    [Theory]
    [InlineData("unattended")]
    [InlineData("missing-record")]
    [InlineData("empty")]
    [InlineData("missing-medication")]
    [InlineData("too-many")]
    public async Task InvalidIssueDoesNotCreateClinicalData(string scenario)
    {
        using var app = new TestApp(); app.SignIn(app.Data.OwnerUserId, "Profesional");
        if (scenario == "unattended") await app.Mutate(db => db.Appointments.Single().Status = AppointmentStatus.Confirmada);
        if (scenario == "missing-record") await app.Mutate(db => db.MedicalRecords.RemoveRange(db.MedicalRecords));
        object payload = new { generalIndications = "Prueba", items = new[] { new { medication = "Prueba" } } };
        if (scenario == "empty") payload = new { items = Array.Empty<object>() };
        if (scenario == "missing-medication") payload = new { items = new[] { new { medication = " " } } };
        if (scenario == "too-many") payload = new { items = Enumerable.Range(0, 21).Select(_ => new { medication = "Prueba" }).ToArray() };
        using var response = await app.Client.PostAsJsonAsync(app.AppointmentUrl, payload); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = app.Host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Empty(await db.ClinicalEncounters.ToListAsync()); Assert.Empty(await db.Prescriptions.ToListAsync());
    }

    [Fact]
    public async Task IndicationsOnlyExposeOwnAttendedRecommendations_WithDateAndProfessional()
    {
        using var app = new TestApp(); app.SignIn(app.Data.PatientId, "Paciente");
        var rows = await app.Client.GetFromJsonAsync<List<PatientIndicationDto>>("/api/prescriptions/indications/mine"); var row = Assert.Single(rows!);
        Assert.Equal("Indicaciones registradas", row.Content); Assert.Equal("Profesional de prueba", row.Professional);
        using var detail = await app.Client.GetAsync($"/api/prescriptions/indications/{row.Id}"); Assert.Equal(HttpStatusCode.OK, detail.StatusCode); Assert.DoesNotContain("Nota privada", await detail.Content.ReadAsStringAsync());
        app.SignIn(app.Data.OtherUserId, "Paciente"); using var foreign = await app.Client.GetAsync($"/api/prescriptions/indications/{row.Id}"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        app.SignIn(app.Data.PatientId, "Paciente"); await app.Mutate(db => db.Appointments.Single().Status = AppointmentStatus.Confirmada);
        Assert.Empty((await app.Client.GetFromJsonAsync<List<PatientIndicationDto>>("/api/prescriptions/indications/mine"))!);
    }

    [Fact]
    public async Task ExistingEncounterIsPreserved_AndInvalidIdentifierIsUnauthorized()
    {
        using var app = new TestApp(); await app.Mutate(db => db.ClinicalEncounters.Add(new ClinicalEncounter { AppointmentId = app.Data.AppointmentId, PatientId = app.Data.PatientId, ProfessionalId = app.Data.ProfessionalId, Status = "Abierta", Notes = "Nota original" }));
        await app.Create(); using var scope = app.Host.Services.CreateScope(); var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ClinicalEncounters.SingleAsync(); Assert.Equal("Nota original", row.Notes); Assert.Equal("Abierta", row.Status);
        app.Client.DefaultRequestHeaders.Remove("X-Test-UserId"); app.Client.DefaultRequestHeaders.Add("X-Test-UserId", "invalid"); using var response = await app.Client.GetAsync("/api/prescriptions/mine"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class TestApp : IDisposable
    {
        public IHost Host { get; }
        public HttpClient Client { get; }
        public TestAppointments Data { get; }
        public string AppointmentUrl => $"/api/prescriptions/appointments/{Data.AppointmentId}";
        public TestApp()
        {
            var database = Guid.NewGuid().ToString();
            Host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services => {
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(database).ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
                services.AddControllers().AddApplicationPart(typeof(PrescriptionsController).Assembly); services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { }); services.AddAuthorization();
            }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).Start();
            Client = Host.GetTestClient(); using var scope = Host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Data = TestAppointments.Seed(db, withRecord: true);
            db.Appointments.Single().Status = AppointmentStatus.Atendida; var record = db.MedicalRecords.Single(); record.Recommendations = "Indicaciones registradas"; record.Observations = "Nota privada"; db.SaveChanges();
        }
        public void SignIn(Guid userId, string role) { Client.DefaultRequestHeaders.Remove("X-Test-UserId"); Client.DefaultRequestHeaders.Remove("X-Test-Role"); Client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString()); Client.DefaultRequestHeaders.Add("X-Test-Role", role); }
        public Task<HttpResponseMessage> Issue() => Client.PostAsJsonAsync(AppointmentUrl, new { generalIndications = "Indicaciones de prueba", items = new[] { new { medication = "Medicamento de prueba", instructions = "Texto registrado por el profesional" } } });
        public async Task<PrescriptionDto> Create() { SignIn(Data.OwnerUserId, "Profesional"); using var response = await Issue(); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<PrescriptionDto>())!; }
        public async Task Mutate(Action<AppDbContext> action) { using var scope = Host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); action(db); await db.SaveChangesAsync(); }
        public void Dispose() { Client.Dispose(); Host.Dispose(); }
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
            if (!Request.Headers.TryGetValue("X-Test-UserId", out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())], "Test")), "Test")));
        }
    }
}
