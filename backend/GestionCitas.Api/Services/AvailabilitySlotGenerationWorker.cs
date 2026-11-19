using GestionCitas.Api.Data;

namespace GestionCitas.Api.Services;

public sealed class AvailabilitySlotGenerationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AvailabilitySlotGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // La carga inicial se realiza en Program.cs; luego se amplía la ventana mientras la API sigue activa.
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var created = await DbInitializer.EnsureAvailabilitySlotsAsync(
                        db, cancellationToken: stoppingToken);
                    if (created > 0)
                        logger.LogInformation("Se generaron {Count} horarios nuevos.", created);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No se pudieron generar los horarios; se reintentará.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cierre normal de la aplicación.
        }
    }
}
