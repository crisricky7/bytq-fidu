using System.Diagnostics;
using System.Text.Json;
using Auditoria.Application.Abstracciones;
using Auditoria.Domain.Comun;
using Auditoria.Domain.Registros;
using Auditoria.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Auditoria.Infrastructure.Persistencia;

public sealed class AuditoriaDbContext(DbContextOptions<AuditoriaDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Esquema = "auditoria";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public DbSet<RegistroAuditoria> Registros => Set<RegistroAuditoria>();
    public DbSet<MensajeOutbox> Outbox => Set<MensajeOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditoriaDbContext).Assembly);
    }

    /// <summary>
    /// Convierte los eventos de dominio pendientes en filas del outbox y confirma todo con un solo
    /// SaveChanges, es decir, en una sola transacción de base de datos.
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken ct)
    {
        var entidades = ChangeTracker.Entries<ITieneEventosDominio>()
            .Select(e => e.Entity)
            .Where(e => e.EventosDominio.Count > 0)
            .ToList();

        foreach (var evento in entidades.SelectMany(e => e.EventosDominio))
        {
            Outbox.Add(new MensajeOutbox
            {
                Id = Guid.CreateVersion7(),
                Tipo = evento.GetType().Name,
                Payload = JsonSerializer.Serialize(evento, evento.GetType(), OpcionesJson),
                OccurredAt = evento.OcurridoEn,
                NextAttemptAt = evento.OcurridoEn,
                TraceParent = Activity.Current?.Id
            });
        }

        await SaveChangesAsync(ct);

        foreach (var entidad in entidades)
            entidad.LimpiarEventosDominio();
    }
}
