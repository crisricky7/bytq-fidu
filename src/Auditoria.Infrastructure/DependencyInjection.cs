using Auditoria.Application.Abstracciones;
using Auditoria.Infrastructure.Mensajeria;
using Auditoria.Infrastructure.Outbox;
using Auditoria.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Auditoria.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuracion)
    {
        services.AddDbContext<AuditoriaDbContext>((sp, o) =>
        {
            var cadena = configuracion.GetConnectionString("Auditoria")
                ?? throw new InvalidOperationException("Falta ConnectionStrings:Auditoria (variable ConnectionStrings__Auditoria).");
            o.UseNpgsql(cadena, npgsql => npgsql.EnableRetryOnFailure(3));
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AuditoriaDbContext>());
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
        services.AddScoped<IAuditoriaLecturas, AuditoriaLecturas>();

        services.Configure<OpcionesOutbox>(configuracion.GetSection(OpcionesOutbox.Seccion));
        services.Configure<OpcionesRabbitMq>(configuracion.GetSection(OpcionesRabbitMq.Seccion));

        services.AddSingleton<IPublicadorEventos>(sp =>
            sp.GetRequiredService<IOptions<OpcionesOutbox>>().Value.Publicador.Equals("RabbitMq", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<PublicadorRabbitMq>(sp)
                : ActivatorUtilities.CreateInstance<PublicadorLog>(sp));

        services.AddSingleton<DespachadorOutbox>();
        services.AddHostedService(sp => sp.GetRequiredService<DespachadorOutbox>());
        return services;
    }
}
