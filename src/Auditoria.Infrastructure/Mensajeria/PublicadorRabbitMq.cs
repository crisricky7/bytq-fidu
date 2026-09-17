using System.Text;
using Auditoria.Infrastructure.Outbox;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Auditoria.Infrastructure.Mensajeria;

public sealed class OpcionesRabbitMq
{
    public const string Seccion = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Puerto { get; set; } = 5672;
    public string Usuario { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Exchange { get; set; } = "corefid.auditoria";
}

/// <summary>
/// Publica en un exchange topic durable con confirmación del broker. La conexión se crea bajo
/// demanda y se descarta ante cualquier error, de modo que el siguiente intento reconecta.
/// </summary>
internal sealed class PublicadorRabbitMq(IOptions<OpcionesRabbitMq> opciones) : IPublicadorEventos, IAsyncDisposable
{
    private readonly OpcionesRabbitMq _opciones = opciones.Value;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;
    private IChannel? _canal;

    public async Task PublicarAsync(MensajeOutbox mensaje, CancellationToken ct)
    {
        await _candado.WaitAsync(ct);
        try
        {
            var canal = await ObtenerCanalAsync(ct);
            var propiedades = new BasicProperties
            {
                MessageId = mensaje.Id.ToString(),
                Type = mensaje.Tipo,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(mensaje.OccurredAt.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?> { ["traceparent"] = mensaje.TraceParent }
            };

            await canal.BasicPublishAsync(
                _opciones.Exchange, RoutingKey(mensaje.Tipo), mandatory: false, propiedades,
                Encoding.UTF8.GetBytes(mensaje.Payload), ct);
        }
        catch
        {
            await CerrarAsync();
            throw;
        }
        finally
        {
            _candado.Release();
        }
    }

    private async Task<IChannel> ObtenerCanalAsync(CancellationToken ct)
    {
        if (_canal is { IsOpen: true }) return _canal;

        await CerrarAsync();
        var fabrica = new ConnectionFactory
        {
            HostName = _opciones.Host,
            Port = _opciones.Puerto,
            UserName = _opciones.Usuario,
            Password = _opciones.Clave,
            ClientProvidedName = "auditoria-outbox"
        };
        _conexion = await fabrica.CreateConnectionAsync(ct);
        _canal = await _conexion.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await _canal.ExchangeDeclareAsync(_opciones.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        return _canal;
    }

    // AuditoriaRegistrada -> auditoria.registrada
    private static string RoutingKey(string tipo) => "auditoria." + tipo.Replace("Auditoria", string.Empty).ToLowerInvariant();

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

    public async ValueTask DisposeAsync() => await CerrarAsync();
}
