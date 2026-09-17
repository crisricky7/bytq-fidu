using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Auditoria.Infrastructure;

public static class Telemetria
{
    public const string Nombre = "Auditoria";

    public static readonly ActivitySource Fuente = new(Nombre);

    private static readonly Meter Medidor = new(Nombre);

    public static readonly Counter<long> OutboxPublicados =
        Medidor.CreateCounter<long>("auditoria.outbox.publicados", description: "Mensajes del outbox confirmados por el broker");

    public static readonly Counter<long> OutboxFallidos =
        Medidor.CreateCounter<long>("auditoria.outbox.fallidos", description: "Intentos de publicación fallidos");

    /// <summary>Segundos entre que ocurrió el evento y su publicación: mide el retraso del outbox.</summary>
    public static readonly Histogram<double> OutboxRetraso =
        Medidor.CreateHistogram<double>("auditoria.outbox.retraso", unit: "s");
}
