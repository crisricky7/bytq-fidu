using Auditoria.Application.Registrar;
using Auditoria.UnitTests.Dominio;
using Microsoft.Extensions.Time.Testing;

namespace Auditoria.UnitTests.Aplicacion;

public class RegistrarAuditoriaHandlerTests
{
    private readonly RepositorioEnMemoria _repositorio = new();
    private readonly FakeTimeProvider _reloj = new(DatosPrueba.Ahora);

    private RegistrarAuditoriaHandler CrearHandler() => new(_repositorio, _repositorio, _reloj);

    [Fact]
    public async Task Registro_valido_se_confirma_con_fecha_del_reloj_y_devuelve_el_id()
    {
        var respuesta = await CrearHandler().ManejarAsync(new RegistrarAuditoria(DatosPrueba.Validos()), CancellationToken.None);

        Assert.Equal("OK", respuesta.Codigo);
        Assert.Equal("Registro creado.", respuesta.Mensaje);
        var registro = Assert.Single(_repositorio.Confirmados);
        Assert.Equal(registro.Id, respuesta.Data);
        Assert.Equal(DatosPrueba.Ahora, registro.FechaRegistro);
        Assert.Equal(1, _repositorio.Guardados);
    }

    [Fact]
    public async Task Peticion_invalida_no_reserva_id_ni_persiste()
    {
        var datos = DatosPrueba.Validos() with { Accion = "U", ValorAnterior = null };

        var respuesta = await CrearHandler().ManejarAsync(new RegistrarAuditoria(datos), CancellationToken.None);

        Assert.Equal("E04", respuesta.Codigo);
        Assert.Equal(0, _repositorio.IdsReservados);
        Assert.Equal(0, _repositorio.Guardados);
    }

    [Fact]
    public async Task Fallo_de_persistencia_no_se_reporta_como_exito()
    {
        _repositorio.FallarAlGuardar = new InvalidOperationException("BD caída");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearHandler().ManejarAsync(new RegistrarAuditoria(DatosPrueba.Validos()), CancellationToken.None));

        Assert.Empty(_repositorio.Confirmados);
    }
}
