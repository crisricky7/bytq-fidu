using System.Net.Http.Json;
using System.Text.Json;
using Auditoria.Infrastructure.Mensajeria;
using Auditoria.Infrastructure.Outbox;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Auditoria.IntegrationTests;

[Collection(ColeccionIntegracion.Nombre)]
public class DespachadorOutboxTests(EntornoPruebas entorno)
{
    private sealed class PublicadorControlable : IPublicadorEventos
    {
        public bool BrokerCaido { get; set; }
        public List<Guid> Publicados { get; } = [];

        public Task PublicarAsync(MensajeOutbox mensaje, CancellationToken ct)
        {
            if (BrokerCaido) throw new InvalidOperationException("broker no disponible");
            Publicados.Add(mensaje.Id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Con_broker_caido_reintenta_con_backoff_y_al_volver_publica_una_sola_vez()
    {
        var publicador = new PublicadorControlable { BrokerCaido = true };
        using var app = entorno.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IPublicadorEventos>(publicador)));

        // Aísla la prueba de los mensajes generados por otras pruebas.
        await entorno.EjecutarSqlAsync("UPDATE auditoria.outbox_mensaje SET published_at = now() WHERE published_at IS NULL");

        var respuesta = await app.CreateClient().WithToken("audit.write").PostAsJsonAsync("/api/auditoria/registro", new
        {
            codigoEmpresa = "0001", usuario = "jperez", entidad = "Rol", claveEntidad = "ROL-7", accion = "C"
        });
        var id = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetInt64();
        var despachador = app.Services.GetRequiredService<DespachadorOutbox>();

        await despachador.ProcesarLoteAsync(CancellationToken.None);

        const string sqlEstado = "SELECT attempts FROM auditoria.outbox_mensaje WHERE (payload->>'id')::bigint = $1 AND published_at IS NULL";
        Assert.Equal(1, await entorno.EscalarAsync<int>(sqlEstado, id));
        Assert.Empty(publicador.Publicados);

        // El broker vuelve; se adelanta el próximo intento para no esperar el backoff real.
        publicador.BrokerCaido = false;
        await entorno.EjecutarSqlAsync("UPDATE auditoria.outbox_mensaje SET next_attempt_at = now() WHERE published_at IS NULL");
        await despachador.ProcesarLoteAsync(CancellationToken.None);
        await despachador.ProcesarLoteAsync(CancellationToken.None);

        Assert.Single(publicador.Publicados);
        Assert.Equal(0L, await entorno.EscalarAsync<long>(
            "SELECT count(*) FROM auditoria.outbox_mensaje WHERE published_at IS NULL"));
    }
}

internal static class HttpClientExtensiones
{
    public static HttpClient WithToken(this HttpClient cliente, params string[] roles)
    {
        cliente.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", EntornoPruebas.CrearToken(roles));
        return cliente;
    }
}
