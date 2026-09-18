# DECISIONES

Microservicio de Auditoría extraído de CORE-FID (primera pieza del Strangler Fig).
Alcance entregado: **P0 y P1 completos; P2 #13** (manifiestos K8s + HTTPRoute) **y #14** (OpenTelemetry, sin collector en el compose).

## 1. Qué encontré en el código legacy (por gravedad)

| # | Problema | Dónde | Gravedad | En el microservicio |
|---|---|---|---|---|
| 1 | **Correo enviado dentro del `TransactionScope`.** Si el commit falla después del envío, Seguridad recibe aviso de algo que nunca quedó registrado. Si el SMTP tarda, la transacción de Oracle queda abierta con locks. Y si el correo falla, se ignora en silencio. | `BAuditoria.Registrar` | Crítica | Arreglado: outbox (ver §3) |
| 2 | **SQL por concatenación** → inyección en `usuario`, `entidad`, `claveEntidad`, etc. `usuario = x' OR '1'='1` devuelve toda la empresa. | `EAuditoria.Insertar/Buscar` | Crítica | Arreglado: SQL parametrizado + prueba |
| 3 | **Credenciales de Oracle en el código** y cambio manual por ambiente. | `ADOracle` | Crítica | Arreglado: `IOptions` + variables de entorno / Secret. **La clave hay que rotarla en el monolito ya**: está en el historial de su repositorio |
| 4 | **`catch` que responde `OK "Procesado"`** ante cualquier excepción: el cliente cree que auditó y la evidencia se pierde. | `AuditoriaController.Registrar` | Crítica | Arreglado: E99 + traceId; prueba que lo verifica |
| 5 | **`SELECT *` sin límite ni paginación**: sin `fechaDesde` trae toda la historia de la empresa. | `EAuditoria.Buscar` | Alta | Mitigado: tope configurable + índices. Paginar requiere contrato v2 |
| 6 | **Conexiones sin `using`/`finally`**: toda excepción deja una conexión fuera del pool. | `EAuditoria` | Alta | Arreglado (DbContext con scope) |
| 7 | **`out string error` descartado**: una caída de BD en `Consultar` devuelve lista vacía, indistinguible de "no hay datos". | `EAuditoria.Buscar`, `BAuditoria.Consultar` | Alta | Arreglado: excepciones → 500 E99 |
| 8 | **`DateTime.Now`**: hora local del servidor, no inyectable ni testeable; `TO_DATE` además pierde precisión. | `BAuditoria.Registrar` | Media | Arreglado: `TimeProvider`, UTC. Ver riesgo en §4 |
| 9 | **Regla de negocio en el controlador** (BATCH no registra Q), fácil de perder al migrar. | `AuditoriaController` | Media | Movida al dominio, conservando que se evalúa primero |
| 10 | **`StackTrace` y `ex.Message` devueltos al cliente** en el 500 de consulta. | `AuditoriaController.Consultar` | Media | Arreglado: solo traceId |
| 11 | **API sin versionar** (`/api/auditoria`). | Controlador / contrato | Media | **No arreglado a propósito**: rompería consumidores |
| 12 | HTML sin escapar en el cuerpo del correo (inyección con `valorNuevo`), longitudes del contrato no validadas (el error llega como ORA- en `detalle`), `usuario` tomado del body y no del token (suplantable), clases con `new` en lugar de DI. | Varios | Media/Baja | Longitudes → dominio. Resto en §5 |

**Defectos del contrato (anotados, no corregidos):** errores de negocio con HTTP 200; `E00` usado pero no documentado; sin versión ni paginación; `fechaDesde` sin zona horaria; `valorAnterior/valorNuevo` como string en vez de objeto JSON; sin 403 ni 500 documentados; `usuario` enviado por el cliente. Además, Web API 2 serializa en PascalCase por defecto y el contrato dice camelCase: **hay que confirmarlo con tráfico real antes del corte**.

## 2. Dónde puse cada regla y por qué

