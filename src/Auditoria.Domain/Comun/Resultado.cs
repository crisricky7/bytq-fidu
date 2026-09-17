namespace Auditoria.Domain.Comun;

public sealed class Resultado<T>
{
    private readonly T? _valor;

    private Resultado(T? valor, ErrorDominio? error)
    {
        _valor = valor;
        Error = error;
    }

    public ErrorDominio? Error { get; }
    public bool EsExitoso => Error is null;

    public T Valor => EsExitoso
        ? _valor!
        : throw new InvalidOperationException($"Resultado fallido ({Error!.Codigo}); no tiene valor.");

    public static Resultado<T> Exito(T valor) => new(valor, null);
    public static Resultado<T> Fallo(ErrorDominio error) => new(default, error);

    public static implicit operator Resultado<T>(T valor) => Exito(valor);
    public static implicit operator Resultado<T>(ErrorDominio error) => Fallo(error);
}
