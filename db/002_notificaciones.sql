-- Esquema del worker de notificaciones (SPEC-001).
-- Registro de idempotencia: un MessageId produce como máximo un correo.

CREATE SCHEMA IF NOT EXISTS notificaciones;

CREATE TABLE IF NOT EXISTS notificaciones.notificacion_enviada (
    message_id    uuid         PRIMARY KEY,
    reservado_at  timestamptz  NOT NULL,
    enviado_at    timestamptz
);

-- Para limpiar registros antiguos por fecha de envío.
CREATE INDEX IF NOT EXISTS ix_notificacion_enviada_enviado_at
    ON notificaciones.notificacion_enviada (enviado_at);
