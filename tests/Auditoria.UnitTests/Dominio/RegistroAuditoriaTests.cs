using Auditoria.Domain.Registros;

namespace Auditoria.UnitTests.Dominio;

public class RegistroAuditoriaTests
{
    [Fact]
    public void Crear_con_datos_validos_genera_registro_y_evento()
    {
        var resultado = RegistroAuditoria.Crear(42, DatosPrueba.Validos(), DatosPrueba.Ahora);

        Assert.True(resultado.EsExitoso);
        var registro = resultado.Valor;
        Assert.Equal(42, registro.Id);
        Assert.Equal(Accion.Crear, registro.Accion);
        Assert.Equal(Canal.Api, registro.Canal);
        Assert.Equal(DatosPrueba.Ahora, registro.FechaRegistro);

        var evento = Assert.IsType<AuditoriaRegistrada>(Assert.Single(registro.EventosDominio));
        Assert.Equal(42, evento.Id);
        Assert.Equal("RES-2026-004871", evento.ClaveEntidad);
    }

    [Theory]
    [InlineData(null, "E01")]
    [InlineData("", "E01")]
    [InlineData("   ", "E01")]
    public void Empresa_es_obligatoria(string? empresa, string codigo) =>
        AssertError(DatosPrueba.Validos() with { CodigoEmpresa = empresa }, codigo);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Usuario_es_obligatorio(string? usuario) =>
        AssertError(DatosPrueba.Validos() with { Usuario = usuario }, "E02");

    [Theory]
    [InlineData(null)]
    [InlineData("X")]
    [InlineData("u")]
    [InlineData("CU")]
    public void Accion_fuera_del_catalogo_es_invalida(string? accion) =>
        AssertError(DatosPrueba.Validos() with { Accion = accion }, "E03");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Actualizacion_requiere_valor_anterior(string? valorAnterior) =>
        AssertError(DatosPrueba.Validos() with { Accion = "U", ValorAnterior = valorAnterior }, "E04");

    [Fact]
    public void Actualizacion_con_valor_anterior_es_valida()
    {
        var datos = DatosPrueba.Validos() with { Accion = "U", ValorAnterior = "{\"monto\":50}" };

        Assert.Null(RegistroAuditoria.Validar(datos));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Clave_de_entidad_es_obligatoria(string? clave) =>
        AssertError(DatosPrueba.Validos() with { ClaveEntidad = clave }, "E05");

    [Fact]
    public void Canal_batch_no_registra_consultas() =>
        AssertError(DatosPrueba.Validos() with { Canal = "BATCH", Accion = "Q" }, "E10");

    [Fact]
    public void Regla_batch_consulta_se_evalua_antes_que_las_demas()
    {
        // Mismo orden que el sistema actual: la regla del canal se revisaba antes que el resto.
        var datos = DatosPrueba.Validos() with { Canal = "BATCH", Accion = "Q", CodigoEmpresa = null };

        AssertError(datos, "E10");
    }

    [Fact]
    public void Primer_error_gana_cuando_hay_varios()
    {
        var datos = DatosPrueba.Validos() with { Usuario = null, ClaveEntidad = null };

        AssertError(datos, "E02");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_canal_se_asume_web(string? canal)
    {
        var registro = RegistroAuditoria.Crear(1, DatosPrueba.Validos() with { Canal = canal }, DatosPrueba.Ahora).Valor;

        Assert.Equal(Canal.Web, registro.Canal);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("MOVIL")]
    public void Canal_fuera_del_catalogo_es_formato_invalido(string canal) =>
        AssertError(DatosPrueba.Validos() with { Canal = canal }, "E00");

    [Theory]
    [InlineData("6A")]
    [InlineData("123")]
    public void Codigo_modulo_debe_ser_numerico_de_dos_digitos(string modulo) =>
        AssertError(DatosPrueba.Validos() with { CodigoModulo = modulo }, "E00");

    [Fact]
    public void Respeta_longitudes_maximas_del_contrato()
    {
        AssertError(DatosPrueba.Validos() with { CodigoEmpresa = "00001" }, "E00");
        AssertError(DatosPrueba.Validos() with { Usuario = new string('a', 31) }, "E00");
        AssertError(DatosPrueba.Validos() with { ClaveEntidad = new string('a', 61) }, "E00");
    }

    [Fact]
    public void Texto_con_comillas_se_conserva_literal()
    {
        const string malicioso = "x' OR '1'='1";

        var registro = RegistroAuditoria.Crear(1, DatosPrueba.Validos() with { Entidad = malicioso }, DatosPrueba.Ahora).Valor;

        Assert.Equal(malicioso, registro.Entidad);
    }

    [Fact]
    public void Fecha_de_registro_debe_estar_en_utc()
    {
        var local = new DateTimeOffset(2026, 9, 16, 17, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<ArgumentException>(() => RegistroAuditoria.Crear(1, DatosPrueba.Validos(), local));
    }

    [Fact]
    public void Id_debe_venir_de_la_secuencia() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RegistroAuditoria.Crear(0, DatosPrueba.Validos(), DatosPrueba.Ahora));

    [Theory]
    [InlineData("D", "03", "SolicitudRescate", true)]
    [InlineData("C", "06", "SolicitudRescate", true)]
    [InlineData("U", "03", "Usuario", true)]
    [InlineData("C", "03", "Rol", true)]
    [InlineData("C", "03", "SolicitudRescate", false)]
    [InlineData("Q", null, "Cliente", false)]
    public void Identifica_acciones_sensibles(string accion, string? modulo, string entidad, bool esperado)
    {
        var datos = DatosPrueba.Validos() with
        {
            Accion = accion, CodigoModulo = modulo, Entidad = entidad, ValorAnterior = "{}"
        };

        var registro = RegistroAuditoria.Crear(1, datos, DatosPrueba.Ahora).Valor;

        Assert.Equal(esperado, registro.EsAccionSensible);
        Assert.Equal(esperado, ((AuditoriaRegistrada)registro.EventosDominio.Single()).EsAccionSensible);
    }

    private static void AssertError(DatosRegistroAuditoria datos, string codigoEsperado)
    {
        var resultado = RegistroAuditoria.Crear(1, datos, DatosPrueba.Ahora);

        Assert.False(resultado.EsExitoso);
        Assert.Equal(codigoEsperado, resultado.Error!.Codigo);
    }
}
