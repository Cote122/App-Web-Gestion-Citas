namespace GestionCitas.Api.Services.Documents;

public static class DocumentFileValidator
{
    public static readonly string[] Extensions = [".pdf", ".jpg", ".jpeg", ".png"];

    public static async Task<(string FileName, string ContentType)> ValidateAsync(
        IFormFile file, long maxSize, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
            throw new DocumentValidationException("El archivo está vacío.");
        if (file.Length > maxSize)
            throw new DocumentValidationException($"El archivo supera el máximo de {maxSize / 1024d / 1024d:0.##} MB.");

        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl))
            throw new DocumentValidationException("El nombre del archivo no es válido (máximo 255 caracteres).");

        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!Extensions.Contains(extension))
            throw new DocumentValidationException("Solo se permiten archivos PDF, JPG y PNG.");

        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        // No confiar en Content-Type ni en la extensión que envía el navegador.
        var valid = extension switch
        {
            ".pdf" => read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            ".png" => read >= 8 && header.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
            _ => false
        };
        if (!valid)
            throw new DocumentValidationException("El contenido del archivo no coincide con su formato PDF, JPG o PNG.");

        return (name, extension == ".pdf" ? "application/pdf" : extension == ".png" ? "image/png" : "image/jpeg");
    }
}

public sealed class DocumentValidationException(string message) : Exception(message) { }
