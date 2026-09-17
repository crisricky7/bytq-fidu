# Especificaciones

Trabajo con *Spec Driven Development*: ningún cambio se implementa sin una spec revisada.

## Flujo

1. **Spec** en `docs/specs/NNN-nombre.md` con estado `Borrador`.
2. **Revisión** con el equipo; al aprobarse pasa a `Aprobada` y se crea el **issue** que la referencia.
3. **Implementación** en una rama `spec/NNN-nombre`: primero las pruebas derivadas de los criterios de aceptación y luego el código.
4. **PR** que cierra el issue; la spec pasa a `Implementada`. Si la implementación obliga a cambiar la spec, primero se actualiza la spec.

## Índice

| Spec | Título | Estado | Prioridad | Issue |
|---|---|---|---|---|
| [001](001-notificacion-acciones-sensibles.md) | Notificación de acciones sensibles a Seguridad | Aprobada | Alta: bloquea el corte | [#1](https://github.com/crisricky7/bytq-fidu/issues/1) |
| [002](002-adaptador-oracle-coexistencia.md) | Adaptador Oracle para coexistencia con el monolito | Aprobada | Alta: bloquea el corte | [#2](https://github.com/crisricky7/bytq-fidu/issues/2) |
| [003](003-contrato-auditoria-v2.md) | Contrato de Auditoría v2 | Aprobada | Media: después del corte | [#3](https://github.com/crisricky7/bytq-fidu/issues/3) |

## Plantilla

Contexto · Objetivo · Alcance / Fuera de alcance · Requisitos · Criterios de aceptación
(Dado / Cuando / Entonces) · Diseño propuesto · Riesgos · Preguntas abiertas · Definición de terminado.
