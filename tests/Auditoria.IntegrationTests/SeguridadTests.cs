using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Auditoria.IntegrationTests;

[Collection(ColeccionIntegracion.Nombre)]
public class SeguridadTests(EntornoPruebas entorno)
{
    private static readonly object Peticion = new
    {
        codigoEmpresa = "0001", usuario = "jperez", entidad = "Cliente", claveEntidad = "CLI-1", accion = "C"
    };

    public static TheoryData<string, string> TokensInvalidos() => new()
    {
        { "sin token", "" },
        { "issuer ajeno", EntornoPruebas.CrearToken(["audit.write"], issuer: "https://otro-issuer/") },
        { "audience ajena", EntornoPruebas.CrearToken(["audit.write"], audience: "api://otra-api") },
        { "expirado", EntornoPruebas.CrearToken(["audit.write"], expira: DateTime.UtcNow.AddMinutes(-5)) },
        { "firma con otra clave", EntornoPruebas.CrearToken(["audit.write"], clave: new string('x', 64)) }
    };

    [Theory]
    [MemberData(nameof(TokensInvalidos))]
    public async Task Token_invalido_responde_401(string caso, string token)
    {
        var cliente = entorno.CreateClient();
        if (token.Length > 0)
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await cliente.PostAsJsonAsync("/api/auditoria/registro", Peticion);

        Assert.True(respuesta.StatusCode == HttpStatusCode.Unauthorized, caso);
    }

    [Fact]
    public async Task Sin_rol_de_escritura_responde_403()
    {
        var respuesta = await entorno.CrearCliente("audit.read").PostAsJsonAsync("/api/auditoria/registro", Peticion);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    public async Task Health_checks_son_anonimos_y_responden_200(string ruta)
    {
        var respuesta = await entorno.CreateClient().GetAsync(ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
