# SPEC-013 — Persistencia unificada en PostgreSQL y retirada de MySQL

- **Estado:** Aprobado (2026-10-21). El usuario eligió el camino 1 (migración completa) tras ver la evidencia.
- **Rama de implementación:** `feat/postgres-only-persistence` (base `feat/hcm-real-schema-data` @ `3ac1ca8`).
- **Requisitos relacionados:** sin FR ni NFR nuevos de producto; actualiza la línea base técnica de persistencia de la fundación sin tocar el contenido de FR/NFR existentes.
- **Decisión arquitectónica:** ADR-0013 (sustituye a ADR-0004).
- **Decisión de producto registrada:** el usuario aportó la corrección con estas palabras: “solo una correccion que debemos hacer en la documentacion, solo vamos a trabajar con postgres no mysql”. Ante la evidencia de que MySQL vive en el código (no solo en documentos), se presentaron dos caminos y el usuario respondió: “1” (migración completa a PostgreSQL, eliminando MySQL del stack).
- **Versión:** 0.1.

## 1. Objetivo

El motor de la persistencia propia de Talent Assistant (identidad + asistencia sintética) pasa de MySQL 8.4.11 a **PostgreSQL 17**, con el proveedor EF Core `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`. MySQL deja de existir en dependencias, Compose, imágenes Docker, scripts, cadenas de conexión, símbolos de código y documentación operativa. El HCM PostgreSQL (SPEC-011/012) no cambia.

## 2. Alcance

### Incluido

- Sustitución de `MySql.EntityFrameworkCore 10.0.9` + `MySql.Data 26.7.0` por `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` (con `Npgsql 10.0.3`, ya referenciado) en `src/TalentAssistant.Infrastructure` y `tests/TalentAssistant.IntegrationTests`.
- `UseNpgsql` en los dos contextos propios (`TalentAssistantDbContext`, `TalentAssistantIdentityDbContext`), conservando la cadena `ConnectionStrings:TalentAssistant` compartida.
- Regeneración completa de las migraciones EF para PostgreSQL: las 4 de Infrastructure, la de probe de IntegrationTests y los 2 snapshots. Sin anotaciones `MySQL:Charset` ni `MySQL:ValueGenerationStrategy`; IDs enteros con identidad nativa de PostgreSQL. **El volumen local actual se descarta**: es desarrollo con datos sintéticos y se reconstruye con Compose.
- Sustitución del `HasColumnType("longtext")` de `PayloadJson` por el tipo PostgreSQL fiel con respecto a la serialización real (elección registrada en el registro ODD: `text` si JSON UTF-8, `bytea` si binario puro).
- Renombrado de símbolos/config heredados de MySQL: `MySqlHcmAttendanceSource`, `MySqlDatabaseAvailabilityCheck` y el valor `Attendance:Source=MySql`. El valor de configuración se llama exactamente igual en todos los puntos que lo consumen (compose, Program, factories de test, docs). Fallo controlado al arrancar si aparece el valor antiguo.
- `compose.yaml`: servicio propio de PostgreSQL (`postgres:17-alpine`) con bases `talentassistant_dev` y `talentassistant_tests`, usuarios de menor privilegio, healthcheck `pg_isready`, volumen nuevo. Eliminación de `docker/mysql/` (Dockerfile e init SQL). `hcm-postgres` (5433) intacto; la base propia usa un puerto local distinto para evitar colisiones (5434, confirmado contra el entorno).
- `db-init` re-cableado a la cadena PostgreSQL; script de verificación de redacción de secretos (`scripts/verify-database-secret-redaction.ps1`) con cadena laudable PostgreSQL.
- Documentación: README, `docs/development/local-setup.md`, ADR-0006/0007/0008/0009/0012 (ajuste o herencia), registro de decisiones, y notas de herencia en SPEC-001/002/004/005/006/011/012 sin reescribir su contenido histórico.

### Fuera de alcance

