using Microsoft.Extensions.Options;

namespace GestionCitas.Api.Services;

public class AppointmentCommunicationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CommunicationSettings> options,
    ILogger<AppointmentCommunicationWorker> logger)
    : BackgroundService
{
    private readonly CommunicationSettings settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.ProcessIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<AppointmentCommunicationService>();
                var result = await processor.ProcessAsync(stoppingToken);

                if (result.RemindersCreated > 0 || result.AttendanceRequestsCreated > 0)
                {
                    logger.LogInformation(
                        "Comunicaciones generadas: {Reminders} recordatorios, {Attendance} solicitudes de confirmación.",
                        result.RemindersCreated,
                        result.AttendanceRequestsCreated);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error procesando recordatorios y confirmaciones de asistencia.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
