using Auditoria.Application.Abstracciones;
using Auditoria.Domain.Registros;
using Microsoft.EntityFrameworkCore;

namespace Auditoria.Infrastructure.Persistencia;

internal sealed class AuditoriaRepository(AuditoriaDbContext db) : IAuditoriaRepository
{
    public async Task<long> ReservarIdAsync(CancellationToken ct) =>
        await db.Database
            .SqlQuery<long>($"SELECT nextval('auditoria.sq_registro_auditoria') AS \"Value\"")
            .SingleAsync(ct);

    public void Agregar(RegistroAuditoria registro) => db.Registros.Add(registro);
}
