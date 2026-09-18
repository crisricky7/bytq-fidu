using Auditoria.Notificaciones;
using Auditoria.Notificaciones.Correo;
using Auditoria.Notificaciones.Idempotencia;
using Auditoria.Notificaciones.Mensajeria;
using Auditoria.Notificaciones.Procesamiento;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});
builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

builder.Services.Configure<OpcionesNotificaciones>(builder.Configuration.GetSection(OpcionesNotificaciones.Seccion));
builder.Services.Configure<OpcionesRabbitMq>(builder.Configuration.GetSection(OpcionesRabbitMq.Seccion));
builder.Services.Configure<OpcionesSmtp>(builder.Configuration.GetSection(OpcionesSmtp.Seccion));

builder.Services.AddSingleton<IRegistroNotificaciones>(sp => new RegistroNotificacionesPostgres(
    builder.Configuration.GetConnectionString("Notificaciones")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Notificaciones (variable ConnectionStrings__Notificaciones)."),
    TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<OpcionesNotificaciones>>().Value.VencimientoReservaSegundos)));
builder.Services.AddSingleton<IEnviadorCorreo, EnviadorSmtp>();
builder.Services.AddSingleton<IProcesadorNotificaciones, ProcesadorNotificaciones>();
builder.Services.AddHostedService<ConsumidorNotificaciones>();

builder.Build().Run();
