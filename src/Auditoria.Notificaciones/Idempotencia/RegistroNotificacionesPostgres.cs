using Npgsql;

namespace Auditoria.Notificaciones.Idempotencia;

public sealed class RegistroNotificacionesPostgres(string cadenaConexion, TimeSpan vencimientoReserva) : IRegistroNotificaciones
{
    public async Task<bool> ReservarAsync(Guid messageId, CancellationToken ct)
    {
        // Inserta la reserva, o toma una abandonada (no enviada y vencida). La PK serializa la concurrencia.
        const string sql = """
            INSERT INTO notificaciones.notificacion_enviada (message_id, reservado_at)
            VALUES (@id, now())
            ON CONFLICT (message_id) DO UPDATE SET reservado_at = now()
             WHERE notificacion_enviada.enviado_at IS NULL
               AND notificacion_enviada.reservado_at <= now() - @vencimiento
            RETURNING message_id
            """;
        return await EjecutarAsync(sql, messageId, ct, c => c.Parameters.AddWithValue("vencimiento", vencimientoReserva)) is not null;
    }

    public Task ConfirmarAsync(Guid messageId, CancellationToken ct) =>
        EjecutarAsync("UPDATE notificaciones.notificacion_enviada SET enviado_at = now() WHERE message_id = @id", messageId, ct);

    public Task LiberarAsync(Guid messageId, CancellationToken ct) =>
        EjecutarAsync("DELETE FROM notificaciones.notificacion_enviada WHERE message_id = @id AND enviado_at IS NULL", messageId, ct);

    private async Task<object?> EjecutarAsync(string sql, Guid messageId, CancellationToken ct, Action<NpgsqlCommand>? parametros = null)
    {
        await using var conexion = new NpgsqlConnection(cadenaConexion);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("id", messageId);
        parametros?.Invoke(comando);
        return await comando.ExecuteScalarAsync(ct);
    }
}
