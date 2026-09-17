using Auditoria.Application.Abstracciones;
using Auditoria.Application.Consultar;
using Microsoft.EntityFrameworkCore;

namespace Auditoria.Infrastructure.Persistencia;

/// <summary>
/// Lado de lectura: SQL parametrizado proyectado directo a DTO, sin tracking ni agregado.
/// Nada de lo que envía el cliente se concatena en la sentencia.
/// </summary>
internal sealed class AuditoriaLecturas(AuditoriaDbContext db) : IAuditoriaLecturas
{
    public async Task<IReadOnlyList<RegistroAuditoriaDto>> BuscarAsync(CriterioBusqueda criterio, CancellationToken ct)
    {
        // Mismo comportamiento que el LIKE '%x%' sin distinguir mayúsculas, pero escapando comodines.
        var patronEntidad = criterio.Entidad is null ? null : "%" + EscaparLike(criterio.Entidad) + "%";

        var filas = await db.Database.SqlQuery<FilaRegistro>($"""
            SELECT id, codigo_empresa, codigo_modulo, codigo_transaccion, usuario, entidad, clave_entidad,
                   accion, valor_anterior, valor_nuevo, direccion_ip, canal, fecha_registro
              FROM auditoria.registro_auditoria
             WHERE codigo_empresa = {criterio.CodigoEmpresa}
               AND ({criterio.Usuario}::varchar IS NULL OR usuario = {criterio.Usuario})
               AND ({patronEntidad}::varchar IS NULL OR entidad ILIKE {patronEntidad} ESCAPE '\')
               AND ({criterio.DesdeUtc}::timestamptz IS NULL OR fecha_registro >= {criterio.DesdeUtc})
             ORDER BY fecha_registro DESC, id DESC
             LIMIT {criterio.MaximoRegistros}
            """).ToListAsync(ct);

        return filas.ConvertAll(f => new RegistroAuditoriaDto(
            f.id, f.codigo_empresa, f.codigo_modulo, f.codigo_transaccion, f.usuario, f.entidad,
            f.clave_entidad, f.accion, f.valor_anterior, f.valor_nuevo, f.direccion_ip, f.canal,
            f.fecha_registro));
    }

    private static string EscaparLike(string valor) =>
        valor.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    // Nombres iguales a las columnas para mapear SqlQuery sin configuración adicional.
    private sealed record FilaRegistro(
        long id, string codigo_empresa, string? codigo_modulo, string? codigo_transaccion, string usuario,
        string? entidad, string clave_entidad, string accion, string? valor_anterior, string? valor_nuevo,
        string? direccion_ip, string canal, DateTimeOffset fecha_registro);
}
