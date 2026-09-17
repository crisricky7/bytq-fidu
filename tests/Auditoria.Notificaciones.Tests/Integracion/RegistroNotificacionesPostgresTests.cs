using Auditoria.Notificaciones.Idempotencia;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Auditoria.Notificaciones.Tests.Integracion;

public sealed class RegistroNotificacionesPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "db", "002_notificaciones.sql"));
        await using var conexion = new NpgsqlConnection(_postgres.GetConnectionString());
        await conexion.OpenAsync();
        await new NpgsqlCommand(script, conexion).ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private RegistroNotificacionesPostgres Crear(TimeSpan? vencimiento = null) =>
        new(_postgres.GetConnectionString(), vencimiento ?? TimeSpan.FromMinutes(5));

    [Fact]
    public async Task Solo_una_reserva_gana_para_el_mismo_mensaje()
    {
        var id = Guid.NewGuid();
        var registro = Crear();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => registro.ReservarAsync(id, CancellationToken.None)));

        Assert.Equal(1, resultados.Count(r => r));
    }

    [Fact]
    public async Task Mensaje_confirmado_no_se_vuelve_a_reservar_aunque_venza_el_plazo()
    {
        var id = Guid.NewGuid();
        var registro = Crear(TimeSpan.Zero);

        Assert.True(await registro.ReservarAsync(id, CancellationToken.None));
        await registro.ConfirmarAsync(id, CancellationToken.None);

        Assert.False(await registro.ReservarAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Reserva_liberada_permite_reintentar()
    {
        var id = Guid.NewGuid();
        var registro = Crear();

        await registro.ReservarAsync(id, CancellationToken.None);
        await registro.LiberarAsync(id, CancellationToken.None);

        Assert.True(await registro.ReservarAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Reserva_abandonada_por_un_proceso_caido_se_recupera_al_vencer()
    {
        var id = Guid.NewGuid();

        Assert.True(await Crear().ReservarAsync(id, CancellationToken.None));
        Assert.False(await Crear().ReservarAsync(id, CancellationToken.None));
        Assert.True(await Crear(TimeSpan.Zero).ReservarAsync(id, CancellationToken.None));
    }
}
