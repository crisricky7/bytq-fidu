# Ejercicio técnico — Ingeniero de Microservicios y Cloud

**Tiempo estimado: 3 a 4 horas.** No es una prueba de resistencia. Si llegas a las 4 horas,
para y entrega lo que tengas: parte de lo que evaluamos es cómo priorizas cuando el tiempo
se acaba.

**Formato de entrega:** repositorio Git (público, o zip con la carpeta `.git` incluida).
Queremos ver tus commits, no solo el resultado final.

**Después de la entrega:** sesión de 45 minutos donde vas a explicar y defender tus
decisiones. Esa conversación pesa tanto como el código.

---

## 1. Contexto

Una fiduciaria opera una plataforma interna llamada **CORE-FID**: un monolito en
**.NET Framework 4.8** sobre **Oracle 19c**, con tres puntos de entrada (una web
ASP.NET, una API REST y un motor batch) que comparten la misma capa de negocio.

La plataforma está viva, es crítica y **no se puede apagar**. La estrategia de
modernización es **Strangler Fig**: se levanta un API Gateway delante del monolito y se
van extrayendo módulos, uno por uno, a microservicios en Kubernetes. Cuando un módulo
está listo, el gateway redirige esa ruta al microservicio nuevo. El monolito no se entera.

El primer módulo a extraer es **Auditoría**. Se eligió a propósito: es un dominio
genérico, sin lógica financiera crítica, y sirve para validar el flujo completo de punta
a punta antes de tocar los dominios de negocio.

La arquitectura destino define estos patrones, que son obligatorios en todos los
microservicios del proyecto:

| Capa | Patrón |
|---|---|
| Domain | Entidades, Value Objects, Domain Events. Sin dependencias externas. |
| Application | CQRS — Commands y Queries como clases explícitas |
| Infrastructure | Repository, Unit of Work, **Outbox Pattern**, mensajería |
| API | REST, contrato OpenAPI, DTOs de entrada/salida |

---

## 2. Lo que recibes

```
starter/
├── legacy/                       # El código actual del módulo, tal cual está en producción
│   ├── ADOracle.cs               #   capa de acceso a datos genérica (no la vas a portar)
│   ├── EAuditoria.cs             #   persistencia (convención E[Entidad])
│   ├── BAuditoria.cs             #   lógica de negocio (convención B[Entidad])
│   ├── AuditoriaController.cs    #   controlador REST
│   └── Models.cs                 #   DTOs
├── contrato/
│   └── auditoria-legacy-v1.yaml  # Contrato OpenAPI VIGENTE EN PRODUCCIÓN
├── docker-compose.yml            # PostgreSQL + RabbitMQ
└── JWT-DEV.md                    # Cómo simular los tokens
```

> **Sobre PostgreSQL:** el destino real es Oracle. Usamos PostgreSQL en el ejercicio para
> que no pierdas 45 minutos levantando un contenedor de Oracle. Tenlo presente en tu diseño.

### La regla de oro del contrato

El microservicio **debe responder exactamente el mismo contrato** que el monolito
(`contrato/auditoria-legacy-v1.yaml`), incluidos los códigos de error `E01`–`E05`, `E10`,
`E99` y el envoltorio `{codigo, mensaje, detalle, data}`.

Sí, el contrato tiene defectos evidentes. Anótalos, pero **no los arregles**: hay una app
móvil, un motor BPM y tres procesos batch consumiéndolo hoy. Extraer el módulo y versionar
el contrato son dos cambios independientes, y mezclarlos es la forma más rápida de romper
producción. En `DECISIONES.md` explica qué cambiarías y cómo lo desplegarías después.

---

## 3. Lo que tienes que entregar

Está ordenado por prioridad. **No esperamos que completes todo.** Haz P0 bien antes de
tocar P1.

### P0 — Obligatorio

1. **Solución .NET 8 o superior** con separación en proyectos: `Domain`, `Application`,
   `Infrastructure`, `Api`. Las dependencias deben apuntar hacia adentro.
2. **Dominio.** Modela el registro de auditoría con sus invariantes. Las reglas están hoy
   dispersas como `if` en `BAuditoria.Registrar` y en el controlador — encuéntralas todas.
3. **CQRS.** Un Command (`RegistrarAuditoria`) y una Query (`ConsultarAuditoria`), cada uno
   con su handler. MediatR es opcional; si no lo usas, justifica la alternativa.
