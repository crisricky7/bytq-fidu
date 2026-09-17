namespace Auditoria.Infrastructure.Outbox;

public sealed class MensajeOutbox
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>W3C traceparent de la petición que originó el evento, para continuar la traza.</summary>
    public string? TraceParent { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}
