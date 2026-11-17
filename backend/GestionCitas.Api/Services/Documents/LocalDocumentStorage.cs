using Microsoft.Extensions.Options;

namespace GestionCitas.Api.Services.Documents;

public sealed class LocalDocumentStorage : IDocumentStorage
{
    private readonly string root;

    public LocalDocumentStorage(IWebHostEnvironment environment, IOptions<MedicalDocumentOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.RootPath))
            throw new InvalidOperationException("Configura MedicalDocuments:RootPath.");

        root = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);
        if (IsInside(root, environment.ContentRootPath) ||
            (!string.IsNullOrEmpty(environment.WebRootPath) && IsInside(root, environment.WebRootPath)))
            throw new InvalidOperationException("Los documentos deben estar fuera del directorio de publicación y de las carpetas públicas.");
    }

    private static bool IsInside(string path, string parent)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        return path.Equals(parent, comparison) || path.StartsWith(parent + Path.DirectorySeparatorChar, comparison);
    }

    private string GetPath(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _))
            throw new ArgumentException("Clave de documento inválida.", nameof(key));
        return Path.Combine(root, key);
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        var path = GetPath(key);
        Directory.CreateDirectory(root);
        var created = false;
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, useAsync: true);
            created = true;
            await content.CopyToAsync(output, cancellationToken);
        }
        catch
        {
            // No borrar un archivo preexistente cuando CreateNew rechaza una colisión.
            if (created) File.Delete(path);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult<Stream?>(new FileStream(GetPath(key), FileMode.Open,
                FileAccess.Read, FileShare.Read, 81920, useAsync: true));
        }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
        catch (DirectoryNotFoundException) { return Task.FromResult<Stream?>(null); }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(GetPath(key));
        return Task.CompletedTask;
    }
}
