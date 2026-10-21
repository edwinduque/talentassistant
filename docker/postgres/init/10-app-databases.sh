#!/bin/bash
set -euo pipefail

# SPEC-013 S6: create the dedicated tests database and the least-privilege
# application role. The dev database itself is created by the postgres
# image entrypoint (POSTGRES_DB). The admin role (POSTGRES_USER) only runs
# this bootstrap; the application connects as talentassistant_app.
psql -v ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set app_password="$APP_POSTGRES_PASSWORD" \
  <<-SQL
CREATE ROLE talentassistant_app LOGIN PASSWORD :'app_password';
ALTER DATABASE "$POSTGRES_DB" OWNER TO talentassistant_app;
CREATE DATABASE talentassistant_tests OWNER talentassistant_app;
SQL

# EF Core migrations need CREATE rights on schema public of both databases;
# ownership of the database grants it on PostgreSQL 15+.
for db in "$POSTGRES_DB" talentassistant_tests; do
  psql -v ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname "$db" \
    -c 'ALTER SCHEMA public OWNER TO talentassistant_app;'
done
