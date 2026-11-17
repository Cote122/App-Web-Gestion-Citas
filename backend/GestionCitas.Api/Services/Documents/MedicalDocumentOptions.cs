using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Services.Documents;

public class MedicalDocumentOptions
{
    // Relativa al directorio de la aplicación: en MonsterASP queda fuera de /wwwroot.
    [Required]
    public string RootPath { get; set; } = "../private-medical-documents";

    // El límite HTTP admite hasta 10 MiB más el encabezado multipart.
    [Range(1, 10 * 1024 * 1024)]
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
}
