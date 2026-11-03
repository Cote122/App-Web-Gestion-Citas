namespace GestionCitas.Api.Services;

public class CommunicationSettings
{
    public int ReminderWindowHours { get; set; } = 24;
    public int AttendanceConfirmationWindowHours { get; set; } = 48;
    public int ProcessIntervalMinutes { get; set; } = 5;
}
