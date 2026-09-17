using Auditoria.Application.Abstracciones;
using Auditoria.Domain.Registros;

namespace Auditoria.Application.Registrar;

public sealed record RegistrarAuditoria(DatosRegistroAuditoria Datos);

public sealed class RegistrarAuditoriaHandler(
    IAuditoriaRepository repositorio,
    IUnitOfWork unidadDeTrabajo,
    TimeProvider reloj) : ICommandHandler<RegistrarAuditoria, Respuesta<long>>
{
    public const string MensajeCreado = "Registro creado.";

    public async Task<Respuesta<long>> ManejarAsync(RegistrarAuditoria command, CancellationToken ct)
    {
        // Validar antes de reservar el id evita consumir la secuencia con peticiones inválidas.
        var error = RegistroAuditoria.Validar(command.Datos);
        if (error is not null)
            return Respuesta<long>.Error(error.Codigo, error.Mensaje);

        var id = await repositorio.ReservarIdAsync(ct);
        var registro = RegistroAuditoria.Crear(id, command.Datos, reloj.GetUtcNow()).Valor;

        repositorio.Agregar(registro);
        // Registro + evento en outbox: una sola transacción. Si falla, no queda ninguno de los dos.
        await unidadDeTrabajo.GuardarCambiosAsync(ct);

        return Respuesta<long>.Ok(MensajeCreado, registro.Id);
    }
}
