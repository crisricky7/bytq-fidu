using Auditoria.Application.Abstracciones;
using Auditoria.Domain.Comun;
using Microsoft.Extensions.Options;

namespace Auditoria.Application.Consultar;

public sealed record ConsultarAuditoria(string? CodigoEmpresa, string? Usuario, string? Entidad, DateOnly? FechaDesde);

public sealed record RegistroAuditoriaDto(
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

/// <summary>Criterio ya normalizado para la capa de lectura.</summary>
public sealed record CriterioBusqueda(
    string CodigoEmpresa,
    string? Usuario,
    string? Entidad,
    DateTimeOffset? DesdeUtc,
    int MaximoRegistros);

public sealed class OpcionesConsulta
{
    public const string Seccion = "Auditoria:Consulta";

    /// <summary>
    /// Tope de filas por consulta. El contrato no tiene paginación; sin tope una consulta sin
    /// fecha devuelve la tabla completa.
    /// </summary>
    public int MaximoRegistros { get; set; } = 1000;

    /// <summary>Zona en la que los consumidores expresan fechaDesde (fecha sin hora).</summary>
    public string ZonaHoraria { get; set; } = "America/Guayaquil";
}

public sealed class ConsultarAuditoriaHandler(IAuditoriaLecturas lecturas, IOptions<OpcionesConsulta> opciones)
    : IQueryHandler<ConsultarAuditoria, Respuesta<IReadOnlyList<RegistroAuditoriaDto>>>
{
    public const string MensajeExito = "Consulta exitosa";

    public async Task<Respuesta<IReadOnlyList<RegistroAuditoriaDto>>> ManejarAsync(ConsultarAuditoria query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.CodigoEmpresa))
        {
            var error = ErroresAuditoria.EmpresaRequerida;
            return Respuesta<IReadOnlyList<RegistroAuditoriaDto>>.Error(error.Codigo, error.Mensaje);
        }

        var criterio = new CriterioBusqueda(
            query.CodigoEmpresa,
            string.IsNullOrWhiteSpace(query.Usuario) ? null : query.Usuario,
            string.IsNullOrWhiteSpace(query.Entidad) ? null : query.Entidad,
            query.FechaDesde is { } fecha ? InicioDelDiaEnUtc(fecha) : null,
            opciones.Value.MaximoRegistros);

        var registros = await lecturas.BuscarAsync(criterio, ct);
        return Respuesta<IReadOnlyList<RegistroAuditoriaDto>>.Ok(MensajeExito, registros);
    }

    private DateTimeOffset InicioDelDiaEnUtc(DateOnly fecha)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(opciones.Value.ZonaHoraria);
        var inicioLocal = fecha.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(inicioLocal, zona.GetUtcOffset(inicioLocal)).ToUniversalTime();
    }
}
