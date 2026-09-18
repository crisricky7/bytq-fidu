using System.Text.Json;
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
    /// <summary>Lanza excepción si el mensaje debe reintentarse; <see cref="JsonException"/> si es ilegible.</summary>
    Task<ResultadoProcesamiento> ProcesarAsync(MensajeEntrante mensaje, CancellationToken ct);
}

public sealed class ProcesadorNotificaciones(
    IRegistroNotificaciones registro,
    IEnviadorCorreo enviador,
    IOptions<OpcionesNotificaciones> opciones) : IProcesadorNotificaciones
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoProcesamiento> ProcesarAsync(MensajeEntrante mensaje, CancellationToken ct)
    {
        var evento = JsonSerializer.Deserialize<AuditoriaRegistradaMensaje>(mensaje.Cuerpo.Span, OpcionesJson)
            ?? throw new JsonException("Mensaje vacío.");

        if (!evento.EsAccionSensible)
            return ResultadoProcesamiento.NoSensible;

        // Orden: reservar -> enviar -> confirmar. Un duplicado encuentra la reserva y no envía.
        if (!await registro.ReservarAsync(mensaje.MessageId, ct))
            return ResultadoProcesamiento.Duplicada;

        try
        {
            await enviador.EnviarAsync(ConstructorCorreo.Construir(evento, opciones.Value), ct);
        }
        catch
        {
            // Sin correo enviado: se libera para que el reintento pueda enviarlo.
            await registro.LiberarAsync(mensaje.MessageId, CancellationToken.None);
            throw;
        }

        // Si el proceso muere antes de confirmar, la reserva vence y puede salir un duplicado:
        // riesgo aceptado en la spec (mejor un correo repetido que uno perdido).
        await registro.ConfirmarAsync(mensaje.MessageId, ct);
        return ResultadoProcesamiento.Enviada;
    }
}
