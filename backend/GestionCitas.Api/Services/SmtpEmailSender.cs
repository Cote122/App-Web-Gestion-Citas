using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GestionCitas.Api.Services;

public class SmtpEmailSender(IOptions<EmailSettings> options)
{
    private readonly EmailSettings settings = options.Value;

    public bool IsEnabled => settings.Enabled;

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        var destination = string.IsNullOrWhiteSpace(settings.TestRecipientOverride)
            ? recipientEmail
            : settings.TestRecipientOverride.Trim();

        if (!MailboxAddress.TryParse(destination, out var recipient))
            throw new InvalidOperationException($"Correo destinatario inválido: {destination}");

        if (!MailboxAddress.TryParse(settings.FromAddress, out var from))
            throw new InvalidOperationException("Email:FromAddress no es una dirección válida.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, from.Address));
        message.To.Add(recipient);
        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = body
        };

        using var client = new SmtpClient
        {
            CheckCertificateRevocation = settings.CheckCertificateRevocation
        };

        var socketOptions = settings.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(
            settings.SmtpHost,
            settings.SmtpPort,
            socketOptions,
            cancellationToken);

        await client.AuthenticateAsync(
            settings.Username,
            settings.Password,
            cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private void ValidateConfiguration()
    {
        if (!settings.Enabled)
            throw new InvalidOperationException("El envío de correo está desactivado.");

        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
            throw new InvalidOperationException("Falta Email:SmtpHost.");

        if (settings.SmtpPort <= 0)
            throw new InvalidOperationException("Email:SmtpPort no es válido.");

        if (string.IsNullOrWhiteSpace(settings.Username))
            throw new InvalidOperationException("Falta Email:Username.");

        if (string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("Falta Email:Password.");

        if (string.IsNullOrWhiteSpace(settings.FromAddress))
            throw new InvalidOperationException("Falta Email:FromAddress.");
    }
}
