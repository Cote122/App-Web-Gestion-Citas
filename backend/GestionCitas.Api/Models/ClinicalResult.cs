using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

// Contrato del catálogo clínico existente. Status no se convierte a un enum de EF.
public class ClinicalResult
{
    public const string Pending = "Pendiente";
    public const string Published = "Publicado";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid? EncounterId { get; set; }
    public Guid? AppointmentId { get; set; }
    [MaxLength(100)] public string ResultType { get; set; } = string.Empty;
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    [MaxLength(30)] public string Status { get; set; } = Pending;
    public bool IsVisibleToPatient { get; set; }
    public DateTimeOffset? ResultDate { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ClinicalResultFile> Files { get; set; } = [];
}

public class ClinicalResultFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicalResultId { get; set; }
    [MaxLength(260)] public string FileName { get; set; } = string.Empty;
    [MaxLength(120)] public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    [MaxLength(1000)] public string StoragePath { get; set; } = string.Empty;
    [MaxLength(128)] public string? FileHash { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ClinicalEncounter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public Guid ProfessionalId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Notes { get; set; }
    public string? DiagnosisSummary { get; set; }
    public string? Indications { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "Abierta";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
