#!/bin/sh
# Inicialización local de la base de pruebas del contrato HCM (SPEC-011 S1).
# Orden explícito: tablas locales de sustitución -> vistas del contrato ->
# semilla sintética -> roles -> contraseña del rol de aplicación (desde
# entorno, nunca en el repo) -> grants de solo lectura. Se ejecuta una sola
# vez, en el primer arranque del volumen (docker-entrypoint-initdb.d).
# SEC-003 (security-review 2026-10-19): pass the statement on stdin with the
# password as a psql variable (:'var'). psql interpolates variables only in
# file/stdin input - not inside -c - and stdin binding means a quote in the
# value can never break or inject SQL, even locally.
# Line endings MUST stay LF (see .gitattributes: *.sh text eol=lf): a CRLF
# shebang fails in the Linux container with "/bin/sh^M: bad interpreter".
set -e

# SPEC-012 S1: forma real del HCM (db/hcm-real/schema.sql) convive con las
# tablas de sustitución hcm.* hasta el recableado de vistas de T3; el orden
# mantiene el contrato actual en verde entre tareas.
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /real/schema.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /real/seed.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/local/local_tables.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/views.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/local/local_seed.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/roles.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
    -v app_password="$HCM_POSTGRES_APP_PASSWORD" <<'EOSQL'
ALTER ROLE talentassistant_hcm_app WITH PASSWORD :'app_password';
EOSQL
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/grants.sql

echo "hcm contract initialized (synthetic local seed, read-only grants)"
