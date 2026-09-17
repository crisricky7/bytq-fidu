using System.Diagnostics;

namespace Auditoria.Infrastructure;

public static class Telemetria
{
    public const string Nombre = "Auditoria";

    public static readonly ActivitySource Fuente = new(Nombre);
}
