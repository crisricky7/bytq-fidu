using Auditoria.Domain.Comun;

namespace Auditoria.Domain.Registros;

public sealed record Canal
{
    public static readonly Canal Web = new("WEB");
    public static readonly Canal Api = new("API");
    public static readonly Canal Batch = new("BATCH");

    private Canal(string valor) => Valor = valor;

    public string Valor { get; }

    /// <summary>Sin canal informado se asume WEB (default del contrato).</summary>
    public static Resultado<Canal> Desde(string? valor) => valor switch
    {
        null or "" => Web,
        "WEB" => Web,
        "API" => Api,
        "BATCH" => Batch,
        _ => ErroresAuditoria.FormatoInvalido("canal debe ser WEB, API o BATCH.")
    };

    public override string ToString() => Valor;
}
