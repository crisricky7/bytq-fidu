using Auditoria.Domain.Comun;

namespace Auditoria.Domain.Registros;

/// <summary>
/// Se emite cuando un registro de auditoría queda persistido. Lleva el snapshot completo para
/// que los consumidores (p. ej. la notificación a Seguridad) no tengan que volver a consultar.
/// </summary>
public sealed record AuditoriaRegistrada(
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
    DateTimeOffset OcurridoEn) : IEventoDominio;
