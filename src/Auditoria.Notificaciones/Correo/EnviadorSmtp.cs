using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Auditoria.Notificaciones.Correo;

public sealed class OpcionesSmtp
{
    public const string Seccion = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Puerto { get; set; } = 25;
    public string? Usuario { get; set; }
    public string? Clave { get; set; }
    public bool UsarTls { get; set; } = true;
    public string Remitente { get; set; } = "auditoria@corefid.com.ec";
}

public sealed class EnviadorSmtp(IOptions<OpcionesSmtp> opciones) : IEnviadorCorreo
{
    public async Task EnviarAsync(CorreoSaliente correo, CancellationToken ct)
    {
        var configuracion = opciones.Value;
        var mensaje = new MimeMessage();
        mensaje.From.Add(MailboxAddress.Parse(configuracion.Remitente));
        mensaje.To.Add(MailboxAddress.Parse(correo.Para));
        mensaje.Subject = correo.Asunto;
        mensaje.Body = new TextPart("html") { Text = correo.CuerpoHtml };

        using var cliente = new SmtpClient();
        await cliente.ConnectAsync(configuracion.Host, configuracion.Puerto,
            configuracion.UsarTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (!string.IsNullOrEmpty(configuracion.Usuario))
            await cliente.AuthenticateAsync(configuracion.Usuario, configuracion.Clave, ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(quit: true, ct);
    }
}
