# Feature: postgres-only-persistence

Objetivo: migrar toda la persistencia propia de la app (identidad + asistencia sintética) de MySQL a PostgreSQL y eliminar MySQL del stack. Documentado como SPEC-013 + ADR-0013 (sustituye a ADR-0004).
Branch: `feat/postgres-only-persistence` (base: `feat/hcm-real-schema-data` @ `3ac1ca8`). Entrega: `ask-on-risk`. Runner: `dotnet test` (Compose local con Postgres 17).

## Specs

- **S1** (usuario, verbatim): «solo vamos a trabajar con postgres no mysql». Único motor: Npgsql.EntityFrameworkCore.PostgreSQL reemplaza `MySql.EntityFrameworkCore` + `MySql.Data` en Infrastructure e IntegrationTests. Aceptación: 0 referencias a paquetes MySQL en todos los `.csproj` (grep verificado).
- **S2**: `UseNpgsql` en `TalentAssistantDbContext` (`DependencyInjection.cs:38`) y `TalentAssistantIdentityDbContext` (`:126`), misma cadena `ConnectionStrings:TalentAssistant` para ambos.
- **S3**: migraciones EF regeneradas para Postgres: 4 de Infrastructure + 1 de probe de IntegrationTests + 2 snapshots, sin anotaciones MySQL (`MySQL:Charset`, `MySQL:ValueGenerationStrategy`). IDs enteros generados con identidad nativa de Postgres.
- **S4**: `HasColumnType("longtext")` (`TalentAssistantDbContext.cs:25`) sustituido por el tipo PostgreSQL fiel con respecto a la serialización real de `PayloadJson` (decidir leyendo el código: `text` si JSON UTF-8, `bytea` si binario puro; registrar elección en Log).
- **S5**: sin símbolos/config heredados de MySQL: `MySqlHcmAttendanceSource`, `MySqlDatabaseAvailabilityCheck` y el valor `Attendance:Source=MySql` se renombran a equivalentes PostgreSQL/agnósticos. El cambio del valor de configuración es interno (compose/scripts/docs se actualizan en el mismo conjunto; sin consumidores externos). El check polimórfico `IDatabaseAvailabilityCheck` y el catch de `DbException` se conservan (compatibles con Npgsql).
- **S6**: Compose: ningún servicio/imagen/digest MySQL (fuera `docker/mysql/`). Bd propia en Postgres 17-alpine con dos bases (`talentassistant_dev` y `talentassistant_tests`) y usuarios de menor privilegio; healthcheck `pg_isready`; `db-init` re-cableado; volumen nuevo (reset local aceptado: datos sintéticos). `hcm-postgres` sin cambios.
- **S7**: suite completa contra Postgres: integración verde (132/132 registrada como última corrida; ± pruebas renombradas), persistencia probe en `talentassistant_tests` verificando servidor Postgres 17, unit tests verdes salvo el fallo ambiental conocido `0x800711C7` (`FileLoadException` intermitente al cargar `TalentAssistant.Domain.dll`), documentado y sin eludir.
- **S8**: `scripts/verify-database-secret-redaction.ps1` con cadena laudable PostgreSQL; init SQL de Postgres crea bases y GRANTs de menor privilegio (equivalente al rol que hoy hace `docker/mysql/init/10-create-test-database.sql`).
- **S9**: documentación: SPEC-013 + ADR-0013 (Npgsql, licencia MIT, sustituye ADR-0004); ADR-0004 → estado «Sustituido» con referencia cruzada; `decision-register.md` actualizado (fila + línea base + retirada de la nota de licencia Oracle); README, `local-setup.md`, ADR-0006/0007/0008/0009/0012 ajustados; SPEC-001/002/004/005/006/011/012 reciben nota de herencia sin reescribir su contenido histórico (**supuesto**: no reescribir specs aprobadas viejas).

## Tasks

| ID | Specs | Ruta | Estado | Commit |
|---|---|---|---|---|
| T1 | S9 | inline | completado | pendiente de commit — SPEC-013 + ADR-0013 + ADR-0004 Sustituido + decision-register |
| T2 | S1,S2,S3,S4,S5 | inline | pendiente | — código: paquetes Npgsql, UseNpgsql ×2, renames, config, migraciones regeneradas, probe |
| T3 | S6,S8 | inline | pendiente | — infra: compose, docker/postgres, .env.example, script redacción |
| T4 | S7 | inline | pendiente | — suite completa GREEN contra Postgres; evidencia en Log |
| T5 | S9 | inline | pendiente | — README, local-setup, ADRs heredados, notas en specs |
| T6 | cierre | inline | pendiente | — checks diferenciales, log final, assess de revisión |

## Log

- **L1** (2026-10-21, usuario, verbatim): «solo una correccion que debemos hacer en la documentacion, solo vamos a trabajar con postgres no mysql». Tras presentar la evidencia (MySQL vive en código, no solo en docs) se presentaron 2 caminos: (1) migración completa a PostgreSQL [recomendado, evita dos motores en producción dado que la VPS del usuario ya usa postgres] o (2) postgres de aquí en adelante con MySQL congelado como legado. El usuario eligió: **«1»** → migración completa.
- **L2** (2026-10-21): feature creada tras mapeo `gentle-ai-explore` (handoff con path:line en la sesión; 10 riesgos detectados, incluidos: anotaciones MySQL en 4 migraciones + 1 probe, cadena compartida de los dos contextos, `verify-database-secret-redaction.ps1` laudable MySQL, HcmPostgresModeApiFactories pasa cadena MySQL a identidad, ArchitectureBoundaryTests con gate de proveedor).
- **L3** (2026-10-21): política test-first: sin RED clásico posible para un swap de proveedor (no existe Postgres propio antes del switch). La red de regresión es la suite existente de integración (132/132) contra Compose Postgres; excepción documentada en SPEC-013 §Verificación.
