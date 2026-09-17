using Auditoria.Notificaciones.Procesamiento;
using Microsoft.Extensions.Options;

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

    public string ColaMuertos => Cola + ".dlq";
    public string ExchangeMuertos => Cola + ".dlx";
}

public sealed class ConsumidorNotificaciones(
    IOptions<OpcionesRabbitMq> opciones,
    IProcesadorNotificaciones procesador,
    ILogger<ConsumidorNotificaciones> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => throw new NotImplementedException();
}
