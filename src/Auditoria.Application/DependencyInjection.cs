using Auditoria.Application.Abstracciones;
using Auditoria.Application.Consultar;
using Auditoria.Application.Registrar;
using Microsoft.Extensions.DependencyInjection;

namespace Auditoria.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICommandHandler<RegistrarAuditoria, Respuesta<long>>, RegistrarAuditoriaHandler>();
        services.AddScoped<IQueryHandler<ConsultarAuditoria, Respuesta<IReadOnlyList<RegistroAuditoriaDto>>>, ConsultarAuditoriaHandler>();
        return services;
    }
}
