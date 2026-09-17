using System.Diagnostics;
using Auditoria.Api.Contrato;
using Microsoft.AspNetCore.Diagnostics;

namespace Auditoria.Api.Configuracion;

/// <summary>
/// Ningún error se reporta como éxito y ningún detalle interno (mensaje de excepción, stack trace)
/// sale al cliente: se devuelve el traceId para correlacionar con los logs.
/// </summary>
public sealed class ManejadorErrores(ILogger<ManejadorErrores> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier;

        if (excepcion is BadHttpRequestException)
        {
            logger.LogInformation(excepcion, "Petición mal formada");
            await EscribirAsync(contexto, StatusCodes.Status400BadRequest,
                CodigosRespuesta.PeticionInvalida, "Petición inválida.", null, ct);
            return true;
        }

        logger.LogError(excepcion, "Error no controlado procesando {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);

        if (HttpMethods.IsPost(contexto.Request.Method))
        {
            // Mismo código y estado que el sistema actual usa cuando no puede insertar (200 + E99).
            await EscribirAsync(contexto, StatusCodes.Status200OK,
                CodigosRespuesta.ErrorInterno, "No se pudo registrar la auditoría.", $"traceId: {traceId}", ct);
        }
        else
        {
            await EscribirAsync(contexto, StatusCodes.Status500InternalServerError,
                CodigosRespuesta.ErrorInterno, "Error interno.", $"traceId: {traceId}", ct);
        }
        return true;
    }

    private static Task EscribirAsync(HttpContext contexto, int estado, string codigo, string mensaje, string? detalle, CancellationToken ct)
    {
        contexto.Response.StatusCode = estado;
        return contexto.Response.WriteAsJsonAsync(new RespuestaEstandar<object>(codigo, mensaje, detalle, null), ct);
    }
}
