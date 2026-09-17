using Auditoria.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;

namespace Auditoria.Infrastructure.Mensajeria;

/// <summary>Publicador para desarrollo: deja el evento en el log estructurado.</summary>
internal sealed class PublicadorLog(ILogger<PublicadorLog> logger) : IPublicadorEventos
{
    public Task PublicarAsync(MensajeOutbox mensaje, CancellationToken ct)
    {
        logger.LogInformation("Evento publicado {Tipo} {MensajeId} {Payload}", mensaje.Tipo, mensaje.Id, mensaje.Payload);
        return Task.CompletedTask;
    }
}
