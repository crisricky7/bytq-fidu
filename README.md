# Microservicio de Auditoría — CORE-FID

Extracción del módulo de Auditoría del monolito CORE-FID a un microservicio .NET 10, respetando
el contrato vigente (`starter/contrato/auditoria-legacy-v1.yaml`). Las decisiones de diseño, los
hallazgos del código legacy y el plan de corte están en **[DECISIONES.md](DECISIONES.md)**.

## Levantar todo (un comando)

Requisito: Docker Desktop / Docker Engine con Compose v2.

```bash
docker compose up --build
```

| Servicio | URL |
|---|---|
| API + Swagger | http://localhost:8080/swagger |
| Liveness / Readiness | http://localhost:8080/healthz · http://localhost:8080/readyz |
| RabbitMQ (management) | http://localhost:15672 (`auditoria` / `<REDACTADO>`) |
| PostgreSQL | `localhost:5432`, base `auditdb` (`audit` / `<REDACTADO>`) |

El esquema (`db/001_esquema.sql`) se aplica automáticamente al crear el volumen de PostgreSQL.
Para empezar de cero: `docker compose down -v`.

> Las credenciales y la clave JWT del compose son **solo de desarrollo** (las del material del
> ejercicio) y se pueden sobrescribir con variables de entorno (`POSTGRES_PASSWORD`,
> `RABBITMQ_PASSWORD`, `JWT_CLAVE_FIRMA`). La aplicación no tiene secretos en `appsettings.json`.

## Probar la API

```bash
TOKEN=$(node scripts/token-dev.mjs)            # o generarlo en jwt.io con starter/JWT-DEV.md

curl -s -X POST http://localhost:8080/api/auditoria/registro \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"codigoEmpresa":"0001","codigoModulo":"06","usuario":"jperez","entidad":"SolicitudRescate","claveEntidad":"RES-2026-004871","accion":"U","valorAnterior":"{}","valorNuevo":"{}","canal":"API"}'
# {"codigo":"OK","mensaje":"Registro creado.","detalle":null,"data":1}

curl -s "http://localhost:8080/api/auditoria/consulta?codigoEmpresa=0001&fechaDesde=2026-01-01" \
  -H "Authorization: Bearer $TOKEN"
```

Cada respuesta trae la cabecera `X-Trace-Id`, el mismo `TraceId` de los logs JSON del contenedor
(`docker logs auditoria-api`) y el `trace_parent` guardado en `auditoria.outbox_mensaje`.
Los eventos publicados llegan al exchange `corefid.auditoria` (routing key `auditoria.registrada`).

## Pruebas

```bash
dotnet test
```

- `Auditoria.UnitTests`: invariantes del dominio y handlers (reloj falso, dobles en memoria).
- `Auditoria.IntegrationTests`: API real con `WebApplicationFactory` y PostgreSQL efímero con
  **Testcontainers** (necesita Docker en ejecución; no requiere `docker compose up`). Cubren
  forma del contrato, atomicidad registro + outbox, JWT, filtros maliciosos y despachador con
  broker caído.

## Estructura

```
src/
  Auditoria.Domain          Agregado RegistroAuditoria, value objects, evento AuditoriaRegistrada (sin dependencias)
  Auditoria.Application     Command/Query + handlers, puertos (repositorio, unidad de trabajo, lecturas)
  Auditoria.Infrastructure  EF Core/PostgreSQL, outbox, DespachadorOutbox, publicadores Log/RabbitMQ
  Auditoria.Api             Endpoints del contrato, JWT, health checks, Swagger, logs JSON
tests/                      Unitarias e integración
db/001_esquema.sql          Esquema (tabla append-only, secuencia, outbox)
k8s/                        Deployment, Service, PDB, ConfigMap, plantilla de Secret, HTTPRoute canary
starter/                    Material de partida: código legacy, contrato vigente, compose original
```

Dependencias: `Api → Application, Infrastructure`; `Infrastructure → Application`; `Application → Domain`.

## Configuración

| Clave (variable de entorno) | Descripción |
|---|---|
| `ConnectionStrings__Auditoria` | Cadena de conexión PostgreSQL |
| `Autenticacion__Issuer` / `__Audience` | Valores esperados en el token |
| `Autenticacion__Authority` | Producción: Entra ID. Valida RS256 contra JWKS |
| `Autenticacion__ClaveFirma` | Desarrollo (si no hay Authority): clave HS256 ≥ 32 caracteres |
| `Outbox__Publicador` | `Log` (por defecto) o `RabbitMq` |
| `Outbox__Habilitado`, `__IntervaloSegundos`, `__TamanoLote`, `__MaximoBackoffSegundos` | Despachador |
| `RabbitMq__Host`, `__Puerto`, `__Usuario`, `__Clave`, `__Exchange` | Broker |
| `Auditoria__Consulta__MaximoRegistros`, `__ZonaHoraria` | Tope de filas y zona de `fechaDesde` |

### Ejecutar la API fuera de Docker

```bash
docker compose up -d postgres rabbitmq
export ConnectionStrings__Auditoria="Host=localhost;Database=auditdb;Username=audit;Password=<REDACTADO>"
export Autenticacion__ClaveFirma="<REDACTADO>"
dotnet run --project src/Auditoria.Api
```

## Observabilidad

- Logs JSON en stdout con `TraceId`/`SpanId`; cabecera `X-Trace-Id` en cada respuesta.
- OpenTelemetry: trazas de ASP.NET Core, Npgsql y la publicación del outbox (continúa la traza
  original mediante `trace_parent`), y métricas `auditoria.outbox.publicados`,
  `auditoria.outbox.fallidos` y `auditoria.outbox.retraso`. Se exportan por OTLP al definir
  `OTEL_EXPORTER_OTLP_ENDPOINT`.
