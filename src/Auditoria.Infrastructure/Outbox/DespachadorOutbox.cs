using System.Diagnostics;
using Auditoria.Infrastructure.Mensajeria;
using Auditoria.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Auditoria.Infrastructure.Outbox;

/// <summary>
/// Publica los mensajes pendientes del outbox. Entrega at-least-once: si el broker confirma pero
/// la marca de publicado no llega a guardarse, el mensaje se vuelve a enviar; los consumidores
/// deben deduplicar por MessageId (el id del outbox).
/// </summary>
public sealed class DespachadorOutbox(
    IServiceScopeFactory scopes,
    IPublicadorEventos publicador,
    IOptions<OpcionesOutbox> opciones,
    TimeProvider reloj,
    ILogger<DespachadorOutbox> logger) : BackgroundService
{
    private readonly OpcionesOutbox _opciones = opciones.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.Habilitado)
        {
            logger.LogInformation("Despachador de outbox deshabilitado por configuración");
            return;
        }

        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(_opciones.IntervaloSegundos), reloj);
        do
        {
            try
            {
                // Si el lote vino lleno probablemente hay más pendientes: seguir sin esperar.
                while (await ProcesarLoteAsync(stoppingToken) == _opciones.TamanoLote) { }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Base de datos caída u otro error transitorio: el servicio sigue vivo y reintenta.
                logger.LogError(ex, "Error procesando el outbox; se reintenta en el próximo ciclo");
            }
        }
        while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }

    /// <returns>Cantidad de mensajes tomados del outbox en este lote.</returns>
    public async Task<int> ProcesarLoteAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditoriaDbContext>();

        // La estrategia de reintentos de Npgsql exige ejecutar la transacción como unidad reintentable.
        // Si se reintenta tras publicar, el mensaje puede salir dos veces: cubierto por at-least-once.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(ct, c => ProcesarLoteEnTransaccionAsync(db, c));
    }

    private async Task<int> ProcesarLoteEnTransaccionAsync(AuditoriaDbContext db, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var ahora = reloj.GetUtcNow();

        // SKIP LOCKED permite varias réplicas del servicio sin publicar dos veces el mismo mensaje.
        var lote = await db.Outbox.FromSql($"""
            SELECT * FROM auditoria.outbox_mensaje
             WHERE published_at IS NULL AND next_attempt_at <= {ahora}
             ORDER BY occurred_at
             LIMIT {_opciones.TamanoLote}
             FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);

        foreach (var mensaje in lote)
        {
            using var actividad = Telemetria.Fuente.StartActivity(
                $"publicar {mensaje.Tipo}", ActivityKind.Producer, mensaje.TraceParent);
            try
            {
                await publicador.PublicarAsync(mensaje, ct);
                mensaje.PublishedAt = reloj.GetUtcNow();
                mensaje.LastError = null;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                mensaje.Attempts++;
                mensaje.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                mensaje.NextAttemptAt = ahora.Add(CalcularBackoff(mensaje.Attempts));
                actividad?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogWarning(ex, "No se pudo publicar {MensajeId} ({Tipo}), intento {Intento}; próximo intento {ProximoIntento}",
                    mensaje.Id, mensaje.Tipo, mensaje.Attempts, mensaje.NextAttemptAt);
                // Con el broker caído el resto del lote fallaría igual; se corta aquí.
                break;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return lote.Count;
    }

    private TimeSpan CalcularBackoff(int intentos) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Min(intentos, 20)), _opciones.MaximoBackoffSegundos));
}
