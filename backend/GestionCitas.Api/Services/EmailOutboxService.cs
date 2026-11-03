using GestionCitas.Api.Common;
using GestionCitas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestionCitas.Api.Services;

public class EmailOutboxService(
    AppDbContext db,
    SmtpEmailSender emailSender,
    IOptions<EmailSettings> options,
    ILogger<EmailOutboxService> logger)
{
    private readonly EmailSettings settings = options.Value;

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return 0;

        var batchSize = Math.Clamp(settings.BatchSize, 1, 100);

        var pending = await db.NotificationOutbox
            .Where(x => x.Status == "Pendiente")
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var item in pending)
        {
            item.Status = "Enviando";
            item.LastError = null;
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                await emailSender.SendAsync(
                    item.RecipientEmail,
                    item.Subject,
                    item.Body,
                    cancellationToken);

                item.Status = "Enviado";
                item.SentAt = PeruClock.Now;
                item.LastError = null;
                sent++;
            }
            catch (Exception ex)
            {
                item.Status = "Error";
                item.SentAt = null;
                item.LastError = ex.Message.Length <= 1000
                    ? ex.Message
                    : ex.Message[..1000];

                logger.LogError(
                    ex,
                    "No se pudo enviar NotificationOutbox {NotificationId} a {Recipient}.",
                    item.Id,
                    item.RecipientEmail);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }
}
