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

public class ClinicalResultsTests
{
    internal static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nResultado ficticio\n%%EOF");

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Caja", HttpStatusCode.Forbidden)]
    [InlineData("Administrador", HttpStatusCode.Forbidden)]
    public async Task AnonymousAndOtherRoles_CannotReadResultsOrFiles(string? role, HttpStatusCode expected)
    {
        using var app = new TestApplication();
        if (role is not null) app.SignIn(app.Data.PatientId, role);
        foreach (var url in new[] { "/api/clinical-results/mine", app.DetailUrl, app.DownloadUrl })
        {
            using var response = await app.Client.GetAsync(url);
            Assert.Equal(expected, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("Pendiente", true, true, false)]
    [InlineData("Publicado", false, true, false)]
    [InlineData("Publicado", true, false, false)]
    [InlineData("Anulado", true, true, false)]
    [InlineData("Publicado", true, true, true)]
    public async Task Patient_RequiresPublishedStateVisibilityAndDate(string status, bool visible, bool dated, bool accessible)
    {
        using var app = new TestApplication();
        await app.Mutate(db => { var r = db.ClinicalResults.Single(); r.Status = status; r.IsVisibleToPatient = visible; r.PublishedAt = dated ? DateTimeOffset.UtcNow.AddDays(-1) : null; });
        app.SignIn(app.Data.PatientId, "Paciente");
        using var list = await app.Client.GetAsync("/api/clinical-results/mine");
        var results = await list.Content.ReadFromJsonAsync<List<ClinicalResultDto>>();
        Assert.Equal(accessible ? 1 : 0, results!.Count);
        using var detail = await app.Client.GetAsync(app.DetailUrl);
        using var file = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(accessible ? HttpStatusCode.OK : HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(accessible ? HttpStatusCode.OK : HttpStatusCode.NotFound, file.StatusCode);
    }

    [Theory]
    [InlineData("foreign-patient")]
    [InlineData("foreign-appointment")]
    [InlineData("foreign-encounter")]
    [InlineData("future-publication")]
    public async Task Patient_CannotAccessForeignInconsistentOrFutureResults(string scenario)
    {
        using var app = new TestApplication();
        await app.Mutate(db => {
            var r = db.ClinicalResults.Single();
            if (scenario == "foreign-patient") r.PatientId = Guid.NewGuid();
            if (scenario == "foreign-appointment") r.AppointmentId = Guid.NewGuid();
            if (scenario == "foreign-encounter") r.EncounterId = Guid.NewGuid();
            if (scenario == "future-publication") r.PublishedAt = DateTimeOffset.UtcNow.AddDays(1);
        });
        app.SignIn(app.Data.PatientId, "Paciente");
        Assert.Empty((await app.Client.GetFromJsonAsync<List<ClinicalResultDto>>("/api/clinical-results/mine"))!);
        using var detail = await app.Client.GetAsync(app.DetailUrl);
        using var download = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task PatientCanReadDirectPatientResult_AndEncounterOnlyResult_WithConsistentLinks()
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.PatientId, "Paciente");
        await app.Mutate(db => db.ClinicalResults.Single().AppointmentId = null);
        using var direct = await app.Client.GetAsync(app.DetailUrl);
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        await app.Mutate(db => {
            var encounter = new ClinicalEncounter { PatientId = app.Data.PatientId, AppointmentId = app.Data.AppointmentId, ProfessionalId = app.Data.ProfessionalId };
            db.ClinicalEncounters.Add(encounter); db.ClinicalResults.Single().EncounterId = encounter.Id;
        });
        using var linked = await app.Client.GetAsync(app.DetailUrl);
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
    }

    [Fact]
    public async Task ProfessionalCreatesDraft_WithOwnershipDerivedFromAppointment_IgnoringForgedFields()
    {
        using var app = new TestApplication();
        app.SignIn(app.Data.OwnerUserId, "Profesional");
        using var response = await app.Client.PostAsJsonAsync(app.AppointmentUrl, new {
            resultType = "Laboratorio", title = " Hemograma ", summary = "Contenido ficticio", resultDate = (DateTimeOffset?)null,
            patientId = Guid.NewGuid(), createdBy = Guid.NewGuid(), status = "Publicado", isVisibleToPatient = true, publishedAt = DateTimeOffset.UtcNow
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<ClinicalResultDto>())!;
        Assert.Equal("Pendiente", result.Status); Assert.False(result.IsVisibleToPatient); Assert.Null(result.PublishedAt); Assert.True(result.CanManage);
        using var scope = app.Host.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ClinicalResults.SingleAsync(x => x.Id == result.Id);
        Assert.Equal(app.Data.PatientId, saved.PatientId); Assert.Equal(app.Data.OwnerUserId, saved.CreatedBy);
        Assert.Equal(app.Data.AppointmentId, saved.AppointmentId); Assert.Null(saved.EncounterId); Assert.Equal("Hemograma", saved.Title);
    }

    [Theory]
    [InlineData("Paciente")]
    [InlineData("Administrador")]
    [InlineData("Caja")]
    public async Task OtherRoles_CannotCreateEditPublishOrAttach(string role)
    {
        using var app = new TestApplication(); app.SignIn(app.Data.PatientId, role);
        using var create = await app.Client.PostAsJsonAsync(app.AppointmentUrl, app.Payload);
        using var update = await app.Client.PutAsJsonAsync(app.DetailUrl, app.Payload);
        using var publish = await app.Client.PatchAsJsonAsync(app.PublicationUrl, new { publish = false });
        using var upload = await app.Upload();
        foreach (var response in new[] { create, update, publish, upload }) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Professional_CannotManageResultsOfOtherAuthorsOrAppointments(bool sameAppointment)
    {
        using var app = new TestApplication();
        if (sameAppointment) await app.Mutate(db => db.ClinicalResults.Single().CreatedBy = app.Data.OtherUserId);
        app.SignIn(sameAppointment ? app.Data.OwnerUserId : app.Data.OtherUserId, "Profesional");
        using var publish = await app.Client.PatchAsJsonAsync(app.PublicationUrl, new { publish = false });
        using var edit = await app.Client.PutAsJsonAsync(app.DetailUrl, app.Payload);
        using var upload = await app.Upload();
        Assert.Equal(HttpStatusCode.NotFound, publish.StatusCode); Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode); Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        using var detail = await app.Client.GetAsync(app.DetailUrl);
        Assert.Equal(sameAppointment ? HttpStatusCode.OK : HttpStatusCode.NotFound, detail.StatusCode);
        if (sameAppointment) Assert.False((await detail.Content.ReadFromJsonAsync<ClinicalResultDto>())!.CanManage);
    }

    [Fact]
    public async Task PublishThenWithdraw_RechecksPatientAccessOnEveryDownload_AndPreservesFile()
    {
        using var app = new TestApplication(); app.SignIn(app.Data.OwnerUserId, "Profesional");
        using var withdraw = await app.Client.PatchAsJsonAsync(app.PublicationUrl, new { publish = false });
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
        app.SignIn(app.Data.PatientId, "Paciente");
        using var hidden = await app.Client.GetAsync(app.DownloadUrl); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        app.SignIn(app.Data.OwnerUserId, "Profesional");
        using var published = await app.Client.PatchAsJsonAsync(app.PublicationUrl, new { publish = true });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var result = (await published.Content.ReadFromJsonAsync<ClinicalResultDto>())!;
        Assert.NotNull(result.PublishedAt); Assert.True(result.IsVisibleToPatient);
        app.SignIn(app.Data.PatientId, "Paciente");
        using var visible = await app.Client.GetAsync(app.DownloadUrl);
        Assert.Equal(Pdf, await visible.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", visible.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("no-store", visible.Headers.CacheControl?.ToString() ?? "");
        Assert.Equal("nosniff", Assert.Single(visible.Headers.GetValues("X-Content-Type-Options")));
        var json = await app.Client.GetStringAsync(app.DetailUrl);
        Assert.DoesNotContain("storagePath", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdBy", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fileHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.Single(app.Storage.Files);
    }

    [Fact]
    public async Task EmptyResultCannotPublish_ButAttachedReportIsEnough_AndPublishedContentCannotChange()
    {
        using var app = new TestApplication(); app.SignIn(app.Data.OwnerUserId, "Profesional");
        await app.Mutate(db => { var r = db.ClinicalResults.Single(); r.Status = "Pendiente"; r.IsVisibleToPatient = false; r.PublishedAt = null; r.Summary = null; db.ClinicalResultFiles.RemoveRange(db.ClinicalResultFiles); });
        using var empty = await app.Client.PatchAsJsonAsync(app.PublicationUrl, new { publish = true }); Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        using var upload = await app.Upload(); Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var file = (await upload.Content.ReadFromJsonAsync<ClinicalResultFileDto>())!;
        Assert.Equal("application/pdf", file.ContentType);
        using var published = await app.Client.PatchAsJsonAsync(app.PublicationUrl, new { publish = true }); Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        using var edit = await app.Client.PutAsJsonAsync(app.DetailUrl, app.Payload); Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        using var attach = await app.Upload(); Assert.Equal(HttpStatusCode.Conflict, attach.StatusCode);
        app.SignIn(app.Data.PatientId, "Paciente");
        using var download = await app.Client.GetAsync($"{app.DetailUrl}/files/{file.Id}/download"); Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ForeignFileIdOrMissingFile_Is404_AndLegacyPathsAreNeverOpened()
    {
        using var app = new TestApplication(); app.SignIn(app.Data.PatientId, "Paciente");
        using var wrong = await app.Client.GetAsync($"{app.DetailUrl}/files/{Guid.NewGuid()}/download"); Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        await app.Mutate(db => db.ClinicalResultFiles.Single().ClinicalResultId = Guid.NewGuid());
        using var mismatch = await app.Client.GetAsync(app.DownloadUrl); Assert.Equal(HttpStatusCode.NotFound, mismatch.StatusCode);
        await app.Mutate(db => { var f = db.ClinicalResultFiles.Single(); f.ClinicalResultId = app.ResultId; f.StoragePath = "C:\\private\\report.pdf"; });
        using var legacy = await app.Client.GetAsync(app.DownloadUrl); Assert.Equal(HttpStatusCode.NotFound, legacy.StatusCode);
        Assert.Equal(0, app.Storage.Reads);
        await app.Mutate(db => db.ClinicalResultFiles.Single().StoragePath = Guid.NewGuid().ToString("N"));
        using var missing = await app.Client.GetAsync(app.DownloadUrl); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("invalid.exe", 25)]
    [InlineData("fake.png", 25)]
    [InlineData("empty.pdf", 0)]
    [InlineData("large.pdf", 4097)]
    public async Task InvalidReportIsRejectedWithoutPersisting(string name, int size)
    {
        using var app = new TestApplication(); app.SignIn(app.Data.OwnerUserId, "Profesional");
        await app.MakePending(); var bytes = new byte[size]; Array.Copy(Pdf, bytes, Math.Min(Pdf.Length, bytes.Length));
        using var response = await app.Upload(name, bytes); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(app.Storage.Files);
    }

    [Fact]
    public async Task FailedUploadCompensatesStorage_AndFailedPublicationLeavesDraft()
    {
        using var app = new TestApplication(); app.SignIn(app.Data.OwnerUserId, "Profesional");
        await app.MakePending(); app.Failures.Fail = true;
        using var upload = await app.Upload(); Assert.Equal(HttpStatusCode.ServiceUnavailable, upload.StatusCode); Assert.Single(app.Storage.Files);
        using var publish = await app.Client.PatchAsJsonAsync(app.PublicationUrl, new { publish = true }); Assert.Equal(HttpStatusCode.ServiceUnavailable, publish.StatusCode);
        using var scope = app.Host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Pendiente", (await db.ClinicalResults.SingleAsync()).Status); Assert.Single(await db.ClinicalResultFiles.ToListAsync());
    }

    [Fact]
    public async Task MissingPublicationFlagAndInvalidClaim_AreRejected()
    {
        using var app = new TestApplication(); app.SignIn(app.Data.OwnerUserId, "Profesional");
        using var flag = await app.Client.PatchAsJsonAsync(app.PublicationUrl, new { }); Assert.Equal(HttpStatusCode.BadRequest, flag.StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-UserId"); app.Client.DefaultRequestHeaders.Add("X-Test-UserId", "invalid");
        using var detail = await app.Client.GetAsync(app.DetailUrl); Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
    }

    internal sealed class TestApplication : IDisposable
    {
        public IHost Host { get; }
        public HttpClient Client { get; }
        public TestAppointments Data { get; }
        public TestStorage Storage { get; } = new();
        public FailureInterceptor Failures { get; } = new();
        public Guid ResultId { get; } = Guid.NewGuid();
        public Guid FileId { get; } = Guid.NewGuid();
        public string Key { get; } = Guid.NewGuid().ToString("N");
        public string DetailUrl => $"/api/clinical-results/{ResultId}";
        public string DownloadUrl => $"{DetailUrl}/files/{FileId}/download";
        public string PublicationUrl => $"{DetailUrl}/publication";
        public string AppointmentUrl => $"/api/clinical-results/appointments/{Data.AppointmentId}";
        public object Payload => new { resultType = "Ecografía", title = "Informe", summary = "Hallazgos ficticios" };

        public TestApplication()
        {
            var database = Guid.NewGuid().ToString();
            Host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services => {
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(database).AddInterceptors(Failures));
                services.AddSingleton<IDocumentStorage>(Storage); services.Configure<MedicalDocumentOptions>(o => o.MaxFileSizeBytes = 4096);
                services.AddControllers().AddApplicationPart(typeof(ClinicalResultsController).Assembly);
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                services.AddAuthorization();
            }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).Start();
            Client = Host.GetTestClient();
            using var scope = Host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Data = TestAppointments.Seed(db);
            db.ClinicalResults.Add(new ClinicalResult { Id = ResultId, PatientId = Data.PatientId, AppointmentId = Data.AppointmentId, CreatedBy = Data.OwnerUserId,
                ResultType = "Laboratorio", Title = "Hemograma ficticio", Summary = "Contenido ficticio", Status = "Publicado", IsVisibleToPatient = true,
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-1), Files = [new ClinicalResultFile { Id = FileId, FileName = "informe.pdf", ContentType = "application/pdf", FileSizeBytes = Pdf.Length, StoragePath = Key }] });
            db.SaveChanges(); Storage.Files.Add(Key, Pdf);
        }
        public void SignIn(Guid id, string role) {
            Client.DefaultRequestHeaders.Remove("X-Test-UserId"); Client.DefaultRequestHeaders.Remove("X-Test-Role");
            Client.DefaultRequestHeaders.Add("X-Test-UserId", id.ToString()); Client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }
        public async Task Mutate(Action<AppDbContext> action) {
            using var scope = Host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); action(db); await db.SaveChangesAsync();
        }
        public Task MakePending() => Mutate(db => { var r = db.ClinicalResults.Single(); r.Status = "Pendiente"; r.IsVisibleToPatient = false; r.PublishedAt = null; });
        public async Task<HttpResponseMessage> Upload(string name = "informe.pdf", byte[]? bytes = null) {
            using var payload = new MultipartFormDataContent(); var file = new ByteArrayContent(bytes ?? Pdf); file.Headers.ContentType = new("text/html"); payload.Add(file, "file", name);
            return await Client.PostAsync($"{DetailUrl}/files", payload);
        }
        public void Dispose() { Client.Dispose(); Host.Dispose(); }
    }

    internal sealed class TestStorage : IDocumentStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public int Reads { get; private set; }
        public async Task SaveAsync(string key, Stream content, CancellationToken token) { using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, token); Files.Add(key, buffer.ToArray()); }
        public Task<Stream?> OpenReadAsync(string key, CancellationToken token) { Reads++; return Task.FromResult(Files.TryGetValue(key, out var bytes) ? (Stream?)new MemoryStream(bytes) : null); }
        public Task DeleteAsync(string key, CancellationToken token) { Files.Remove(key); return Task.CompletedTask; }
    }
    internal sealed class FailureInterceptor : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken token = default) {
            if (Fail) throw new DbUpdateException("Fallo simulado"); return ValueTask.FromResult(result);
        }
    }
    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
            if (!Request.Headers.TryGetValue("X-Test-UserId", out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