4. **Persistencia.** Repositorio sobre PostgreSQL (EF Core o Dapper, tu decisión) + el
   script o migración del esquema.
5. **API.** Los dos endpoints del contrato + `/healthz` y `/readyz` + Swagger.
6. **Outbox.** El command debe escribir el evento de dominio `AuditoriaRegistrada` en una
   tabla outbox **dentro de la misma transacción** que el registro de auditoría.
7. **Pruebas.** Mínimo 3 unitarias sobre dominio/handler y 1 de integración sobre el
   endpoint. `dotnet test` tiene que pasar en verde.
8. **`DECISIONES.md`** — ver sección 4.

### P1 — Deseable

9. **Dispatcher del Outbox** como `BackgroundService`: lee pendientes, publica, marca
   `published_at`. Tiene que ser idempotente y sobrevivir a que el broker esté caído.
   Publicar a RabbitMQ o a un `IEventPublisher` que loguee — nos interesa el mecanismo.
10. **Validación de JWT** (issuer, audience, firma, expiración) según `JWT-DEV.md`.
11. **Dockerfile multi-stage** y que `docker compose up` levante todo funcionando.
12. **Logs estructurados en JSON** y un `traceId` que se pueda seguir de punta a punta.

### P2 — Si sobra tiempo, o solo documentado en `DECISIONES.md`

13. **Manifiestos de Kubernetes** (Deployment, Service, probes, requests/limits) y un
    `HTTPRoute` del Gateway API que corte la ruta `/api/auditoria/**` del monolito al
    microservicio, con *weighted routing* para hacer canary.
14. **Instrumentación OpenTelemetry** (trazas y métricas).

---

## 4. `DECISIONES.md` — el entregable que más pesa

Máximo 2 páginas. Escribe como si fuera para el equipo que va a mantener esto:

1. **Qué encontraste en el código legacy.** Lista los problemas que detectaste, ordenados
   por gravedad, y di cuál arreglaste y cuál no. *(Hay varios. Algunos son serios.)*
2. **Dónde pusiste cada regla de negocio y por qué ahí.**
3. **Qué problema resuelve el Outbox aquí en concreto.** No la definición del patrón: qué
   pasa hoy en `BAuditoria.Registrar` que el Outbox arregla.
4. **Plan de corte.** Cómo pasas el tráfico de `/api/auditoria/**` del monolito al
   microservicio sin downtime, cómo verificas que salió bien, y cómo haces rollback a las
   3 de la mañana si sale mal.
5. **Qué dejaste fuera y qué harías con 2 días más.**
6. **Uso de IA** — ver abajo.

---

## 5. Uso de herramientas de IA

**Está permitido y es bienvenido.** La metodología del proyecto es *Spec Driven
Development* con asistentes de IA, así que es exactamente como vas a trabajar.

La única condición: en `DECISIONES.md`, una sección corta indicando qué herramientas usaste,
qué generaste con ellas y **qué tuviste que corregir de lo que te entregaron**. Esa última
parte es la que nos interesa de verdad.

En la sesión de 45 minutos vas a tener que explicar cualquier línea del repositorio. Código
que no puedas defender cuenta en contra, lo haya escrito una IA o tú.

---

## 6. Cómo se evalúa

| Peso | Qué miramos |
|---|---|
| 30 % | Diseño: separación de capas, dominio con invariantes reales, CQRS bien aplicado |
| 20 % | Outbox y transaccionalidad |
| 20 % | Pruebas: que existan, que sean significativas, que corran |
| 15 % | Criterio: lo que dice `DECISIONES.md`, sobre todo el plan de corte |
| 15 % | Operación: Docker, health checks, configuración, logs, seguridad |

**Lo que NO evaluamos:** que esté todo completo, que uses una librería concreta, ni el UI.
**Lo que sí pesa en contra:** credenciales en el repositorio, pruebas que no corren,
romper el contrato, y código que no puedas explicar.

---

## 7. Entrega

- Repositorio con `README.md` que diga cómo levantarlo en **un comando**.
- `DECISIONES.md`.
- Commits con mensajes legibles. Un único commit "initial commit" con todo adentro resta.

Si algo del enunciado te parece ambiguo: decide, documenta el supuesto en `DECISIONES.md`
y sigue. No te bloquees preguntando.
