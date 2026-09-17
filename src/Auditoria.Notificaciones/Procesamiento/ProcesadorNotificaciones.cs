using Auditoria.Notificaciones.Correo;
using Auditoria.Notificaciones.Idempotencia;
using Auditoria.Notificaciones.Mensajes;
using Microsoft.Extensions.Options;

namespace Auditoria.Notificaciones.Procesamiento;

public enum ResultadoProcesamiento
{
    Enviada,
    NoSensible,
    Duplicada
}

public interface IProcesadorNotificaciones
{
    /// <summary>Lanza excepción si el mensaje debe reintentarse.</summary>
    Task<ResultadoProcesamiento> ProcesarAsync(MensajeEntrante mensaje, CancellationToken ct);
}

public sealed class ProcesadorNotificaciones(
    IRegistroNotificaciones registro,
    IEnviadorCorreo enviador,
    IOptions<OpcionesNotificaciones> opciones) : IProcesadorNotificaciones
{
    public Task<ResultadoProcesamiento> ProcesarAsync(MensajeEntrante mensaje, CancellationToken ct) =>
        throw new NotImplementedException();
}
