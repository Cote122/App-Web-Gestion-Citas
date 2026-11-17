using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using GestionCitas.Api.Controllers;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services.Documents;
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

public class MedicalDocumentsTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nDocumento ficticio de prueba\n%%EOF");

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Administrador", HttpStatusCode.Forbidden)]
    [InlineData("Caja", HttpStatusCode.Forbidden)]
    public async Task UnauthorizedRoles_CannotListUploadOrDownload(string? role, HttpStatusCode expected)
    {
        using var app = new TestApplication();
        if (role is not null) app.SignIn(app.Data.PatientId, role);
        using var list = await app.Client.GetAsync(app.ListUrl);
        using var upload = await app.Upload();
        using var download = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(expected, list.StatusCode);
        Assert.Equal(expected, upload.StatusCode);
        Assert.Equal(expected, download.StatusCode);
        Assert.Single(app.Storage.Files);
    }

    [Theory]
    [InlineData("Paciente", true)]
    [InlineData("Profesional", true)]
    [InlineData("Paciente", false)]
    [InlineData("Profesional", false)]
    public async Task Access_IsRestrictedToPatientAndAssignedProfessional(string role, bool owner)
    {
        using var app = new TestApplication();
        var userId = owner ? (role == "Paciente" ? app.Data.PatientId : app.Data.OwnerUserId) : app.Data.OtherUserId;
        app.SignIn(userId, role);

        using var list = await app.Client.GetAsync(app.ListUrl);
        using var download = await app.Client.GetAsync(app.DownloadUrl);
        using var upload = await app.Upload();

        Assert.Equal(owner ? HttpStatusCode.OK : HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(owner ? HttpStatusCode.OK : HttpStatusCode.NotFound, download.StatusCode);
        Assert.Equal(owner ? HttpStatusCode.Created : HttpStatusCode.NotFound, upload.StatusCode);
        if (!owner)
        {
            Assert.DoesNotContain("informe.pdf", await list.Content.ReadAsStringAsync());
            Assert.DoesNotContain("%PDF", await download.Content.ReadAsStringAsync());
            Assert.Single(app.Storage.Files);
        }
    }

    [Fact]
    public async Task Upload_DerivesOwnerAndMime_SanitizesName_AndDownloadsPrivately()
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        using var response = await app.Upload("C:\\fakepath\\resultado.pdf", Pdf, "text/html");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<MedicalDocumentDto>();
        Assert.NotNull(document);
        Assert.Equal("resultado.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(Pdf.Length, document.SizeBytes);
        Assert.Equal(app.Data.AppointmentId, document.AppointmentId);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-medical-documents", json);

        using var scope = app.Host.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().MedicalDocuments.SingleAsync(x => x.Id == document.Id);
        Assert.Equal(app.Data.PatientId, saved.UploadedByUserId);
        Assert.Equal(app.Data.PatientId, saved.PatientId);
        Assert.Equal("Documento", saved.DocumentType);
        Assert.Equal("resultado", saved.Title);
        Assert.True(saved.IsVisibleToPatient);
        Assert.True(Guid.TryParseExact(saved.StorageKey, "N", out _));

        app.SignIn(app.Data.OwnerUserId, "Profesional");
        using var download = await app.Client.GetAsync($"/api/medical-documents/{document.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("no-store", download.Headers.CacheControl?.ToString() ?? "");
        Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
    }

    [Theory]
    [InlineData(".pdf", "JVBERi0xLjc=", "application/pdf")]
    [InlineData(".PDF", "JVBERi0xLjc=", "application/pdf")]
    [InlineData(".png", "iVBORw0KGgo=", "image/png")]
    [InlineData(".jpg", "/9j/", "image/jpeg")]
    [InlineData(".jpeg", "/9j/", "image/jpeg")]
    public async Task AllowedFormats_AreValidatedAgainstTheirSignature(string extension, string base64, string contentType)
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        using var response = await app.Upload("documento" + extension, Convert.FromBase64String(base64));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(contentType, (await response.Content.ReadFromJsonAsync<MedicalDocumentDto>())?.ContentType);
    }

    [Theory]
    [InlineData("ejecutable.exe", 40)]
    [InlineData("vacio.pdf", 0)]
    [InlineData("grande.pdf", 4097)]
    [InlineData("falso.png", 40)]
    [InlineData("falso.pdf", -1)]
    public async Task InvalidFiles_AreRejectedWithoutSavingAnything(string name, int size)
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        var bytes = size < 0 ? Encoding.UTF8.GetBytes("Texto que no es un PDF") : new byte[size];
        if (size >= 5) Array.Copy(Pdf, bytes, Math.Min(bytes.Length, Pdf.Length));
        using var response = await app.Upload(name, bytes);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(app.Storage.Files);
        Assert.Equal(1, await app.DocumentCount());
    }

    [Fact]
    public async Task MissingFile_AndInvalidIdentifier_AreRejected()
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("ignored"), "description");
        using var missing = await app.Client.PostAsync(app.ListUrl, form);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-UserId");
        app.Client.DefaultRequestHeaders.Add("X-Test-UserId", "invalid-guid");
        using var invalidId = await app.Client.GetAsync(app.ListUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidId.StatusCode);
    }

    [Fact]
    public async Task DuplicateNames_CreateSeparateFiles()
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        using var first = await app.Upload("informe.pdf");
        using var second = await app.Upload("informe.pdf");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(3, app.Storage.Files.Count);
        Assert.Equal(Pdf, app.Storage.Files[app.InitialKey]);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelada, false)]
    [InlineData(AppointmentStatus.Reprogramada, false)]
    [InlineData(AppointmentStatus.Atendida, true)]
    public async Task HistoricalDocuments_RemainReadable_AndUploadFollowsAppointmentStatus(AppointmentStatus status, bool canUpload)
    {
        using var app = new TestApplication();
        using (var scope = app.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Appointments.SingleAsync()).Status = status;
            await db.SaveChangesAsync();
        }
        app.SignIn(app.Data.PatientId, "Paciente");
        using var upload = await app.Upload();
        using var download = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(canUpload ? HttpStatusCode.Created : HttpStatusCode.BadRequest, upload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
    }

    [Fact]
    public async Task DatabaseFailure_RemovesNewFile_ButPreservesExistingDocument()
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        app.Failures.FailWrites = true;
        using var response = await app.Upload();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Single(app.Storage.Files);
        Assert.Equal(Pdf, app.Storage.Files[app.InitialKey]);
        Assert.Equal(1, await app.DocumentCount());
    }

    [Fact]
    public async Task StorageFailure_DoesNotPersistMetadata_AndDoesNotExposePhysicalPath()
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        app.Storage.FailWrites = true;
        using var response = await app.Upload();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("C:\\private", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, await app.DocumentCount());
    }

    [Fact]
    public async Task MissingPhysicalFile_ReturnsControlledNotFound()
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        app.Storage.Files.Clear();
        using var response = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Paciente", false)]
    [InlineData("Profesional", true)]
    public async Task HiddenDocuments_RespectExistingVisibility(string role, bool canRead)
    {
        using var app = new TestApplication();
        using (var scope = app.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MedicalDocuments.SingleAsync()).IsVisibleToPatient = false;
            await db.SaveChangesAsync();
        }
        app.SignIn(role == "Paciente" ? app.Data.PatientId : app.Data.OwnerUserId, role);
        using var response = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(canRead ? HttpStatusCode.OK : HttpStatusCode.NotFound, response.StatusCode);
        var list = await app.Client.GetFromJsonAsync<MedicalDocumentDto[]>(app.ListUrl);
        Assert.Equal(canRead ? 1 : 0, list!.Length);
    }

    [Theory]
    [InlineData("Paciente")]
    [InlineData("Profesional")]
    public async Task DocumentWithDifferentPatient_CannotBeReadThroughAnAuthorizedAppointment(string role)
    {
        using var app = new TestApplication();
        using (var scope = app.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MedicalDocuments.SingleAsync()).PatientId = app.Data.OtherUserId;
            await db.SaveChangesAsync();
        }
        app.SignIn(role == "Paciente" ? app.Data.PatientId : app.Data.OwnerUserId, role);
        using var response = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty((await app.Client.GetFromJsonAsync<MedicalDocumentDto[]>(app.ListUrl))!);
    }

    [Theory]
    [InlineData("Paciente")]
    [InlineData("Profesional")]
    public async Task EncounterOnlyDocuments_AreNotExposedByTheAppointmentApi(string role)
    {
        using var app = new TestApplication();
        using (var scope = app.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = await db.MedicalDocuments.SingleAsync();
            document.AppointmentId = null;
            document.EncounterId = Guid.NewGuid();
            await db.SaveChangesAsync();
        }
        app.SignIn(role == "Paciente" ? app.Data.PatientId : app.Data.OwnerUserId, role);
        using var response = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty((await app.Client.GetFromJsonAsync<MedicalDocumentDto[]>(app.ListUrl))!);
    }

    [Fact]
    public async Task LegacyPhysicalPath_IsNotPassedToTheLocalStorageProvider()
    {
        using var app = new TestApplication();
        using (var scope = app.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MedicalDocuments.SingleAsync()).StorageKey = "C:\\legacy\\informe.pdf";
            await db.SaveChangesAsync();
        }
        app.SignIn(app.Data.PatientId, "Paciente");
        using var response = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("C:\\legacy", await response.Content.ReadAsStringAsync());
    }

    private sealed class TestApplication : IDisposable
    {
        public IHost Host { get; }
        public HttpClient Client { get; }
        public TestAppointments Data { get; }
        public TestStorage Storage { get; } = new();
        public WriteFailureInterceptor Failures { get; } = new();
        public Guid InitialDocumentId { get; } = Guid.NewGuid();
        public string InitialKey { get; } = Guid.NewGuid().ToString("N");
        public string ListUrl => $"/api/medical-documents/appointments/{Data.AppointmentId}";
        public string DownloadUrl => $"/api/medical-documents/{InitialDocumentId}/download";

        public TestApplication()
        {
            var database = Guid.NewGuid().ToString();
            Host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(database).AddInterceptors(Failures));
                    services.AddSingleton<IDocumentStorage>(Storage);
                    services.Configure<MedicalDocumentOptions>(o => o.MaxFileSizeBytes = 4096);
                    services.AddControllers().AddApplicationPart(typeof(MedicalDocumentsController).Assembly);
                    services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                    services.AddAuthorization();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                })).Start();
            Client = Host.GetTestClient();
            using var scope = Host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Data = TestAppointments.Seed(db);
            db.MedicalDocuments.Add(new MedicalDocument
            {
                Id = InitialDocumentId, AppointmentId = Data.AppointmentId, PatientId = Data.PatientId,
                UploadedByUserId = Data.OwnerUserId, FileName = "informe.pdf",
                Title = "Informe", DocumentType = "Documento", IsVisibleToPatient = true,
                StorageKey = InitialKey, ContentType = "application/pdf", SizeBytes = Pdf.Length
            });
            db.SaveChanges();
            Storage.Files.Add(InitialKey, Pdf);
        }

        public void SignIn(Guid id, string role)
        {
            Client.DefaultRequestHeaders.Remove("X-Test-UserId");
            Client.DefaultRequestHeaders.Remove("X-Test-Role");
            Client.DefaultRequestHeaders.Add("X-Test-UserId", id.ToString());
            Client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        public async Task<HttpResponseMessage> Upload(string name = "informe.pdf", byte[]? bytes = null, string mime = "application/pdf")
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes ?? Pdf);
            file.Headers.ContentType = new(mime);
            form.Add(file, "file", name);
            return await Client.PostAsync(ListUrl, form);
        }

        public async Task<int> DocumentCount()
        {
            using var scope = Host.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().MedicalDocuments.CountAsync();
        }

        public void Dispose() { Client.Dispose(); Host.Dispose(); }
    }

    private sealed class TestStorage : IDocumentStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public bool FailWrites { get; set; }
        public async Task SaveAsync(string key, Stream content, CancellationToken token)
        {
            if (FailWrites) throw new IOException("C:\\private: acceso fallido simulado");
            using var output = new MemoryStream();
            await content.CopyToAsync(output, token);
            Files.Add(key, output.ToArray());
        }
        public Task<Stream?> OpenReadAsync(string key, CancellationToken token) =>
            Task.FromResult(Files.TryGetValue(key, out var bytes) ? (Stream?)new MemoryStream(bytes) : null);
        public Task DeleteAsync(string key, CancellationToken token) { Files.Remove(key); return Task.CompletedTask; }
    }

    private sealed class WriteFailureInterceptor : SaveChangesInterceptor
    {
        public bool FailWrites { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new DbUpdateException("Fallo de SQL simulado.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-UserId", out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())
            }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
