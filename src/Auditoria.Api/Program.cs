using System.Diagnostics;
using System.Text.Json;
using Auditoria.Api.Configuracion;
using Auditoria.Api.Endpoints;
using Auditoria.Application;
using Auditoria.Application.Consultar;
using Auditoria.Infrastructure;
using Auditoria.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Logs JSON en stdout con traceId/spanId de la actividad en curso.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});
builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

// Trazas y métricas OpenTelemetry. Se exportan por OTLP solo si hay collector configurado
// (OTEL_EXPORTER_OTLP_ENDPOINT); sin él la instrumentación sigue alimentando el traceId de los logs.
var exportarOtlp = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "auditoria-api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation(o => o.Filter = contexto =>
                !contexto.Request.Path.StartsWithSegments("/healthz") && !contexto.Request.Path.StartsWithSegments("/readyz"))
            .AddNpgsql()
            .AddSource(Telemetria.Nombre);
        if (exportarOtlp) t.AddOtlpExporter();
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation().AddMeter(Telemetria.Nombre);
        if (exportarOtlp) m.AddOtlpExporter();
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<OpcionesConsulta>(builder.Configuration.GetSection(OpcionesConsulta.Seccion));
builder.Services.AddAutenticacionJwt(builder.Configuration);

builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
// Errores de binding (JSON mal formado, fecha inválida) pasan por el manejador para respetar el envoltorio.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<ManejadorErrores>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuditoriaDbContext>("postgresql", tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CORE-FID - Microservicio de Auditoría",
        Version = "1.0",
        Description = "Implementa el contrato vigente auditoria-legacy-v1."
    });
    o.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    o.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", documento)] = []
    });
});

var app = builder.Build();

app.UseExceptionHandler();

app.Use(async (contexto, siguiente) =>
{
    contexto.Response.OnStarting(() =>
    {
        contexto.Response.Headers["X-Trace-Id"] = Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier;
        return Task.CompletedTask;
    });
    await siguiente(contexto);
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// Liveness: el proceso responde. No revisa dependencias para que K8s no reinicie pods por una BD caída.
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
// Readiness: puede atender tráfico (BD accesible). Si falla, K8s lo saca del Service sin reiniciarlo.
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

app.MapAuditoria();

app.Run();

public partial class Program;
