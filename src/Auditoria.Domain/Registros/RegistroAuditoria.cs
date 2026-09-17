using Auditoria.Domain.Comun;

namespace Auditoria.Domain.Registros;

/// <summary>
/// Registro de auditoría. Es append-only: se crea válido y no expone ninguna operación de
/// modificación ni de borrado.
/// </summary>
public sealed class RegistroAuditoria : ITieneEventosDominio
{
    public const int LongitudCodigoEmpresa = 4;
    public const int LongitudCodigoTransaccion = 10;
    public const int LongitudUsuario = 30;
    public const int LongitudEntidad = 60;
    public const int LongitudClaveEntidad = 60;
    public const int LongitudDireccionIp = 45; // IPv6 completa; el contrato no la acota.

    private readonly List<IEventoDominio> _eventos = [];

    // Constructor para materialización desde persistencia.
    private RegistroAuditoria() { }

    public long Id { get; private init; }
    public string CodigoEmpresa { get; private init; } = null!;
    public CodigoModulo? CodigoModulo { get; private init; }
    public string? CodigoTransaccion { get; private init; }
    public string Usuario { get; private init; } = null!;
    public string? Entidad { get; private init; }
    public string ClaveEntidad { get; private init; } = null!;
    public Accion Accion { get; private init; } = null!;
    public string? ValorAnterior { get; private init; }
    public string? ValorNuevo { get; private init; }
    public string? DireccionIp { get; private init; }
    public Canal Canal { get; private init; } = null!;
    public DateTimeOffset FechaRegistro { get; private init; }

    /// <summary>
    /// Acciones que Seguridad debe conocer. Antes disparaban un correo dentro de la
    /// transacción; aquí solo se marcan en el evento y la notificación la hace un consumidor.
    /// </summary>
    public bool EsAccionSensible =>
        Accion == Accion.Eliminar
        || CodigoModulo?.Valor == CodigoModulo.FondosDeInversion
        || Entidad is "Usuario" or "Rol";

    public IReadOnlyCollection<IEventoDominio> EventosDominio => _eventos.AsReadOnly();

    public void LimpiarEventosDominio() => _eventos.Clear();

    /// <summary>
    /// Verifica todas las invariantes sin crear la instancia. El orden reproduce el del
    /// sistema actual para devolver el mismo código ante la misma petición.
    /// </summary>
    public static ErrorDominio? Validar(DatosRegistroAuditoria datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // En el sistema actual esta regla vivía en el controlador y se evaluaba primero.
        if (datos.Canal == Canal.Batch.Valor && datos.Accion == Accion.Consultar.Valor)
            return ErroresAuditoria.BatchNoRegistraConsultas;

        if (string.IsNullOrWhiteSpace(datos.CodigoEmpresa)) return ErroresAuditoria.EmpresaRequerida;
        if (string.IsNullOrWhiteSpace(datos.Usuario)) return ErroresAuditoria.UsuarioRequerido;

        var accion = Accion.Desde(datos.Accion);
        if (!accion.EsExitoso) return accion.Error;

        if (accion.Valor == Accion.Actualizar && string.IsNullOrEmpty(datos.ValorAnterior))
            return ErroresAuditoria.ActualizacionSinValorAnterior;

        if (string.IsNullOrWhiteSpace(datos.ClaveEntidad)) return ErroresAuditoria.ClaveEntidadRequerida;

        return Canal.Desde(datos.Canal).Error
            ?? CodigoModulo.Desde(datos.CodigoModulo).Error
            ?? ValidarLongitud(datos.CodigoEmpresa, LongitudCodigoEmpresa, "codigoEmpresa")
            ?? ValidarLongitud(datos.CodigoTransaccion, LongitudCodigoTransaccion, "codigoTransaccion")
            ?? ValidarLongitud(datos.Usuario, LongitudUsuario, "usuario")
            ?? ValidarLongitud(datos.Entidad, LongitudEntidad, "entidad")
            ?? ValidarLongitud(datos.ClaveEntidad, LongitudClaveEntidad, "claveEntidad")
            ?? ValidarLongitud(datos.DireccionIp, LongitudDireccionIp, "direccionIp");
    }

    /// <param name="id">Identificador ya reservado en la secuencia.</param>
    /// <param name="datos">Datos de la petición.</param>
    /// <param name="fechaRegistro">Instante en UTC, provisto por un reloj inyectado.</param>
    public static Resultado<RegistroAuditoria> Crear(long id, DatosRegistroAuditoria datos, DateTimeOffset fechaRegistro)
    {
        var error = Validar(datos);
        if (error is not null) return error;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        if (fechaRegistro.Offset != TimeSpan.Zero)
            throw new ArgumentException("La fecha de registro debe estar en UTC.", nameof(fechaRegistro));

        var registro = new RegistroAuditoria
        {
            Id = id,
            CodigoEmpresa = datos.CodigoEmpresa!,
            CodigoModulo = CodigoModulo.Desde(datos.CodigoModulo).Valor,
            CodigoTransaccion = datos.CodigoTransaccion,
            Usuario = datos.Usuario!,
            Entidad = datos.Entidad,
            ClaveEntidad = datos.ClaveEntidad!,
            Accion = Accion.Desde(datos.Accion).Valor,
            ValorAnterior = datos.ValorAnterior,
            ValorNuevo = datos.ValorNuevo,
            DireccionIp = datos.DireccionIp,
            Canal = Canal.Desde(datos.Canal).Valor,
            FechaRegistro = fechaRegistro
        };

        registro._eventos.Add(new AuditoriaRegistrada(
            registro.Id, registro.CodigoEmpresa, registro.CodigoModulo?.Valor, registro.CodigoTransaccion,
            registro.Usuario, registro.Entidad, registro.ClaveEntidad, registro.Accion.Valor,
            registro.ValorAnterior, registro.ValorNuevo, registro.DireccionIp, registro.Canal.Valor,
            registro.EsAccionSensible, fechaRegistro));

        return Resultado<RegistroAuditoria>.Exito(registro);
    }

    private static ErrorDominio? ValidarLongitud(string? valor, int maximo, string campo) =>
        valor is not null && valor.Length > maximo
            ? ErroresAuditoria.FormatoInvalido($"{campo} admite máximo {maximo} caracteres.")
            : null;
}
