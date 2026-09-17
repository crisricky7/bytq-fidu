#!/usr/bin/env sh
# Genera .env con secretos aleatorios para el entorno local. No sobrescribe uno existente.
set -eu
cd "$(dirname "$0")/.."

if [ -f .env ]; then
  echo ".env ya existe; no se modifica."
  exit 0
fi

aleatorio() { openssl rand -hex "$1"; }

{
  echo "POSTGRES_USER=audit"
  echo "POSTGRES_PASSWORD=$(aleatorio 16)"
  echo "RABBITMQ_USER=auditoria"
  echo "RABBITMQ_PASSWORD=$(aleatorio 16)"
  echo "JWT_CLAVE_FIRMA=$(aleatorio 32)"
} > .env

echo ".env generado."
