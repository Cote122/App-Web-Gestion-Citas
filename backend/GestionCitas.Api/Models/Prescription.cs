using System.ComponentModel.DataAnnotations;
namespace GestionCitas.Api.Models;

public class Prescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EncounterId { get; set; }
    public ClinicalEncounter Encounter { get; set; } = null!;
    public Guid PatientId { get; set; }
    public Guid ProfessionalId { get; set; }
    public Professional Professional { get; set; } = null!;
    public string? GeneralIndications { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
    public List<PrescriptionItem> Items { get; set; } = [];
}
public class PrescriptionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    [MaxLength(200)] public string Medication { get; set; } = string.Empty;
    [MaxLength(100)] public string? Dose { get; set; }
    [MaxLength(100)] public string? Frequency { get; set; }
    [MaxLength(100)] public string? Duration { get; set; }
    [MaxLength(500)] public string? Instructions { get; set; }
    public int SortOrder { get; set; } = 1;
}
