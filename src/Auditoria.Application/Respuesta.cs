namespace Auditoria.Application;

/// <summary>Resultado de un caso de uso con la forma del envoltorio del contrato.</summary>
public sealed record Respuesta<T>(string Codigo, string Mensaje, string? Detalle, T? Data)
{
    public const string CodigoOk = "OK";

    public bool EsExitosa => Codigo == CodigoOk;

    public static Respuesta<T> Ok(string mensaje, T data) => new(CodigoOk, mensaje, null, data);

    public static Respuesta<T> Error(string codigo, string mensaje) => new(codigo, mensaje, null, default);
}
