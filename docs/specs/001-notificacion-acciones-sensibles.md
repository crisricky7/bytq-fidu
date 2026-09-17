# SPEC-001 — Notificación de acciones sensibles a Seguridad

- **Estado:** Aprobada
- **Issue:** [#1](https://github.com/crisricky7/bytq-fidu/issues/1)
- **Prioridad:** Alta (bloquea el corte de `/api/auditoria/**`)
- **Relacionado:** DECISIONES.md §1 (#1), §3, §4

## Contexto

Hoy `BAuditoria.Registrar` envía un correo a `seguridad@corefid.com.ec` dentro de la transacción cuando la acción es sensible. El microservicio ya no lo hace: marca `esAccionSensible` en el evento `AuditoriaRegistrada` y lo publica vía outbox en el exchange `corefid.auditoria` (routing key `auditoria.registrada`). Sin un consumidor, las acciones que atienda el microservicio durante el canary **no notifican a Seguridad**.

## Objetivo

Recuperar la notificación a Seguridad fuera del camino transaccional, sin duplicados visibles y sin perder avisos.

## Alcance

- Consumidor de `auditoria.registrada` que filtra `esAccionSensible = true` y envía el correo.
- Idempotencia por `MessageId`.
- Reintentos y cola de mensajes muertos (DLQ).

**Fuera de alcance:** cambiar la regla de qué es sensible (vive en el dominio), otros canales de aviso (Teams, SIEM) y plantillas administrables.

## Requisitos

- **R1.** Cuando llega un `AuditoriaRegistrada` con `esAccionSensible = true`, el consumidor debe enviar un correo con usuario, entidad, clave, acción, fecha (hora de Ecuador) y valores anterior/nuevo.
- **R2.** Si `esAccionSensible = false`, el mensaje se confirma (ack) sin enviar nada.
- **R3.** El mismo `MessageId` nunca produce dos correos, aunque el mensaje llegue varias veces (la entrega del outbox es at-least-once).
- **R4.** Todo valor del evento se **escapa como HTML** antes de ir al cuerpo del correo.
- **R5.** Si el SMTP falla, el mensaje se reintenta con backoff; tras N intentos pasa a la DLQ `corefid.auditoria.notificaciones.dlq` y se registra un error con `traceId`.
- **R6.** El consumidor continúa la traza con la cabecera `traceparent` del mensaje.
- **R7.** El destinatario y el servidor SMTP son configuración; las credenciales vienen de un Secret.

## Criterios de aceptación

1. **Dado** un evento sensible (acción `D`), **cuando** se consume, **entonces** se envía exactamente un correo a Seguridad y el mensaje se confirma.
2. **Dado** el mismo mensaje entregado dos veces, **cuando** se consume la segunda vez, **entonces** no se envía otro correo y el mensaje se confirma.
3. **Dado** `valorNuevo = "<script>alert(1)</script>"`, **cuando** se construye el correo, **entonces** el cuerpo contiene el texto escapado.
4. **Dado** un SMTP caído, **cuando** se agotan los reintentos, **entonces** el mensaje queda en la DLQ, no se pierde y hay un log de error con `traceId`.
5. **Dado** un evento no sensible, **cuando** se consume, **entonces** no se envía correo.

## Diseño propuesto

- Nuevo proyecto `Auditoria.Notificaciones` (worker) con `BackgroundService` sobre RabbitMQ.Client: cola durable `corefid.auditoria.notificaciones` enlazada a `auditoria.registrada`, `prefetch` limitado y ack manual.
- Tabla `notificacion_enviada(message_id PK, enviado_at)`. Se inserta **antes** de confirmar el mensaje; un conflicto de PK significa "ya enviado" y se hace ack. El orden es: reservar `message_id`, enviar, confirmar la reserva, ack. Si el envío falla, se libera la reserva.
- `IEnviadorCorreo` con una implementación SMTP y un doble para pruebas.
- Pruebas: unitarias del armado y escape del correo; integración con Testcontainers (RabbitMQ + PostgreSQL) y un servidor SMTP de prueba (smtp4dev/MailHog).

## Riesgos

- Queda una ventana mínima de duplicado si el proceso muere entre enviar y confirmar la reserva. Se acepta: es mejor un correo repetido que uno perdido. Se documenta.
- Un volumen alto de acciones sensibles (batch nocturno) podría saturar el SMTP: limitar la concurrencia.

## Preguntas abiertas

- ¿Seguridad prefiere un correo por evento o un resumen por intervalo para el batch?
- ¿El módulo `06` (Fondos) sigue siendo sensible completo o solo ciertas entidades?

## Definición de terminado

Criterios 1–5 cubiertos por pruebas en verde, worker en `docker compose`, manifiesto K8s, y el panel del canary con "correos enviados contra acciones sensibles registradas".
