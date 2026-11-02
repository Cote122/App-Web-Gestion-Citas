namespace GestionCitas.Api.Services;

public class EmailSettings
{
    // Desactivado por defecto para evitar enviar accidentalmente correos
    // acumulados durante las pruebas locales.
    public bool Enabled { get; set; } = false;

    // Permite validar SMTP con un correo de prueba sin comenzar a despachar
    // automáticamente todo NotificationOutbox.
    public bool AutomaticDispatchEnabled { get; set; } = false;

    public string SmtpHost { get; set; } = "smtpout.secureserver.net";
    public int SmtpPort { get; set; } = 465;
    public bool UseSsl { get; set; } = true;

    // Username y Password deben venir de User Secrets o variables de entorno.
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "no-reply@mu-tante.com";
    public string FromName { get; set; } = "Gestión de Citas";

    // Durante pruebas puede forzarse un único destinatario real para no enviar
    // mensajes a correos ficticios de la base demo.
    public string TestRecipientOverride { get; set; } = string.Empty;

    public int ProcessIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 20;
}
