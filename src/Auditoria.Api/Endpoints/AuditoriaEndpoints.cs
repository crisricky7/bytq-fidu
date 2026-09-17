using Auditoria.Api.Contrato;
using Auditoria.Application;
using Auditoria.Application.Abstracciones;
using Auditoria.Application.Consultar;
using Auditoria.Application.Registrar;
using Auditoria.Domain.Registros;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Auditoria.Api.Endpoints;

public static class AuditoriaEndpoints
{
    public const string PoliticaEscritura = "audit.write";
    public const string PoliticaLectura = "audit.read";

    public static IEndpointRouteBuilder MapAuditoria(this IEndpointRouteBuilder app)
    {
        // Ruta sin versión, igual que el contrato vigente. La versión va en /api/v1 cuando cambie el contrato.
        var grupo = app.MapGroup("/api/auditoria").WithTags("Auditoría");

        grupo.MapPost("/registro", RegistrarAsync)
            .RequireAuthorization(PoliticaEscritura)
            .WithName("RegistrarAuditoria")
            .Produces<RespuestaEstandar<long>>()
            .Produces<RespuestaEstandar<long>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/consulta", ConsultarAsync)
            .RequireAuthorization(PoliticaLectura)
            .WithName("ConsultarAuditoria")
            .Produces<RespuestaEstandar<IReadOnlyList<RegistroAuditoriaResponse>>>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> RegistrarAsync(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RegistroAuditoriaRequest? request,
        ICommandHandler<RegistrarAuditoria, Respuesta<long>> handler,
        ILoggerFactory loggers,
        CancellationToken ct)
    {
        if (request is null)
            return Results.BadRequest(new RespuestaEstandar<long>(CodigosRespuesta.PeticionInvalida, "Petición vacía.", null, 0));

        var datos = new DatosRegistroAuditoria(
            request.CodigoEmpresa, request.CodigoModulo, request.CodigoTransaccion, request.Usuario,
            request.Entidad, request.ClaveEntidad, request.Accion, request.ValorAnterior, request.ValorNuevo,
            request.DireccionIp, request.Canal);

        var respuesta = await handler.ManejarAsync(new RegistrarAuditoria(datos), ct);

        var logger = loggers.CreateLogger(typeof(AuditoriaEndpoints));
        if (respuesta.EsExitosa)
            logger.LogInformation("Auditoría registrada {RegistroId} empresa {CodigoEmpresa} accion {Accion}",
                respuesta.Data, request.CodigoEmpresa, request.Accion);
        else
            logger.LogInformation("Registro de auditoría rechazado {Codigo}: {Mensaje}", respuesta.Codigo, respuesta.Mensaje);

        // Los valores anterior/nuevo pueden traer datos personales: no se escriben en el log.
        return Results.Json(
            new RespuestaEstandar<long>(respuesta.Codigo, respuesta.Mensaje, respuesta.Detalle, respuesta.Data),
            statusCode: CodigosRespuesta.EstadoHttp(respuesta.Codigo));
    }

    private static async Task<IResult> ConsultarAsync(
        string? codigoEmpresa,
        string? usuario,
        string? entidad,
        DateOnly? fechaDesde,
        IQueryHandler<ConsultarAuditoria, Respuesta<IReadOnlyList<RegistroAuditoriaDto>>> handler,
        CancellationToken ct)
    {
        var respuesta = await handler.ManejarAsync(new ConsultarAuditoria(codigoEmpresa, usuario, entidad, fechaDesde), ct);

        if (!respuesta.EsExitosa)
            return Results.BadRequest(new RespuestaEstandar<IReadOnlyList<RegistroAuditoriaResponse>>(
                respuesta.Codigo, respuesta.Mensaje, null, []));

        var data = respuesta.Data!.Select(r => new RegistroAuditoriaResponse(
            r.Id, r.CodigoEmpresa, r.CodigoModulo, r.CodigoTransaccion, r.Usuario, r.Entidad, r.ClaveEntidad,
            r.Accion, r.ValorAnterior, r.ValorNuevo, r.DireccionIp, r.Canal, r.FechaRegistro)).ToList();

        return Results.Ok(new RespuestaEstandar<IReadOnlyList<RegistroAuditoriaResponse>>(
            respuesta.Codigo, respuesta.Mensaje, null, data));
    }
}
