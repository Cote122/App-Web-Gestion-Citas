using System.ComponentModel.DataAnnotations;
namespace GestionCitas.Api.Models;

public class PrescriptionCorrection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Guid CorrectedBy { get; set; }
    public User Administrator { get; set; } = null!;
    public DateTimeOffset CorrectedAt { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    public string PreviousContent { get; set; } = string.Empty;
    public string UpdatedContent { get; set; } = string.Empty;
    [MaxLength(64)] public string PreviousVersion { get; set; } = string.Empty;
    [MaxLength(64)] public string UpdatedVersion { get; set; } = string.Empty;
}
