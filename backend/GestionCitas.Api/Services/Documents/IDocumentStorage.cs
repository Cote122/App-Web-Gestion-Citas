namespace GestionCitas.Api.Services.Documents;

// La API conserva sus permisos y metadatos al sustituir el disco por un proveedor de nube.
public interface IDocumentStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
