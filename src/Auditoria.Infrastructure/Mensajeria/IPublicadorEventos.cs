using Auditoria.Infrastructure.Outbox;

namespace Auditoria.Infrastructure.Mensajeria;

public interface IPublicadorEventos
{
    /// <summary>Debe lanzar excepción si el broker no confirma la recepción.</summary>
    Task PublicarAsync(MensajeOutbox mensaje, CancellationToken ct);
}