- **Invariantes del registro → `Domain/RegistroAuditoria`** (E01–E05, E10, catálogo de `Accion`/`Canal`, formato de `CodigoModulo`, longitudes). Son verdad para cualquier canal (web, API, batch) y no dependen de HTTP. El agregado es **inmutable y append-only**: constructor privado, factory `Crear` que no permite una instancia inválida, sin métodos de modificación. `U`/`D` describen la acción *auditada*, no una operación sobre el registro. La BD lo refuerza con un trigger que rechaza UPDATE/DELETE y CHECKs de las dos reglas principales.
- **"Acción sensible" → propiedad del dominio** que viaja en el evento. Decidir *qué* es sensible es negocio; *enviar el correo* es un efecto secundario que ya no ocurre en el camino de escritura.
- **Orden de validación**: se conserva el del sistema actual (E10 primero, luego E01…E05) para que la misma petición devuelva el mismo código en los dos sistemas durante el canary.
- **Application**: orquesta (validar → reservar id → crear → guardar). Sin reglas propias, salvo `codigoEmpresa` obligatorio en la consulta.
- **API**: solo traduce contrato ↔ caso de uso y el código de negocio → estado HTTP (E00/E10 → 400, resto → 200, igual que hoy).
- **CQRS sin MediatR**: dos interfaces (`ICommandHandler`, `IQueryHandler`) registradas en DI. Con dos casos de uso el mediador solo agrega indirección (y hoy tiene licencia comercial). La query no toca el agregado: SQL parametrizado proyectado a DTO. Siendo honesto, en este servicio el beneficio inmediato es pequeño. El valor real aparece cuando la lectura (reportería pesada) se mueva a una réplica o a un modelo particionado por fecha sin afectar la escritura.
- **Persistencia: EF Core + script SQL** (no migraciones EF). El script es lo que un DBA de Oracle revisa y traduce 1:1; EF Core queda para el mapeo y la unidad de trabajo. El id sale de una secuencia reservada antes de crear el agregado, lo que calza con `SQ01AUDITORIA.NEXTVAL`.

## 3. Qué resuelve el Outbox aquí

En `BAuditoria.Registrar` el `INSERT` y el `EnviarCorreo` están en el mismo bloque, pero **el correo no es transaccional**. Casos concretos: (a) el correo sale y el commit falla → Seguridad investiga una eliminación que no existe; (b) el SMTP se cuelga → la transacción retiene locks en `T01AUDITORIA` mientras las tres entradas (web, API y batch) escriben ahí; (c) el correo falla → nadie se entera, porque el error se ignora.

Ahora el command guarda el registro **y** la fila `AuditoriaRegistrada` en `outbox_mensaje` con **un solo `SaveChanges`** (una transacción). Si el commit falla, no hay evento; si pasa, el evento existe y se publica tarde o temprano. La prueba `Si_falla_la_escritura_del_outbox_no_queda_el_registro_ni_se_responde_OK` lo demuestra forzando un fallo solo en el outbox. `DespachadorOutbox` publica fuera de la petición: `FOR UPDATE SKIP LOCKED` (varias réplicas), confirmación del broker, backoff exponencial, `attempts`/`last_error`, y sigue vivo si el broker o la BD caen. La entrega es **at-least-once**: el consumidor que envía el correo debe deduplicar por `MessageId`.

## 4. Plan de corte de `/api/auditoria/**`

**Prerrequisitos (no negociables):** el consumidor que notifica a Seguridad está desplegado (si no, las acciones sensibles que atienda el microservicio no generan correo); en coexistencia el microservicio escribe en **la misma tabla Oracle con la misma secuencia `SQ01AUDITORIA`** (así no hay ids duplicados y la consulta devuelve lo mismo, sin importar qué sistema atienda); y se define cómo se guarda la fecha. El monolito guarda hora local de Quito en `DATE` y el microservicio UTC; mezclarlas rompe los filtros por `fechaDesde`. Durante la coexistencia el adaptador Oracle escribe con la convención del monolito, y la conversión a UTC se hace después, con migración de datos.

