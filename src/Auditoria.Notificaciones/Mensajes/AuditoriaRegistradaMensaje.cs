namespace Auditoria.Notificaciones.Mensajes;

/// <summary>
/// Contrato del evento publicado por el servicio de Auditoría (routing key auditoria.registrada).
/// Se define aquí y no se referencia el dominio: el consumidor depende del mensaje, no del productor.
/// </summary>
public sealed record AuditoriaRegistradaMensaje(
    long Id,
    string CodigoEmpresa,
    string? CodigoModulo,
    string? CodigoTransaccion,
    string Usuario,
    string? Entidad,
    string ClaveEntidad,
    string Accion,
    string? ValorAnterior,
    string? ValorNuevo,
    string? DireccionIp,
    string Canal,
    bool EsAccionSensible,
    DateTimeOffset OcurridoEn);

/// <summary>Mensaje tal como llega del broker.</summary>
public sealed record MensajeEntrante(Guid MessageId, ReadOnlyMemory<byte> Cuerpo, string? TraceParent);
