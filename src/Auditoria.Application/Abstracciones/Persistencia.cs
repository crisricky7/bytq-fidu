using Auditoria.Application.Consultar;
using Auditoria.Domain.Registros;

namespace Auditoria.Application.Abstracciones;

/// <summary>Lado de escritura. Solo agrega: el registro de auditoría no se modifica ni se borra.</summary>
public interface IAuditoriaRepository
{
    /// <summary>Reserva el siguiente id de la secuencia (equivalente a SQ01AUDITORIA.NEXTVAL).</summary>
    Task<long> ReservarIdAsync(CancellationToken ct);

    void Agregar(RegistroAuditoria registro);
}

/// <summary>
/// Confirma en una única transacción todo lo pendiente, incluidos los eventos de dominio que se
/// escriben en el outbox.
/// </summary>
public interface IUnitOfWork
{
    Task GuardarCambiosAsync(CancellationToken ct);
}

/// <summary>Lado de lectura: proyecciones directas, sin materializar el agregado.</summary>
public interface IAuditoriaLecturas
{
    Task<IReadOnlyList<RegistroAuditoriaDto>> BuscarAsync(CriterioBusqueda criterio, CancellationToken ct);
}
