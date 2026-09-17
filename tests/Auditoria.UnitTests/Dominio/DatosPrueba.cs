using Auditoria.Domain.Registros;

namespace Auditoria.UnitTests.Dominio;

internal static class DatosPrueba
{
    public static readonly DateTimeOffset Ahora = new(2026, 9, 16, 22, 0, 0, TimeSpan.Zero);

    public static DatosRegistroAuditoria Validos() => new(
        CodigoEmpresa: "0001",
        CodigoModulo: "03",
        CodigoTransaccion: "V062014",
        Usuario: "jperez",
        Entidad: "SolicitudRescate",
        ClaveEntidad: "RES-2026-004871",
        Accion: "C",
        ValorAnterior: null,
        ValorNuevo: "{\"monto\":100}",
        DireccionIp: "10.0.4.77",
        Canal: "API");
}
