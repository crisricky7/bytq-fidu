namespace Auditoria.Application.Abstracciones;

/// <summary>
/// Contratos mínimos de CQRS. Se usan en lugar de MediatR: con dos casos de uso el mediador no
/// aporta desacople real, agrega indirección y hoy tiene licencia comercial.
/// </summary>
public interface ICommandHandler<in TCommand, TResultado>
{
    Task<TResultado> ManejarAsync(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResultado>
{
    Task<TResultado> ManejarAsync(TQuery query, CancellationToken ct);
}
