using Auditoria.Application.Consultar;
using Microsoft.Extensions.Options;

namespace Auditoria.UnitTests.Aplicacion;

public class ConsultarAuditoriaHandlerTests
{
    private readonly LecturasEspia _lecturas = new();
    private readonly OpcionesConsulta _opciones = new() { MaximoRegistros = 50, ZonaHoraria = "America/Guayaquil" };

    private ConsultarAuditoriaHandler CrearHandler() => new(_lecturas, Options.Create(_opciones));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Empresa_es_obligatoria(string? empresa)
    {
        var respuesta = await CrearHandler().ManejarAsync(new ConsultarAuditoria(empresa, null, null, null), CancellationToken.None);

        Assert.Equal("E01", respuesta.Codigo);
        Assert.Null(_lecturas.UltimoCriterio);
    }

    [Fact]
    public async Task Fecha_desde_se_interpreta_en_hora_de_ecuador_y_se_aplica_el_tope()
    {
        var query = new ConsultarAuditoria("0001", "jperez", " ", new DateOnly(2026, 9, 1));

        var respuesta = await CrearHandler().ManejarAsync(query, CancellationToken.None);

        Assert.Equal("OK", respuesta.Codigo);
        var criterio = _lecturas.UltimoCriterio!;
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 5, 0, 0, TimeSpan.Zero), criterio.DesdeUtc);
        Assert.Null(criterio.Entidad);
        Assert.Equal(50, criterio.MaximoRegistros);
    }
}
