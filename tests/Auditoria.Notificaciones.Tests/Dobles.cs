using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Auditoria.Notificaciones.Correo;
using Auditoria.Notificaciones.Idempotencia;
using Auditoria.Notificaciones.Mensajes;

namespace Auditoria.Notificaciones.Tests;

internal sealed class EnviadorEnMemoria : IEnviadorCorreo
{
    public ConcurrentQueue<CorreoSaliente> Enviados { get; } = new();
    public bool ServidorCaido { get; set; }

    public Task EnviarAsync(CorreoSaliente correo, CancellationToken ct)
    {
        if (ServidorCaido) throw new InvalidOperationException("SMTP no disponible");
        Enviados.Enqueue(correo);
        return Task.CompletedTask;
    }
}

internal sealed class RegistroEnMemoria : IRegistroNotificaciones
{
    private readonly ConcurrentDictionary<Guid, bool> _estado = new(); // true = confirmada

    public Task<bool> ReservarAsync(Guid messageId, CancellationToken ct) => Task.FromResult(_estado.TryAdd(messageId, false));

    public Task ConfirmarAsync(Guid messageId, CancellationToken ct)
    {
        _estado[messageId] = true;
        return Task.CompletedTask;
    }

    public Task LiberarAsync(Guid messageId, CancellationToken ct)
    {
        _estado.TryRemove(new KeyValuePair<Guid, bool>(messageId, false));
        return Task.CompletedTask;
    }

    public bool EstaReservado(Guid messageId) => _estado.ContainsKey(messageId);
}

internal static class Eventos
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // UTC 03:30 del 17/09 = 22:30 del 16/09 en Ecuador.
    public static readonly DateTimeOffset Instante = new(2026, 9, 17, 3, 30, 0, TimeSpan.Zero);

    public static AuditoriaRegistradaMensaje Sensible(string? valorNuevo = "{\"estado\":\"ANULADA\"}") => new(
        Id: 884213, CodigoEmpresa: "0001", CodigoModulo: "06", CodigoTransaccion: "V062014", Usuario: "jperez",
        Entidad: "SolicitudRescate", ClaveEntidad: "RES-2026-004871", Accion: "D", ValorAnterior: "{\"estado\":\"APROBADA\"}",
        ValorNuevo: valorNuevo, DireccionIp: "10.0.4.77", Canal: "API", EsAccionSensible: true, OcurridoEn: Instante);

    public static AuditoriaRegistradaMensaje NoSensible() => Sensible() with { Accion = "C", CodigoModulo = "03", EsAccionSensible = false };

    // Serializa igual que el productor (System.Text.Json, convención web).
    public static byte[] Serializar(AuditoriaRegistradaMensaje evento) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evento, Web));

    public static MensajeEntrante Entrante(AuditoriaRegistradaMensaje evento, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Serializar(evento), "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
}
