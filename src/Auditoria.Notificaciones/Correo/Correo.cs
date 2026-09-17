namespace Auditoria.Notificaciones.Correo;

public sealed record CorreoSaliente(string Para, string Asunto, string CuerpoHtml);

public interface IEnviadorCorreo
{
    /// <summary>Debe lanzar excepción si el servidor no acepta el correo.</summary>
    Task EnviarAsync(CorreoSaliente correo, CancellationToken ct);
}
