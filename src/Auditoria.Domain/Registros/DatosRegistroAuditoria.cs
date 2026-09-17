namespace Auditoria.Domain.Registros;

/// <summary>Datos crudos tal como llegan del canal; el agregado decide si son válidos.</summary>
public sealed record DatosRegistroAuditoria(
    string? CodigoEmpresa,
    string? CodigoModulo,
    string? CodigoTransaccion,
    string? Usuario,
    string? Entidad,
    string? ClaveEntidad,
    string? Accion,
    string? ValorAnterior,
    string? ValorNuevo,
    string? DireccionIp,
    string? Canal);
