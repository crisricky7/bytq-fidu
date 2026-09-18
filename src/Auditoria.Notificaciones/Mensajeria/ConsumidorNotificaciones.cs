using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Auditoria.Notificaciones.Mensajes;
using Auditoria.Notificaciones.Procesamiento;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Auditoria.Notificaciones.Mensajeria;

public sealed class OpcionesRabbitMq
{
    public const string Seccion = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Puerto { get; set; } = 5672;
    public string Usuario { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Exchange { get; set; } = "corefid.auditoria";
    public string RoutingKey { get; set; } = "auditoria.registrada";
    public string Cola { get; set; } = "corefid.auditoria.notificaciones";
    public int MaximoIntentos { get; set; } = 5;
    public int BackoffBaseMilisegundos { get; set; } = 500;
    public ushort Prefetch { get; set; } = 10;

    public string ColaMuertos => Cola + ".dlq";
    public string ExchangeMuertos => Cola + ".dlx";
}

/// <summary>
/// Consume auditoria.registrada desde una cola quorum. Los reintentos los cuenta el broker
/// (x-delivery-limit): al superarlos, RabbitMQ mueve el mensaje a la DLQ, así no se pierde.
/// </summary>
public sealed class ConsumidorNotificaciones(
    IOptions<OpcionesRabbitMq> opciones,
    IProcesadorNotificaciones procesador,
    ILogger<ConsumidorNotificaciones> logger) : BackgroundService
{
    public static readonly ActivitySource Fuente = new("Auditoria.Notificaciones");

    private readonly OpcionesRabbitMq _opciones = opciones.Value;
    private IConnection? _conexion;
    private IChannel? _canal;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConectarYConsumirAsync(stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Broker no disponible al arrancar: se reintenta; la recuperación automática cubre caídas posteriores.
                logger.LogError(ex, "No se pudo conectar a RabbitMQ; reintento en 5 s");
                await CerrarAsync();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConectarYConsumirAsync(CancellationToken ct)
    {
        _conexion = await new ConnectionFactory
        {
            HostName = _opciones.Host,
            Port = _opciones.Puerto,
            UserName = _opciones.Usuario,
            Password = _opciones.Clave,
            ClientProvidedName = "auditoria-notificaciones",
            AutomaticRecoveryEnabled = true
        }.CreateConnectionAsync(ct);
        _canal = await _conexion.CreateChannelAsync(cancellationToken: ct);

        await DeclararTopologiaAsync(_canal, ct);
        await _canal.BasicQosAsync(0, _opciones.Prefetch, false, ct);

        var canal = _canal;
        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += (_, entrega) => ManejarAsync(canal, entrega, ct);
        await canal.BasicConsumeAsync(_opciones.Cola, autoAck: false, consumidor, ct);
        logger.LogInformation("Consumiendo {Cola}", _opciones.Cola);
    }

    private async Task DeclararTopologiaAsync(IChannel canal, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(_opciones.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(_opciones.ExchangeMuertos, ExchangeType.Fanout, durable: true, cancellationToken: ct);
        await canal.QueueDeclareAsync(_opciones.ColaMuertos, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(_opciones.ColaMuertos, _opciones.ExchangeMuertos, string.Empty, cancellationToken: ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = _opciones.ExchangeMuertos,
            // x-delivery-limit cuenta devoluciones: N devoluciones = N + 1 intentos.
            ["x-delivery-limit"] = Math.Max(_opciones.MaximoIntentos - 1, 0)
        };
        await canal.QueueDeclareAsync(_opciones.Cola, durable: true, exclusive: false, autoDelete: false, argumentos, cancellationToken: ct);
        await canal.QueueBindAsync(_opciones.Cola, _opciones.Exchange, _opciones.RoutingKey, cancellationToken: ct);
    }

    private async Task ManejarAsync(IChannel canal, BasicDeliverEventArgs entrega, CancellationToken ct)
    {
        var propiedades = entrega.BasicProperties;
        var traceParent = LeerCabecera(propiedades.Headers, "traceparent");
        var intentosPrevios = propiedades.Headers?.TryGetValue("x-delivery-count", out var conteo) == true
            ? Convert.ToInt32(conteo)
            : 0;

        using var actividad = Fuente.StartActivity("notificar acción sensible", ActivityKind.Consumer, traceParent);
        using var alcance = logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"] = propiedades.MessageId,
            ["Intento"] = intentosPrevios + 1
        });

        if (!Guid.TryParse(propiedades.MessageId, out var messageId))
        {
            logger.LogError("Mensaje sin MessageId válido; se envía a la DLQ");
            await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, ct);
            return;
        }

        try
        {
            var mensaje = new MensajeEntrante(messageId, entrega.Body.ToArray(), traceParent);
            var resultado = await procesador.ProcesarAsync(mensaje, ct);
            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
            logger.LogInformation("Mensaje procesado: {Resultado}", resultado);
        }
        catch (JsonException ex)
        {
            // Reintentar un mensaje ilegible no lo arregla: directo a la DLQ.
            logger.LogError(ex, "Mensaje ilegible; se envía a la DLQ");
            await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            actividad?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Falló el envío de la notificación (intento {Intento} de {Maximo})",
                intentosPrevios + 1, _opciones.MaximoIntentos);
            // Backoff antes de devolver el mensaje; el broker lo manda a la DLQ al superar el límite.
            var espera = _opciones.BackoffBaseMilisegundos * Math.Pow(2, Math.Min(intentosPrevios, 10));
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(espera, 60_000)), ct);
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, ct);
        }
    }

    private static string? LeerCabecera(IDictionary<string, object?>? cabeceras, string nombre) =>
        cabeceras?.TryGetValue(nombre, out var valor) == true
            ? valor switch { byte[] bytes => Encoding.UTF8.GetString(bytes), string texto => texto, _ => null }
            : null;

    private async Task CerrarAsync()
    {
        try
        {
            if (_canal is not null) await _canal.DisposeAsync();
            if (_conexion is not null) await _conexion.DisposeAsync();
        }
        catch
        {
            // La conexión ya estaba rota; no hay nada más que liberar.
        }
        _canal = null;
        _conexion = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await CerrarAsync();
    }
}
