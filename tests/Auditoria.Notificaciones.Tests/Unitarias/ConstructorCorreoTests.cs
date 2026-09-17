using Auditoria.Notificaciones.Correo;

namespace Auditoria.Notificaciones.Tests.Unitarias;

public class ConstructorCorreoTests
{
    private readonly OpcionesNotificaciones _opciones = new() { Destinatario = "seguridad@corefid.com.ec" };

    [Fact]
    public void Incluye_los_datos_del_evento_y_va_al_buzon_de_seguridad()
    {
        var correo = ConstructorCorreo.Construir(Eventos.Sensible(), _opciones);

        Assert.Equal("seguridad@corefid.com.ec", correo.Para);
        Assert.Equal("Acción sensible: SolicitudRescate / D", correo.Asunto);
        Assert.Contains("jperez", correo.CuerpoHtml);
        Assert.Contains("RES-2026-004871", correo.CuerpoHtml);
        Assert.Contains("884213", correo.CuerpoHtml);
    }

    [Fact]
    public void Fecha_se_muestra_en_hora_de_ecuador()
    {
        var correo = ConstructorCorreo.Construir(Eventos.Sensible(), _opciones);

        Assert.Contains("16/09/2026 22:30:00", correo.CuerpoHtml);
    }

    [Fact]
    public void Valores_del_evento_se_escapan_como_html()
    {
        var evento = Eventos.Sensible(valorNuevo: "<script>alert(1)</script>") with { Entidad = "<b>Rol</b>" };

        var correo = ConstructorCorreo.Construir(evento, _opciones);

        Assert.DoesNotContain("<script>", correo.CuerpoHtml);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", correo.CuerpoHtml);
        Assert.DoesNotContain("<b>Rol</b>", correo.CuerpoHtml);
    }
}
