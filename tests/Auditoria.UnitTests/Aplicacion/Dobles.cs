using Auditoria.Application.Abstracciones;
using Auditoria.Application.Consultar;
using Auditoria.Domain.Registros;

namespace Auditoria.UnitTests.Aplicacion;

internal sealed class RepositorioEnMemoria : IAuditoriaRepository, IUnitOfWork
{
    private long _secuencia = 100;
    private readonly List<RegistroAuditoria> _pendientes = [];

    public List<RegistroAuditoria> Confirmados { get; } = [];
    public int IdsReservados { get; private set; }
    public int Guardados { get; private set; }
    public Exception? FallarAlGuardar { get; set; }

    public Task<long> ReservarIdAsync(CancellationToken ct)
    {
        IdsReservados++;
        return Task.FromResult(++_secuencia);
    }

    public void Agregar(RegistroAuditoria registro) => _pendientes.Add(registro);

    public Task GuardarCambiosAsync(CancellationToken ct)
    {
        Guardados++;
        if (FallarAlGuardar is not null) throw FallarAlGuardar;
        Confirmados.AddRange(_pendientes);
        _pendientes.Clear();
        return Task.CompletedTask;
    }
}

internal sealed class LecturasEspia : IAuditoriaLecturas
{
    public CriterioBusqueda? UltimoCriterio { get; private set; }

    public Task<IReadOnlyList<RegistroAuditoriaDto>> BuscarAsync(CriterioBusqueda criterio, CancellationToken ct)
    {
        UltimoCriterio = criterio;
        return Task.FromResult<IReadOnlyList<RegistroAuditoriaDto>>([]);
    }
}
