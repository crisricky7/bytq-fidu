namespace Auditoria.Notificaciones.Idempotencia;

/// <summary>Garantiza que un mismo MessageId produzca como máximo un correo.</summary>
public interface IRegistroNotificaciones
{
    /// <returns>false si el mensaje ya fue enviado o está reservado por otra instancia.</returns>
    Task<bool> ReservarAsync(Guid messageId, CancellationToken ct);

    Task ConfirmarAsync(Guid messageId, CancellationToken ct);

    /// <summary>Libera una reserva no confirmada para que un reintento pueda enviar.</summary>
    Task LiberarAsync(Guid messageId, CancellationToken ct);
}
