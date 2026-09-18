using System.Globalization;
using System.Net;
using System.Text;
using Auditoria.Notificaciones.Mensajes;

namespace Auditoria.Notificaciones.Correo;

public static class ConstructorCorreo
{
    public static CorreoSaliente Construir(AuditoriaRegistradaMensaje evento, OpcionesNotificaciones opciones)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(opciones.ZonaHoraria);
        var fechaLocal = TimeZoneInfo.ConvertTime(evento.OcurridoEn, zona);

        // Todo valor que viene del evento se codifica: los snapshots los escribe el usuario final.
        var cuerpo = new StringBuilder()
            .Append("<p>Se registró una acción sensible en CORE-FID.</p><table>")
            .Append(Fila("Registro", evento.Id.ToString(CultureInfo.InvariantCulture)))
            .Append(Fila("Empresa", evento.CodigoEmpresa))
            .Append(Fila("Módulo", evento.CodigoModulo))
            .Append(Fila("Usuario", evento.Usuario))
            .Append(Fila("Entidad", evento.Entidad))
            .Append(Fila("Clave", evento.ClaveEntidad))
            .Append(Fila("Acción", evento.Accion))
            .Append(Fila("Canal", evento.Canal))
            .Append(Fila("IP", evento.DireccionIp))
            .Append(Fila("Fecha", fechaLocal.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)))
            .Append(Fila("Valor anterior", evento.ValorAnterior))
            .Append(Fila("Valor nuevo", evento.ValorNuevo))
            .Append("</table>")
            .ToString();

        var asunto = $"Acción sensible: {evento.Entidad} / {evento.Accion}".ReplaceLineEndings(" ");
        return new CorreoSaliente(opciones.Destinatario, asunto, cuerpo);
    }

    private static string Fila(string etiqueta, string? valor) =>
        $"<tr><th align=\"left\">{etiqueta}</th><td>{WebUtility.HtmlEncode(valor ?? string.Empty)}</td></tr>";
}
