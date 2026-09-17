# SPEC-003 — Contrato de Auditoría v2

- **Estado:** Aprobada
- **Issue:** [#3](https://github.com/crisricky7/bytq-fidu/issues/3)
- **Prioridad:** Media (se inicia cuando el corte del contrato actual esté al 100 %)
- **Relacionado:** DECISIONES.md §1 (defectos del contrato), §5

## Contexto

El contrato vigente se respeta tal cual porque la app móvil, el motor BPM y tres procesos batch lo consumen. Sus defectos: errores de negocio con HTTP 200, `E00` no documentado, sin versión, sin paginación, `fechaDesde` sin zona, snapshots como string, `usuario` enviado por el cliente y sin 403/500 documentados.

## Objetivo

Publicar una ruta versionada `/api/v2/auditoria` que corrija esos defectos **en paralelo** a la actual, y migrar consumidor por consumidor sin romper a nadie.

## Alcance

- Nuevo contrato OpenAPI `auditoria-v2.yaml`.
- Endpoints nuevos sobre los mismos casos de uso (Application y Domain no cambian).
- Política de deprecación de la ruta sin versión.

**Fuera de alcance:** cambiar reglas de negocio y retirar la ruta actual (solo se agenda).

## Requisitos

- **R1.** `POST /api/v2/auditoria/registros` → `201 Created` con `Location` y cuerpo `{ id }`; errores con `application/problem+json` (RFC 9457), `status` real y `code` = `E01…E10` para trazabilidad.
- **R2.** `usuario` sale del claim `preferred_username` del token; si el body lo trae y no coincide → `403`. Los procesos batch que auditan en nombre de otros usan el rol `audit.write.delegated`.
- **R3.** `valorAnterior`/`valorNuevo` son objetos JSON, no strings.
- **R4.** `GET /api/v2/auditoria/registros` con paginación por cursor (`limit` ≤ 200, `cursor`), `fechaDesde`/`fechaHasta` en ISO 8601 **con zona** y `fechaDesde` obligatoria.
- **R5.** Se documentan los estados 400, 401, 403, 422, 429 y 500.
- **R6.** La ruta sin versión responde las cabeceras `Deprecation` y `Sunset` con la fecha acordada, y se mide qué consumidores aún la usan (por `client_id` del token).
- **R7.** Pruebas de contrato automatizadas contra ambos YAML en CI.

## Criterios de aceptación

1. **Dado** un registro válido en v2, **cuando** se envía, **entonces** responde `201` con `Location`, y la misma petición en la ruta actual sigue respondiendo `200 {codigo:"OK"}`.
2. **Dado** `accion = U` sin valor anterior en v2, **cuando** se envía, **entonces** responde `422` problem+json con `code = E04`.
3. **Dado** un token de `jperez` y un body con `usuario = otro`, **cuando** se envía en v2 sin rol delegado, **entonces** responde `403`.
4. **Dado** 450 registros, **cuando** se pagina con `limit = 200`, **entonces** se obtienen 3 páginas sin duplicados ni saltos, aunque entren registros nuevos mientras se pagina.
5. **Dado** cualquier llamada a la ruta sin versión, **cuando** responde, **entonces** incluye `Deprecation` y queda la métrica por consumidor.

## Diseño propuesto

- Grupo de endpoints `v2` en `Auditoria.Api` con su propio mapeo de errores: `Respuesta<T>` se traduce a problem+json.
- Cursor opaco = base64 de `(fecha_registro, id)` del último elemento; consulta `WHERE (fecha_registro, id) < (:f, :id)`, apoyada en el índice existente.
- Migración: BPM primero (equipo interno), luego batch y la app móvil con su ciclo de releases. La ruta sin versión se retira cuando la métrica llegue a cero durante 30 días.

## Riesgos

- La app móvil tiene versiones antiguas instaladas: retirar la ruta sin versión depende de forzar la actualización.
- Tomar el usuario del token puede romper a los batch que hoy auditan en nombre de otros: por eso existe el rol delegado (R2).

## Preguntas abiertas

- ¿El gateway ya propaga `client_id` para identificar consumidores?
- ¿Cuál es la ventana de soporte aceptable para la ruta sin versión?

## Definición de terminado

YAML v2 aprobado por los consumidores, criterios 1–5 en verde, pruebas de contrato en CI, cabeceras de deprecación activas y un tablero de uso por consumidor.
