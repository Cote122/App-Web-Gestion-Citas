using GestionCitas.Api.Services.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionCitas.Api.Tests;

public class LocalDocumentStorageTests
{
    [Fact]
    public async Task PrivateFiles_SurviveNewStorageInstance_AndNamesCannotEscapeDirectory()
    {
        using var location = new TestLocation();
        var storage = location.Create();
        var key = Guid.NewGuid().ToString("N");
        await storage.SaveAsync(key, new MemoryStream([1, 2, 3]), CancellationToken.None);
        await using var read = await location.Create().OpenReadAsync(key, CancellationToken.None);
        Assert.NotNull(read);
        using var output = new MemoryStream();
        await read.CopyToAsync(output);
        Assert.Equal(new byte[] { 1, 2, 3 }, output.ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync("../secret", new MemoryStream([1]), CancellationToken.None));
        Assert.Single(Directory.GetFiles(location.PrivatePath));
    }

    [Fact]
    public async Task DuplicateStorageKey_DoesNotOverwriteOrDeleteExistingFile()
    {
        using var location = new TestLocation();
        var storage = location.Create();
        var key = Guid.NewGuid().ToString("N");
        await storage.SaveAsync(key, new MemoryStream([7]), CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(key, new MemoryStream([9]), CancellationToken.None));
        Assert.Equal(new byte[] { 7 }, await File.ReadAllBytesAsync(Path.Combine(location.PrivatePath, key)));
    }

    [Fact]
    public async Task InterruptedWrite_DeletesPartialFile()
    {
        using var location = new TestLocation();
        await Assert.ThrowsAsync<IOException>(() => location.Create().SaveAsync(
            Guid.NewGuid().ToString("N"), new FailingStream(), CancellationToken.None));
        Assert.Empty(Directory.GetFiles(location.PrivatePath));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("uploads")]
    [InlineData("wwwroot/documents")]
    public void PublicationAndPublicDirectories_AreRejected(string root)
    {
        using var location = new TestLocation();
        Assert.Throws<InvalidOperationException>(() => location.Create(root));
    }

    private sealed class FailingStream : MemoryStream
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, cancellationToken);
            throw new IOException("Carga interrumpida simulada.");
        }
    }

    private sealed class TestLocation : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "gestion-documents-tests", Guid.NewGuid().ToString("N"));
        public string PrivatePath => Path.Combine(path, "private-medical-documents");
        public LocalDocumentStorage Create(string root = "../private-medical-documents") => new(
            new TestEnvironment { ContentRootPath = Path.Combine(path, "app"), WebRootPath = Path.Combine(path, "app", "wwwroot") },
            Options.Create(new MedicalDocumentOptions { RootPath = root }));
        public void Dispose() { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
