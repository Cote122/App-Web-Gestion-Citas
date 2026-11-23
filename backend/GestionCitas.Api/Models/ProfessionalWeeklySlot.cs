namespace GestionCitas.Api.Models;

// Plantilla semanal en hora local de Perú. DayOfWeek: domingo=0, lunes=1, ...
public class ProfessionalWeeklySlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfessionalId { get; set; }
    public Professional Professional { get; set; } = null!;
    public int DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public bool IsEnabled { get; set; } = true;
}
