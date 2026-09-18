using Auditoria.Notificaciones.Mensajeria;
using Auditoria.Notificaciones.Procesamiento;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Auditoria.Notificaciones.Tests.Integracion;

public sealed class ConsumidorNotificacionesTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();
    private readonly EnviadorEnMemoria _enviador = new();
    private readonly RegistroEnMemoria _registro = new();
    private OpcionesRabbitMq _opciones = null!;

    public async Task InitializeAsync()
    {
        await _rabbit.StartAsync();
        var uri = new Uri(_rabbit.GetConnectionString());
        var credenciales = uri.UserInfo.Split(':');
        _opciones = new OpcionesRabbitMq
        {
            Host = uri.Host,
            Puerto = uri.Port,
            Usuario = Uri.UnescapeDataString(credenciales[0]),
            Clave = Uri.UnescapeDataString(credenciales[1]),
            Cola = "notificaciones-prueba",
            MaximoIntentos = 3,
            BackoffBaseMilisegundos = 50
        };
    }

    public Task DisposeAsync() => _rabbit.DisposeAsync().AsTask();

    private ConsumidorNotificaciones CrearConsumidor() => new(
        Options.Create(_opciones),
        new ProcesadorNotificaciones(_registro, _enviador, Options.Create(new OpcionesNotificaciones())),
        NullLogger<ConsumidorNotificaciones>.Instance);

    [Fact]
    public async Task Evento_sensible_publicado_genera_un_correo()
    {
        using var consumidor = CrearConsumidor();
        await IniciarAsync(consumidor);

        await PublicarAsync(Guid.NewGuid(), Eventos.Serializar(Eventos.Sensible()));

        await EsperarAsync(() => Task.FromResult(_enviador.Enviados.Count == 1));
        await consumidor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Mensaje_entregado_dos_veces_genera_un_solo_correo()
    {
        using var consumidor = CrearConsumidor();
        await IniciarAsync(consumidor);
        var id = Guid.NewGuid();

        await PublicarAsync(id, Eventos.Serializar(Eventos.Sensible()));
        await PublicarAsync(id, Eventos.Serializar(Eventos.Sensible()));

        await EsperarAsync(async () => _enviador.Enviados.Count >= 1 && await ContarAsync(_opciones.Cola) == 0);
        await Task.Delay(500);
        Assert.Single(_enviador.Enviados);
        await consumidor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Con_smtp_caido_el_mensaje_termina_en_la_dlq_tras_agotar_reintentos()
    {
        _enviador.ServidorCaido = true;
        using var consumidor = CrearConsumidor();
        await IniciarAsync(consumidor);

        await PublicarAsync(Guid.NewGuid(), Eventos.Serializar(Eventos.Sensible()));

        await EsperarAsync(async () => await ContarAsync(_opciones.ColaMuertos) == 1);
        Assert.Empty(_enviador.Enviados);
        await consumidor.StopAsync(CancellationToken.None);
    }

    private async Task IniciarAsync(ConsumidorNotificaciones consumidor)
    {
        await consumidor.StartAsync(CancellationToken.None);
        // La topología (exchange, cola, DLX y DLQ) la declara el consumidor al arrancar.
        await EsperarAsync(ExisteColaAsync);
    }

    private Task<IConnection> ConectarAsync() => new ConnectionFactory
    {
        HostName = _opciones.Host, Port = _opciones.Puerto, UserName = _opciones.Usuario, Password = _opciones.Clave
    }.CreateConnectionAsync();

    private async Task PublicarAsync(Guid messageId, byte[] cuerpo)
    {
        await using var conexion = await ConectarAsync();
        await using var canal = await conexion.CreateChannelAsync();
        var propiedades = new BasicProperties
        {
            MessageId = messageId.ToString(), ContentType = "application/json", Type = "AuditoriaRegistrada",
            DeliveryMode = DeliveryModes.Persistent
        };
        await canal.BasicPublishAsync(_opciones.Exchange, _opciones.RoutingKey, false, propiedades, cuerpo);
    }

    private async Task<uint> ContarAsync(string cola)
    {
        await using var conexion = await ConectarAsync();
        await using var canal = await conexion.CreateChannelAsync();
        return (await canal.QueueDeclarePassiveAsync(cola)).MessageCount;
    }

    private async Task<bool> ExisteColaAsync()
    {
        try
        {
            await ContarAsync(_opciones.Cola);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task EsperarAsync(Func<Task<bool>> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < limite)
        {
            if (await condicion()) return;
            await Task.Delay(200);
        }
        Assert.Fail("La condición no se cumplió en 30 segundos.");
    }
}