- Sin cambios en Application, Domain, Web ni contratos de puertos: si algo de eso necesita tocar para este cambio, el diseño está mal (pararlo y señalarlo).
- Sin migración de datos: el volumen MySQL local se descarta; los datos son sintéticos y reproducibles.
- Sin cambios en el HCM PostgreSQL (vistas, seed, fuentes) y sin conexión al HCM real.
- Sin despliegue a producción (la topología de despliegue queda para la SPEC de CI/CD, NFR-009 pendiente de asignación) y sin CI.
- Sin ADR nuevo más allá de ADR-0013; sin cambios en ADR-0011/0012.

## 3. Requisitos y límites

- **0 referencias a MySQL en los `.csproj`** (grep verificado como criterio de aceptación) y 0 anotaciones `MySQL:` en migraciones.
- La suite de integración completa corre contra PostgreSQL (bases `talentassistant_dev`/`talentassistant_tests`) y permanece verde: última corrida registrada 132/132 (± pruebas renombradas); el único fallo admisible es el ambiental conocido `0x800711C7` (`FileLoadException` intermitente al cargar `TalentAssistant.Domain.dll` en UnitTests), documentado en los registros ODD y sin eludir.
- `ArchitectureBoundaryTests` mantiene su gate: prohíbe referencias a proveedores de EF fuera de Infrastructure (actualiza la lista: entra `Npgsql.EntityFrameworkCore.PostgreSQL` en la excepción de Infrastructure, salen los paquetes MySQL).
- Las cadenas de conexión y credenciales siguen en `.env`/User Secrets (regla 5 de ADR-0004, conservada en ADR-0013); el init SQL de PostgreSQL crea roles de menor privilegio por base.
- `AttendanceSourceUnavailableException` conserva su contrato: el check polimórfico de disponibilidad y el catch de `DbException` son agnósticos del proveedor y no cambian de firma.
- Todo el trabajo es PostgreSQL; no se introduce ningún servicio MySQL nuevo (vigilado por el criterio de aceptación del compose).

## 4. Verificación

- Política test-first: **excepción documentada** — no existe RED clásico para un cambio de proveedor (no hay base PostgreSQL propia antes del switch). La red de regresión es la suite existente: la paridad funcional la demuestran las mismas pruebas de SPEC-002..009 corriendo en verde contra PostgreSQL.
- Criterios de aceptación observables: suite de integración verde; `/health` y `/health/database` responden OK contra postgres; `db-init` siembra el dataset sintético con éxito; `grep MySql` en `.csproj` devuelve 0 hits; `grep -i mysql compose.yaml docker/` devuelve 0 hits.
- Fallo ambiental conocido (no bloqueante de la sesión, sí documentado): `0x800711C7` en UnitTests; se re-ejecuta si aparece y se registra la evidencia.

## 5. Riesgos y decisiones de diseño

- **Tipo de columna de `PayloadJson`**: decidir leyendo la serialización (§2); la lectura la realiza la propia aplicación deserializando en C#, así que el tipo PostgreSQL es transparente para el dominio.
- **Colisión de nombres con las fuentes HCM Postgres**: `PostgresAttendanceSource` (HCM, SPEC-011) ya existe; el renombrado evita crear confusión (por ejemplo, el nombre elegido para la fuente del dataset sintético sobre la BD propia se registra en el registro ODD).
- **Valor de configuración `Attendance:Source`**: el valor antiguo (`MySql`) es en su lugar inválido; la validación de arranque lo rechaza con mensaje claro. Es un cambio interno de configuración, sin consumidores fuera del repositorio.
- **Puerto local de la BD propia** (5434): evitar colisión con 5432 (host) y 5433 (HCM); confirmado contra el entorno local antes de cerrar T3.
- **Volumen de Compose**: nombre nuevo para la BD propia (distinto del `mysql_data` heredado); el descarte del volumen MySQL se anota en la guía local.
