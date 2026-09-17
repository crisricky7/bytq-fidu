namespace Auditoria.Domain.Comun;

/// <summary>
/// Error de negocio con el código que exige el contrato vigente (E01..E05, E10).
/// </summary>
public sealed record ErrorDominio(string Codigo, string Mensaje);

public static class ErroresAuditoria
{
    public static readonly ErrorDominio EmpresaRequerida = new("E01", "Código de empresa requerido.");
    public static readonly ErrorDominio UsuarioRequerido = new("E02", "Usuario requerido.");
    public static readonly ErrorDominio AccionInvalida = new("E03", "Acción inválida.");
    public static readonly ErrorDominio ActualizacionSinValorAnterior = new("E04", "Una actualización requiere valor anterior.");
    public static readonly ErrorDominio ClaveEntidadRequerida = new("E05", "Clave de entidad requerida.");
    public static readonly ErrorDominio BatchNoRegistraConsultas = new("E10", "El canal BATCH no registra consultas.");

    /// <summary>
    /// Formato inválido (longitudes, canal fuera del catálogo, módulo no numérico).
    /// El contrato no define código para esto; se usa el mismo E00 que el sistema actual
    /// devuelve para peticiones inválidas, siempre con HTTP 400.
    /// </summary>
    public static ErrorDominio FormatoInvalido(string detalle) => new("E00", $"Petición inválida: {detalle}");
}
