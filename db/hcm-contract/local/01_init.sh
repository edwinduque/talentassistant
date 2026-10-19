#!/bin/sh
# Inicialización local de la base de pruebas del contrato HCM (SPEC-011 S1).
# Orden explícito: tablas locales de sustitución -> vistas del contrato ->
# semilla sintética -> roles -> contraseña del rol de aplicación (desde
# entorno, nunca en el repo) -> grants de solo lectura. Se ejecuta una sola
# vez, en el primer arranque del volumen (docker-entrypoint-initdb.d).
set -e

psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/local/local_tables.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/views.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/local/local_seed.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/roles.sql
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
    -c "ALTER ROLE talentassistant_hcm_app WITH PASSWORD '${HCM_POSTGRES_APP_PASSWORD}';"
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /contract/grants.sql

echo "hcm contract initialized (synthetic local seed, read-only grants)"
