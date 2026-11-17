using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionCitas.Api.Models;

public class MedicalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public User Patient { get; set; } = null!;
    public Guid? EncounterId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    [Column("UploadedBy")]
    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
    [MaxLength(80)]
    public string DocumentType { get; set; } = "Documento";
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;
    // La columna existente guarda una referencia interna; no se publica en la API.
    [Column("StoragePath"), MaxLength(1000)]
    public string StorageKey { get; set; } = string.Empty;
    [MaxLength(120)]
    public string? ContentType { get; set; }
    [Column("FileSizeBytes")]
    public long? SizeBytes { get; set; }
    // La visibilidad se define explícitamente al adjuntar; un registro nuevo es privado por defecto.
    public bool IsVisibleToPatient { get; set; }
    [Column("UploadedAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
