using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Auditoria.IntegrationTests;

[Collection(ColeccionIntegracion.Nombre)]
public class RegistroAuditoriaEndpointTests(EntornoPruebas entorno)
{
    private const string Ruta = "/api/auditoria/registro";

    private static object Peticion(string clave, string accion = "C", string? canal = "API", string? valorAnterior = null) => new
    {
        codigoEmpresa = "0001",
        codigoModulo = "06",
        codigoTransaccion = "V062014",
        usuario = "jperez",
        entidad = "SolicitudRescate",
        claveEntidad = clave,
        accion,
        valorAnterior,
        valorNuevo = "{\"estado\":\"APROBADA\"}",
        direccionIp = "10.0.4.77",
        canal
    };

    [Fact]
    public async Task Registro_valido_responde_el_contrato_y_escribe_registro_y_outbox()
    {
        var clave = "RES-" + Guid.NewGuid().ToString("N")[..12];

        var respuesta = await entorno.CrearCliente("audit.write").PostAsJsonAsync(Ruta, Peticion(clave));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.Contains("X-Trace-Id"));

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var raiz = json.RootElement;
        Assert.Equal(["codigo", "mensaje", "detalle", "data"], raiz.EnumerateObject().Select(p => p.Name));
        Assert.Equal("OK", raiz.GetProperty("codigo").GetString());
        Assert.Equal("Registro creado.", raiz.GetProperty("mensaje").GetString());
        Assert.Equal(JsonValueKind.Null, raiz.GetProperty("detalle").ValueKind);
        var id = raiz.GetProperty("data").GetInt64();

        Assert.Equal(1L, await entorno.EscalarAsync<long>(
            "SELECT count(*) FROM auditoria.registro_auditoria WHERE id = $1 AND clave_entidad = $2", id, clave));

        var payload = await entorno.EscalarAsync<string>(
            "SELECT payload::text FROM auditoria.outbox_mensaje WHERE tipo = 'AuditoriaRegistrada' AND (payload->>'id')::bigint = $1", id);
        using var evento = JsonDocument.Parse(payload);
        Assert.True(evento.RootElement.GetProperty("esAccionSensible").GetBoolean()); // módulo 06
    }

    [Fact]
    public async Task Error_de_negocio_viaja_con_200_y_no_persiste_nada()
    {
        var clave = "RES-" + Guid.NewGuid().ToString("N")[..12];

        var respuesta = await entorno.CrearCliente("audit.write").PostAsJsonAsync(Ruta, Peticion(clave, accion: "U"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E04", cuerpo.GetProperty("codigo").GetString());
        Assert.Equal(0L, await entorno.EscalarAsync<long>(
            "SELECT count(*) FROM auditoria.registro_auditoria WHERE clave_entidad = $1", clave));
    }

    [Fact]
    public async Task Batch_con_consulta_responde_400_E10()
    {
        var respuesta = await entorno.CrearCliente("audit.write").PostAsJsonAsync(Ruta, Peticion("RES-1", accion: "Q", canal: "BATCH"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E10", cuerpo.GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ esto no es json")]
    public async Task Cuerpo_vacio_o_mal_formado_responde_400_E00(string cuerpo)
    {
        var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");

        var respuesta = await entorno.CrearCliente("audit.write").PostAsync(Ruta, contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E00", json.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Si_falla_la_escritura_del_outbox_no_queda_el_registro_ni_se_responde_OK()
    {
        // Fuerza un fallo solo en la tabla outbox para comprobar que ambas escrituras son atómicas.
        await entorno.EjecutarSqlAsync("""
            CREATE OR REPLACE FUNCTION auditoria.fn_prueba_falla_outbox() RETURNS trigger AS $$
            BEGIN
                IF NEW.payload->>'claveEntidad' = 'FALLA-OUTBOX' THEN
                    RAISE EXCEPTION 'fallo simulado del outbox';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            DROP TRIGGER IF EXISTS tg_prueba_falla_outbox ON auditoria.outbox_mensaje;
            CREATE TRIGGER tg_prueba_falla_outbox BEFORE INSERT ON auditoria.outbox_mensaje
                FOR EACH ROW EXECUTE FUNCTION auditoria.fn_prueba_falla_outbox();
            """);

        var respuesta = await entorno.CrearCliente("audit.write").PostAsJsonAsync(Ruta, Peticion("FALLA-OUTBOX"));

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E99", cuerpo.GetProperty("codigo").GetString());
        Assert.StartsWith("traceId:", cuerpo.GetProperty("detalle").GetString());
        Assert.Equal(0L, await entorno.EscalarAsync<long>(
            "SELECT count(*) FROM auditoria.registro_auditoria WHERE clave_entidad = 'FALLA-OUTBOX'"));
    }
}
