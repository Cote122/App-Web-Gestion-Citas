namespace GestionCitas.Api.Common;

public static class PeruClock
{
    private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

    /// <summary>
    /// Hora oficial de Perú (UTC-5). Perú no utiliza horario de verano.
    /// No depende de la zona horaria del navegador, VPN o servidor.
    /// </summary>
    public static DateTimeOffset Now =>
        DateTimeOffset.UtcNow.ToOffset(PeruOffset);
}