1. **No-prod:** se reproduce tráfico real capturado contra ambos sistemas y se comparan respuestas (código, forma, casing) y filas escritas.
2. **Prod, peso 0:** despliegue y smoke con el header `X-Canary: auditoria` (regla dedicada del `HTTPRoute`).
3. **Canary 5 % → 25 % → 50 % → 100 %**, cada paso por PR de GitOps, al menos 30 minutos en horario hábil y con el batch nocturno incluido antes del 100 %. Primero `registro`; `consulta` puede ir en una regla separada.
4. **Qué miro (dashboards preparados antes):** tasa de E99/5xx y p95 del microservicio contra la línea base del monolito; 401/403 (tokens o roles distintos a lo esperado); pendientes del outbox y antigüedad del más viejo; registros por minuto en la tabla contra el volumen histórico de esa franja; correos a Seguridad enviados contra acciones sensibles registradas.
5. **Criterio de aborto (acordado antes de empezar):** E99/5xx > línea base + 0,5 pp; p95 > 1,5× el monolito; outbox con pendientes de más de 2 min; cualquier diferencia de contrato; salto de 401/403; conteos que no cuadran.
6. **Rollback a las 3 a. m.:** primero cortar y después investigar. Se revierte el PR o, en emergencia, `kubectl apply` del `HTTPRoute` con el microservicio en peso 0: segundos, sin redeploy y sin tocar el monolito. Los registros escritos por el microservicio son válidos y append-only, así que no se borran. Si hubiera duplicados (reintentos de clientes durante el cambio), se identifican por `traceId`/fecha y se marcan en una tabla de conciliación, nunca con DELETE sobre auditoría.
7. **Cierre:** con 100 % estable durante una semana y un cierre de mes, se retira el código del módulo en el monolito.

## 5. Qué dejé fuera y qué haría con 2 días más

Lo pendiente que bloquea el corte o cambia el contrato ya está especificado en [`docs/specs/`](docs/specs/README.md): SPEC-001 (notificación a Seguridad), SPEC-002 (adaptador Oracle para coexistencia) y SPEC-003 (contrato v2).

- ~~Consumidor de notificación a Seguridad~~: **implementado** en `Auditoria.Notificaciones` (SPEC-001, #1).
- **Adaptador Oracle** (`Oracle.EntityFrameworkCore`, secuencia compartida, convención de fecha) y pruebas contra Oracle XE.
- **Observabilidad completa**: hoy hay trazas OpenTelemetry (ASP.NET Core, Npgsql y la publicación del outbox, que continúa la traza con `trace_parent`) y métricas (`auditoria.outbox.publicados/fallidos/retraso`) exportadas por OTLP si se configura `OTEL_EXPORTER_OTLP_ENDPOINT`. Falta el collector en el compose, un gauge de pendientes, dashboards y alertas del canary, y propagar el contexto al consumidor en RabbitMQ.
- **Contrato v2** (`/api/v1`): errores con estados HTTP reales, paginación por cursor, `usuario` tomado del token, fechas con zona y snapshots como objetos JSON. Se publica en paralelo y se migra consumidor por consumidor.
- Retención y particionado por fecha de la tabla; limpieza de outbox publicado; pruebas de contrato automatizadas contra el YAML; pipeline CI (build, pruebas, escaneo de imagen); Helm en lugar de YAML plano.

Supuestos que tomé: formato inválido (canal fuera del catálogo, longitudes) → HTTP 400 con `E00`; fallo de infraestructura al registrar → 200 + `E99` (lo que hoy devuelve el insert fallido) con `traceId` en `detalle`; `fechaDesde` se interpreta en `America/Guayaquil`; la consulta exige los roles `audit.read` y el registro `audit.write`.

## 6. Uso de IA

Usé un **asistente IA de código** para: armar la estructura de la solución, escribir borradores del dominio, handlers, EF Core, despachador y pruebas, generar Dockerfile/compose/manifiestos y hacer un primer borrador de este documento. Las decisiones (contrato intacto, orden de validación, secuencia reservada, SQL de lectura, supuestos y plan de corte) las revisé y ajusté yo.

Lo que tuve que corregir de lo que entregó:
- El despachador abría una transacción manual y **fallaba en ejecución** con la estrategia de reintentos de Npgsql. Lo detectó la prueba de integración y se reescribió dentro de `CreateExecutionStrategy()`.
- El escape de comodines `LIKE` venía con secuencias inválidas y no compilaba; se corrigió y se agregó la prueba con `entidad=%` para verificar el escape.
- `Resultado<T>` no tenía conversión implícita desde el valor y los value objects no compilaban.
- Una edición automática del `.csproj` de pruebas lo dejó corrupto y hubo que reescribirlo.
- La cadena de conexión en contenedor generaba errores de `libgssapi` en el log; se deshabilitó GSS explícitamente.
