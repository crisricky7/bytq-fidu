using Auditoria.Domain.Comun;

namespace Auditoria.Domain.Registros;

/// <summary>Subsistema de CORE-FID, "00".."99". Opcional en el contrato.</summary>
public sealed record CodigoModulo
{
    public const string FondosDeInversion = "06";

    private CodigoModulo(string valor) => Valor = valor;

    public string Valor { get; }

    public static Resultado<CodigoModulo?> Desde(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return Resultado<CodigoModulo?>.Exito(null);

        if (valor.Length > 2 || !valor.All(char.IsAsciiDigit))
            return ErroresAuditoria.FormatoInvalido("codigoModulo debe tener 1 o 2 dígitos (00-99).");

        return Resultado<CodigoModulo?>.Exito(new CodigoModulo(valor));
    }

    public override string ToString() => Valor;
}
