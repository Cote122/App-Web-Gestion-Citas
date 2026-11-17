using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class MedicalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    [MaxLength(32)]
    public string StorageKey { get; set; } = string.Empty;
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
