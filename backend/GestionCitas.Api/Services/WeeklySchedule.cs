using GestionCitas.Api.Models;

namespace GestionCitas.Api.Services;

public static class WeeklySchedule
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan[] LegacyHours = [
        TimeSpan.FromHours(9), TimeSpan.FromHours(10), TimeSpan.FromHours(11),
        TimeSpan.FromHours(15), TimeSpan.FromHours(16)
    ];

    public static bool Allows(Professional professional, DateTimeOffset start, IEnumerable<ProfessionalWeeklySlot> weeklySlots)
    {
        var local = start.ToOffset(TimeSpan.FromHours(-5));
        var time = local.TimeOfDay;
        if (!professional.ScheduleConfigured)
            return local.DayOfWeek != DayOfWeek.Sunday && LegacyHours.Contains(time);

        return weeklySlots.Any(x => x.ProfessionalId == professional.Id && x.IsEnabled &&
                                    x.DayOfWeek == (int)local.DayOfWeek && x.StartTime == time);
    }
}
