namespace Auditoria.Infrastructure.Outbox;

public sealed class OpcionesOutbox
{
    public const string Seccion = "Outbox";

    public bool Habilitado { get; set; } = true;
    public int IntervaloSegundos { get; set; } = 2;
    public int TamanoLote { get; set; } = 50;
    public int MaximoBackoffSegundos { get; set; } = 300;

    /// <summary>"Log" (solo registra el evento) o "RabbitMq".</summary>
    public string Publicador { get; set; } = "Log";
}
