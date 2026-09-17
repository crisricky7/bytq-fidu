using Auditoria.Domain.Comun;

namespace Auditoria.Domain.Registros;

/// <summary>
/// Acción auditada sobre la entidad de negocio (no una operación sobre el registro de
/// auditoría, que es inmutable): C = Crear, U = Actualizar, D = Eliminar, Q = Consultar.
/// </summary>
public sealed record Accion
{
    public static readonly Accion Crear = new("C");
    public static readonly Accion Actualizar = new("U");
    public static readonly Accion Eliminar = new("D");
    public static readonly Accion Consultar = new("Q");

    private Accion(string valor) => Valor = valor;

    public string Valor { get; }

    /// <summary>Sensible a mayúsculas, igual que el sistema actual.</summary>
    public static Resultado<Accion> Desde(string? valor) => valor switch
    {
        "C" => Crear,
        "U" => Actualizar,
        "D" => Eliminar,
        "Q" => Consultar,
        _ => ErroresAuditoria.AccionInvalida
    };

    public override string ToString() => Valor;
}
