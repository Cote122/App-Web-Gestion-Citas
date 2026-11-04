using Microsoft.Extensions.Options;

namespace GestionCitas.Api.Services;

public class EmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailSettings> options,
    ILogger<EmailOutboxWorker> logger)
    : BackgroundService
{
    private readonly EmailSettings settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Clamp(settings.ProcessIntervalSeconds, 10, 3600));

        if (!settings.Enabled || !settings.AutomaticDispatchEnabled)
        {
            logger.LogInformation(
                "EmailOutboxWorker desactivado. Para despacho automático configura " +
                "Email:Enabled=true y Email:AutomaticDispatchEnabled=true.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (settings.Enabled && settings.AutomaticDispatchEnabled)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<EmailOutboxService>();
                    var sent = await service.ProcessPendingAsync(stoppingToken);

                    if (sent > 0)
                        logger.LogInformation("Se enviaron {Count} correos desde NotificationOutbox.", sent);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error general procesando NotificationOutbox.");
                }
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
