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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionCitas.Api.Tests;

public class MedicalRecordsAuthorizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousRequests_AreRejected(bool write)
    {
        using var app = new TestApplication();
        using var response = await app.SendAsync(write);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Paciente")]
    [InlineData("Administrador")]
    public async Task OtherRoles_CannotReadOrWriteMedicalRecords(string role)
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.OwnerUserId, role);

        using var read = await app.SendAsync(write: false);
        using var write = await app.SendAsync(write: true);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherProfessional_CannotReadForeignAppointment(bool withRecord)
    {
        using var app = new TestApplication(withRecord);
        app.SignIn(app.Data.OtherUserId);

        using var response = await app.SendAsync(write: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Diagnóstico previo", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherProfessional_CannotCreateOrOverwriteForeignRecord(bool withRecord)
    {
        using var app = new TestApplication(withRecord);
        app.SignIn(app.Data.OtherUserId);

        using var response = await app.SendAsync(write: true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = app.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var appointment = await db.Appointments.SingleAsync();
        Assert.Equal(AppointmentStatus.Confirmada, appointment.Status);
        Assert.Equal("Motivo original", appointment.Reason);
        Assert.Equal(withRecord ? 1 : 0, await db.MedicalRecords.CountAsync());
        if (withRecord)
            Assert.Equal("Diagnóstico previo", (await db.MedicalRecords.SingleAsync()).Diagnosis);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_CanReadOwnAppointmentWithOrWithoutRecord(bool withRecord)
    {
        using var app = new TestApplication(withRecord);
        app.SignIn(app.Data.OwnerUserId);

        using var response = await app.SendAsync(write: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<MedicalRecordDto>();
        Assert.NotNull(record);
        Assert.Equal(app.Data.PatientId, record.PatientId);
        Assert.Equal(app.Data.ProfessionalId, record.ProfessionalId);
        Assert.Equal("Motivo original", record.Reason);
        Assert.Equal(withRecord ? "Diagnóstico previo" : null, record.Diagnosis);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_CanSaveOwnRecord_UsingPatientAndProfessionalFromAppointment(bool withRecord)
    {
        using var app = new TestApplication(withRecord);
        app.SignIn(app.Data.OwnerUserId);

        // El payload envía identificadores falsos; la API debe derivarlos de la cita.
        using var response = await app.SendAsync(write: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = app.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.MedicalRecords.SingleAsync();
        Assert.Equal(app.Data.PatientId, record.PatientId);
        Assert.Equal(app.Data.ProfessionalId, record.ProfessionalId);
        Assert.Equal("Diagnóstico actualizado", record.Diagnosis);
        var appointment = await db.Appointments.SingleAsync();
        Assert.Equal("Motivo actualizado", appointment.Reason);
        Assert.Equal(AppointmentStatus.Atendida, appointment.Status);
    }

    [Fact]
    public async Task ProfessionalWithoutValidUserIdentifier_CannotReadOrWrite()
    {
        using var app = new TestApplication();
        app.Client.DefaultRequestHeaders.Add("X-Test-UserId", "invalid-guid");
        app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Profesional");

        using var read = await app.SendAsync(write: false);
        using var write = await app.SendAsync(write: true);

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    private sealed class TestApplication : IDisposable
    {
        public IHost Host { get; }
        public HttpClient Client { get; }
        public TestAppointments Data { get; }

        public TestApplication(bool withRecord = false)
        {
            var database = Guid.NewGuid().ToString();
            Host = new HostBuilder()
                .ConfigureWebHost(web => web.UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(database));
                        services.AddControllers().AddApplicationPart(typeof(MedicalRecordsController).Assembly);
                        services.AddAuthentication("Test")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                        services.AddAuthorization();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .Start();
            Client = Host.GetTestClient();
            using var scope = Host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Data = TestAppointments.Seed(db, withRecord);
        }

        public void SignIn(Guid userId, string role = "Profesional")
        {
            Client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
            Client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        public Task<HttpResponseMessage> SendAsync(bool write) => write
            ? Client.PostAsJsonAsync("/api/medicalrecords", new CreateMedicalRecordDto(
                Data.AppointmentId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
                "Motivo actualizado", "Observación", "Diagnóstico actualizado", "Recomendación"))
            : Client.GetAsync($"/api/medicalrecords/appointment/{Data.AppointmentId}");

        public void Dispose()
        {
            Client.Dispose();
            Host.Dispose();
        }
    }

    // Este esquema solo existe en el servidor de pruebas; la API real conserva JWT.
    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-UserId", out var id))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())
            }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
