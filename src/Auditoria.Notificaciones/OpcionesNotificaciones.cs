namespace Auditoria.Notificaciones;

public sealed class OpcionesNotificaciones
{
    public const string Seccion = "Notificaciones";

    public string Destinatario { get; set; } = "seguridad@corefid.com.ec";
    public string ZonaHoraria { get; set; } = "America/Guayaquil";

    /// <summary>Tiempo tras el cual una reserva sin confirmar se considera abandonada (proceso caído).</summary>
    public int VencimientoReservaSegundos { get; set; } = 300;
}
