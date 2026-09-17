namespace Auditoria.Api.Contrato;

// DTOs de auditoria-legacy-v1.yaml. Los nombres y la forma no se cambian aunque el contrato
// tenga defectos: hay consumidores en producción (ver DECISIONES.md).

public sealed record RegistroAuditoriaRequest(
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

/// <summary>Envoltorio estándar {codigo, mensaje, detalle, data}.</summary>
public sealed record RespuestaEstandar<T>(string Codigo, string Mensaje, string? Detalle, T? Data);

public sealed record RegistroAuditoriaResponse(
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
    DateTimeOffset FechaRegistro);

public static class CodigosRespuesta
{
    public const string PeticionInvalida = "E00";
    public const string BatchNoRegistraConsultas = "E10";
    public const string ErrorInterno = "E99";

    /// <summary>
    /// El sistema actual responde 400 solo para petición vacía (E00) y la regla BATCH+Q (E10);
    /// el resto de errores de negocio viajan con 200 y el código en el cuerpo.
    /// </summary>
    public static int EstadoHttp(string codigo) =>
        codigo is PeticionInvalida or BatchNoRegistraConsultas ? StatusCodes.Status400BadRequest : StatusCodes.Status200OK;
}
