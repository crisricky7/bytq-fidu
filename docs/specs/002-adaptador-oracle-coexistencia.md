# SPEC-002 — Adaptador Oracle para coexistencia con el monolito

- **Estado:** Aprobada
- **Issue:** [#2](https://github.com/crisricky7/bytq-fidu/issues/2)
- **Prioridad:** Alta (bloquea el corte de `/api/auditoria/**`)
- **Relacionado:** DECISIONES.md §2, §4

## Contexto

El microservicio persiste hoy en PostgreSQL. Durante el canary, el monolito y el microservicio atienden la misma ruta al mismo tiempo. Si cada uno escribe en su propia base: (a) `consulta` devuelve resultados distintos según qué sistema atienda; (b) los ids pueden repetirse; (c) el rollback deja registros "huérfanos" en la base del microservicio. Además, el monolito guarda `FECHA_REGISTRO` como `DATE` en hora local de Quito y el microservicio en UTC.

## Objetivo

Que durante la coexistencia ambos sistemas escriban y lean **la misma tabla `T01AUDITORIA`** con resultados indistinguibles para los consumidores.

## Alcance

- Implementación Oracle de `IAuditoriaRepository`, `IUnitOfWork` e `IAuditoriaLecturas`, elegida por configuración (`Persistencia:Proveedor = PostgreSql | Oracle`).
- Uso de `SQ01AUDITORIA.NEXTVAL` para el id.
- Tabla outbox en el esquema de Oracle, en la misma transacción.
- Convención de fecha compatible con el monolito.

**Fuera de alcance:** migrar `FECHA_REGISTRO` a `TIMESTAMP WITH TIME ZONE`, retirar el módulo del monolito y particionar la tabla (specs posteriores).

## Requisitos

- **R1.** Con `Proveedor = Oracle`, el id se obtiene de `SQ01AUDITORIA.NEXTVAL` (la misma secuencia del monolito).
- **R2.** El registro en `T01AUDITORIA` y el mensaje en `T01AUDITORIA_OUTBOX` se confirman en la misma transacción Oracle.
- **R3.** `FECHA_REGISTRO` se escribe en hora local `America/Guayaquil` (convención del monolito). El dominio sigue trabajando en UTC y la conversión vive solo en el adaptador.
- **R4.** La consulta aplica los mismos filtros que el monolito (`UPPER(ENTIDAD) LIKE`) con parámetros enlazados y devuelve la misma forma y orden.
- **R5.** El usuario de base de datos del microservicio solo tiene `INSERT/SELECT` sobre `T01AUDITORIA`, `SELECT` sobre la secuencia y `INSERT/SELECT/UPDATE` sobre el outbox.
- **R6.** El cambio de proveedor no modifica Domain, Application ni Api.

## Criterios de aceptación

1. **Dado** un registro creado por el monolito y otro por el microservicio, **cuando** se consulta desde cualquiera de los dos, **entonces** ambos aparecen con ids distintos y en el mismo orden.
2. **Dado** un registro creado a las 22:30 hora de Quito, **cuando** se consulta con `fechaDesde` del mismo día en ambos sistemas, **entonces** aparece en los dos.
3. **Dado** un fallo al insertar el outbox, **cuando** se registra, **entonces** no queda fila en `T01AUDITORIA` (misma prueba de atomicidad que en PostgreSQL).
4. **Dado** `usuario = x' OR '1'='1`, **cuando** se consulta, **entonces** no se devuelven registros de otros usuarios.
5. **Dado** el conjunto de pruebas de integración existente, **cuando** se ejecuta contra Oracle, **entonces** pasa sin cambios en las aserciones.

## Diseño propuesto

- Proyecto `Auditoria.Infrastructure.Oracle` con `Oracle.EntityFrameworkCore` y mapeo explícito a `T01AUDITORIA` (nombres en mayúsculas, `NUMBER(19)`, `VARCHAR2`, `CLOB`).
- Value converter `DateTimeOffset (UTC) ↔ DATE local` usando la zona configurada. Es un punto único y explícito, marcado para eliminarse cuando se migre la columna.
- Script `db/oracle/001_outbox.sql` revisado por el DBA; `T01AUDITORIA` no se toca.
- Pruebas de integración parametrizadas por proveedor; Oracle Free (`gvenzl/oracle-free`) con Testcontainers en CI nocturno, porque tarda en arrancar.

## Riesgos

- `DATE` en hora local es ambiguo si algún día hay cambio de horario (Ecuador no lo usa hoy). Se documenta y se elimina al migrar la columna.
- Los permisos del usuario Oracle dependen de la DBA: gestionarlos con anticipación.
- Si el monolito cachea la secuencia (`CACHE 20`), los ids no serán estrictamente crecientes entre sistemas. Por eso el orden de la consulta se basa en fecha y no en id.

## Preguntas abiertas

- ¿Existe hoy algún índice sobre `T01AUDITORIA(CODIGO_EMPRESA, FECHA_REGISTRO)`? Si no, crearlo antes del canary.
- ¿Hay procesos batch que lean `T01AUDITORIA` directamente y dependan del formato de fecha?

## Definición de terminado

Criterios 1–5 en verde contra Oracle Free, script del outbox aprobado por el DBA, usuario con permisos mínimos creado en no-prod y comparación de consultas monolito/microservicio sin diferencias.
