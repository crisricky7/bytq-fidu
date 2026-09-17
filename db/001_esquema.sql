-- Esquema del microservicio de Auditoría (PostgreSQL).
-- Equivalente Oracle: T01AUDITORIA + SQ01AUDITORIA. Los tipos se eligieron para que la
-- traducción sea directa (bigint -> NUMBER(19), varchar -> VARCHAR2, text -> CLOB,
-- timestamptz -> TIMESTAMP WITH TIME ZONE).

CREATE SCHEMA IF NOT EXISTS auditoria;

CREATE SEQUENCE IF NOT EXISTS auditoria.sq_registro_auditoria;

CREATE TABLE IF NOT EXISTS auditoria.registro_auditoria (
    id                  bigint       PRIMARY KEY,
    codigo_empresa      varchar(4)   NOT NULL,
    codigo_modulo       varchar(2),
    codigo_transaccion  varchar(10),
    usuario             varchar(30)  NOT NULL,
    entidad             varchar(60),
    clave_entidad       varchar(60)  NOT NULL,
    accion              char(1)      NOT NULL CHECK (accion IN ('C', 'U', 'D', 'Q')),
    valor_anterior      text,
    valor_nuevo         text,
    direccion_ip        varchar(45),
    canal               varchar(5)   NOT NULL CHECK (canal IN ('WEB', 'API', 'BATCH')),
    fecha_registro      timestamptz  NOT NULL,
    -- Invariantes duplicadas como red de seguridad ante escrituras que no pasen por el dominio.
    CONSTRAINT ck_actualizacion_con_valor_anterior CHECK (accion <> 'U' OR coalesce(valor_anterior, '') <> ''),
    CONSTRAINT ck_batch_no_registra_consultas CHECK (NOT (canal = 'BATCH' AND accion = 'Q'))
);

-- La consulta siempre filtra por empresa y ordena por fecha descendente.
CREATE INDEX IF NOT EXISTS ix_registro_auditoria_empresa_fecha
    ON auditoria.registro_auditoria (codigo_empresa, fecha_registro DESC);
CREATE INDEX IF NOT EXISTS ix_registro_auditoria_empresa_usuario_fecha
    ON auditoria.registro_auditoria (codigo_empresa, usuario, fecha_registro DESC);

-- Append-only: la tabla no admite UPDATE ni DELETE, ni siquiera por error de la aplicación.
CREATE OR REPLACE FUNCTION auditoria.fn_registro_inmutable() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'registro_auditoria es append-only (% no permitido)', TG_OP;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_registro_inmutable ON auditoria.registro_auditoria;
CREATE TRIGGER tg_registro_inmutable
    BEFORE UPDATE OR DELETE ON auditoria.registro_auditoria
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registro_inmutable();

CREATE TABLE IF NOT EXISTS auditoria.outbox_mensaje (
    id               uuid         PRIMARY KEY,
    tipo             varchar(200) NOT NULL,
    payload          jsonb        NOT NULL,
    occurred_at      timestamptz  NOT NULL,
    trace_parent     varchar(55),
    published_at     timestamptz,
    attempts         integer      NOT NULL DEFAULT 0,
    next_attempt_at  timestamptz  NOT NULL,
    last_error       varchar(2000)
);

-- Índice parcial: el dispatcher solo recorre pendientes, que deberían ser pocas filas.
CREATE INDEX IF NOT EXISTS ix_outbox_mensaje_pendientes
    ON auditoria.outbox_mensaje (next_attempt_at)
    WHERE published_at IS NULL;
