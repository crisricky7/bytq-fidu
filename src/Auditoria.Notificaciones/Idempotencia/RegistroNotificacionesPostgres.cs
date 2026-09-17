namespace Auditoria.Notificaciones.Idempotencia;

public sealed class RegistroNotificacionesPostgres(string cadenaConexion, TimeSpan vencimientoReserva) : IRegistroNotificaciones
{
    public Task<bool> ReservarAsync(Guid messageId, CancellationToken ct) => throw new NotImplementedException();
    public Task ConfirmarAsync(Guid messageId, CancellationToken ct) => throw new NotImplementedException();
    public Task LiberarAsync(Guid messageId, CancellationToken ct) => throw new NotImplementedException();
}
