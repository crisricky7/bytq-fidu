using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Auditoria.IntegrationTests;

[Collection(ColeccionIntegracion.Nombre)]
public class ConsultaAuditoriaEndpointTests(EntornoPruebas entorno)
{
    [Fact]
    public async Task Consulta_filtra_por_entidad_sin_distinguir_mayusculas_y_respeta_el_contrato()
    {
        var empresa = "C" + Random.Shared.Next(100, 999);
        var cliente = entorno.CrearCliente("audit.write", "audit.read");
        await cliente.PostAsJsonAsync("/api/auditoria/registro", new
        {
            codigoEmpresa = empresa, usuario = "mlopez", entidad = "SolicitudRescate", claveEntidad = "RES-9", accion = "C"
        });
        await cliente.PostAsJsonAsync("/api/auditoria/registro", new
        {
            codigoEmpresa = empresa, usuario = "mlopez", entidad = "Cliente", claveEntidad = "CLI-9", accion = "D"
        });

        var respuesta = await cliente.GetAsync($"/api/auditoria/consulta?codigoEmpresa={empresa}&entidad=rescate");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OK", cuerpo.GetProperty("codigo").GetString());
        var registro = Assert.Single(cuerpo.GetProperty("data").EnumerateArray());
        string[] camposContrato =
        [
            "id", "codigoEmpresa", "codigoModulo", "codigoTransaccion", "usuario", "entidad", "claveEntidad",
            "accion", "valorAnterior", "valorNuevo", "direccionIp", "canal", "fechaRegistro"
        ];
        Assert.Equal(camposContrato, registro.EnumerateObject().Select(p => p.Name));
        Assert.Equal("RES-9", registro.GetProperty("claveEntidad").GetString());
        Assert.Equal("WEB", registro.GetProperty("canal").GetString());
    }

    [Theory]
    [InlineData("usuario=x'%20OR%20'1'='1")]
    [InlineData("entidad=%25")]
    public async Task Filtros_con_comillas_o_comodines_no_alteran_la_consulta(string filtro)
    {
        var empresa = "I" + Random.Shared.Next(100, 999);
        var cliente = entorno.CrearCliente("audit.write", "audit.read");
        await cliente.PostAsJsonAsync("/api/auditoria/registro", new
        {
            codigoEmpresa = empresa, usuario = "jperez", entidad = "Cliente", claveEntidad = "CLI-1", accion = "C"
        });

        var cuerpo = await cliente.GetFromJsonAsync<JsonElement>($"/api/auditoria/consulta?codigoEmpresa={empresa}&{filtro}");

        Assert.Equal("OK", cuerpo.GetProperty("codigo").GetString());
        Assert.Empty(cuerpo.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Sin_codigo_de_empresa_responde_E01()
    {
        var respuesta = await entorno.CrearCliente("audit.read").GetAsync("/api/auditoria/consulta");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E01", cuerpo.GetProperty("codigo").GetString());
    }
}
