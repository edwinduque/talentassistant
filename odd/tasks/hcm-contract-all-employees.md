# Contrato HCM: servir todo el catálogo de empleados activos

- **Objetivo**: ampliar el alcance del contrato de vistas `hcm_contract.*` de los 9 fixtures a todos los
  empleados activos del HCM, para que el asistente y la presencia sirvan el catálogo completo, y
  re-aplicar el contrato en la VPS (hoy la vista de empleados de producción es una versión vieja de
  3 columnas incompatible con el conector desplegado y rompe `search_employees` y `upcoming_birthday`).
- **Rama**: `feat/hcm-contract-all-employees` (desde `main` = `101c993`).
- **Estrategia de entrega**: push directo a `main` + `workflow_dispatch` de `.github/workflows/deploy.yml`
  con aprobación del Environment `production` (misma estrategia que la feature `assistant-followups-age`).
- **Runner**: `dotnet test TalentAssistant.sln --configuration Debug --no-restore` con
  `ConnectionStrings__TalentAssistantTests` (puerto 5434) y `HcmPostgres__ConnectionString` (puerto 5433).
- **Forecast de líneas autorizadas**: ~350–450 previsto; **real ~700** (vistas SQL + pruebas +
  documentación + spec), por encima del heurístico de ~400.

## Specs

### S1 — Alcance del catálogo servido

El contrato sirve **todos los empleados activos** de `public.employee`
(`deleted_on IS NULL AND status_id <> 3`), no solo los nueve con
`department LIKE 'office-demo-%'`. Decisión literal del usuario (2026-10-24):
**«Ampliar a los 209 empleados reales»**.

- Los nueve fixtures conservan sus alias actuales (`employee-demo-*`) para no romper
  paridad, presencia, ausencias ni tareas; el resto usa el fallback `'employee-' || id`
  previsto en `db/hcm-contract/views.sql:31` y `specs/016-admin-user-management/spec.md:30`.
- `office_ids` sigue derivándose de `department`. *Supuesto*: `department` NULL produce
  `office_ids` vacío (no `ARRAY[NULL]`).
- **`display_name` nunca NULL** (hallazgo de T1, registrado en L7): 95 de los 197 empleados
  activos de la semilla no tienen `last_name`, así que `e.name || ' ' || e.last_name` daba
  `NULL` y el conector fallaba con `InvalidCastException: Column 'display_name' is null`.
  Derivación nueva: `COALESCE(NULLIF(TRIM(CONCAT(name, ' ', last_name)), ''), 'employee-' || id)`
  en `employees` y `presence`.
- **Aceptación**: `hcm_contract.talentassistant_employees` y `hcm_contract.talentassistant_presence`
  devuelven exactamente los empleados activos; los nueve ids `employee-demo-*` siguen presentes
  con los mismos campos que hoy. *Supuesto*: los empleados borrados/inactivos no se sirven
  (el total servido es el de la tabla base con esos filtros, no 209 literal).
- **Checks**: `PostgresWorkforceSourceParityTests`, `PostgresModeHttpContractTests`,
  `PostgresSourceSafetyTests`.

### S2 — Vista de ausencias escalable

`hcm_contract.talentassistant_timeoff` deja de estar fijada a `t.id IN (1, 2, 3)` y sirve
todas las filas de `public.timeoff` cuyo empleado está activo (583 de las 620 de la semilla
local; ver «Alcance de filas»), con el mismo esquema de columnas que espera el conector
(`PostgresWorkforceSource.cs:138-145`).

- `reference`: los tres fixtures conservan `timeoff-juan-2026-10`,
  `timeoff-pedro-2026-10`, `timeoff-mauricio-2026-10`; el resto `'timeoff-' || id`.
- `type_es`/`type_en`: nombre del catálogo `public.timeoff_type` (7 tipos, SPEC-012 §6)
  con mapeo bilingüe fijo de ese catálogo cerrado. *Supuesto*: traducción fija por tipo.
- `status_es`/`status_en` desde `status_id`: 1 → Pendiente/Pending,
  2 → Aprobada/Approved, 3 → Rechazada/Rejected (lectura de la semilla `db/hcm-real/seed.sql:263`).
- `summary_es` y `summary_en` llevan **el mismo texto**: decisión literal del usuario (2026-10-24):
  **«Mismo texto en summary_es y summary_en»**. *Supuesto*: aplica a las ausencias reales; los
  tres fixtures de paridad conservan sus textos bilingües actuales porque sus `note` son los
  literales de origen y la paridad exige identidad con el fuente sintético.
  `note` NULL → `summary_*` NULL.
- `starts_on`/`ends_before`/`duration`: `date_end` es inclusivo (`ends_before = date_end + 1`);
  `date_end` NULL → `ends_before` y `duration` NULL. *Supuesto*: filas con `by_hour` se tratan
  igual que las demás, en días naturales.
- **Alcance de filas** (supuesto registrado en L7): solo se sirven ausencias cuyo empleado está
  activo (misma regla que S1), para que ningún `employee_id` cuelgue del directorio → 583 de las
  620 filas de la semilla local; las 37 descartadas pertenecen a empleados borrados o inactivos.
- **Aceptación**: servidas exactamente las filas de `public.timeoff` con empleado activo (583/620
  en la semilla local); los tres fixtures devuelven exactamente los mismos valores que hoy; las
  generadas devuelven tipo/estado/resumen del origen.
- **Checks**: `PostgresWorkforceSourceParityTests` (timeoff), `PostgresModeHttpContractTests`,
  `PostgresCatalogScopeTests`.

### S3 — Paridad redefinida (SPEC-011 AC-036 / SPEC-012 §11)

La paridad exacta synthetic ↔ postgres se verifica **sobre los nueve fixtures** (subconjunto por id),
porque `SyntheticWorkforceSource` tiene los nueve hardcodeados y no escala. El catálogo ampliado
tiene sus propias aserciones: total = empleados activos, ids generados presentes, ausencias = total
de la tabla base.

- *Supuesto*: los registros ampliados no se comparan uno a uno con el fuente sintético; solo los
  nueve fixtures mantienen la identidad campo a campo.
- **Aceptación**: ninguna prueba depende de un conteo fijo de 9 para el directorio/presencia; las
  que sí miden el catálogo ampliado calculan el esperado desde la tabla base.
- **Checks**: `PostgresWorkforceSourceParityTests`, `PostgresModeHttpContractTests`,
  `PostgresSourceSafetyTests`.

### S4 — Documentación y spec

SPEC-012 (gap «ampliar el catálogo … es retirar ese filtro»), SPEC-011 AC-036, la cabecera de
`db/hcm-contract/views.sql`, `docs/acceptance/mvp-acceptance-criteria.md`,
`docs/product/vision.md` y `docs/development/local-setup.md` reflejan el nuevo alcance.
Todo en español; identificadores y rutas en inglés.

- **Aceptación**: no queda texto que afirme que el contrato está acotado a nueve fixtures.
- **Checks**: revisión de los fragmentos citados (`grep office-demo` en `specs/` y `docs/`).

### S5 — Migración de producción

Aplicar el `views.sql` nuevo sobre `talentassistant_hcm` de la VPS y verificar que el asistente
responde en la UI de producción. Si algún `CREATE OR REPLACE VIEW` no puede sustituir una vista
vieja por cambio de columnas, se hace `DROP VIEW` en orden inverso de dependencias
(`talentassistant_attendance_insights` → `talentassistant_employees`) y se re-aplica.

- *Supuesto*: los scripts de la VPS los ejecuta el usuario; el asistente no abre SSH.
- **Aceptación**: `SELECT count(*)` de la vista = empleados activos; chat y presencia responden.

## Tasks

| ID | Specs | Ruta | Estado | Commit |
|----|-------|------|--------|--------|
| T1 | S1, S2, S3 | inline | Implementada; RED observado: 12 fallos por alcance | `9a71b8f` |
| T2 | S1, S2, S3 | inline; trigger: unidad ya entendida | Implementada; `views.sql` aplicada en local, GREEN 68/68 y suite 185/185 | `9a71b8f` |
| T3 | S4 | inline | Implementada; specs/docs en español actualizados | `5070c50` |
| T4 | S1, S2, S3 | inline | Implementada; suite 185/185, vivo 14/14 y guardrails 7/7 (EXIT=0) | `9a71b8f` + `5070c50` |
| T5 | S5 | inline | pendiente (comandos de VPS y dispatch de `deploy.yml`) | — |

Forecast real de líneas autorizadas: **~700** (450 en ficheros modificados + los dos ficheros
nuevos), por encima del heurístico de ~400 → se requiere la pregunta única de estrategia de
entrega antes del primer commit (pendiente, ver L12).

- **T1** — Pruebas del nuevo alcance en RED: subconjunto de paridad sobre los nueve fixtures,
  totales calculados desde la tabla base, y aserciones de catálogo ampliado (directorio, presencia,
  ausencias). Ruta inline (sin trigger de delegación: unidad única ya entendida).
- **T2** — `db/hcm-contract/views.sql`: retirar el filtro `office-demo-%` de `employees` y `presence`,
  fallback `'employee-' || id`, y rehacer `timeoff` sobre todas las filas (S2). Aplicar en local y
  pasar T1 a GREEN.
- **T3** — Spec/docs en español (S4).
- **T4** — Verificación: suite completa, banco vivo `scripts/verify-assistant-questions.ps1` y
  `scripts/verify-assistant-guardrails.ps1` contra la API local (procedimiento documentado en
  `local-setup.md`, fuente sintética; la ruta de datos Postgres queda cubierta por
  `PostgresModeHttpContractTests` + `PostgresCatalogScopeTests`, ver L10) y comprobación de que
  ninguna prueba quedó huérfana.
- **T5** — Migración de producción con el usuario al mando de los comandos de la VPS (S5),
  push y dispatch de `deploy.yml` con autorización explícita.

## Log

- **L1** (petición original del usuario, 2026-10-25, texto literal del síntoma en producción):

  > En prod
  >
  > Cuantos empleados tiene la empresa
  > Lo siento, pero la información sobre el número total de empleados no está disponible en este momento. Si necesita ayuda con otra consulta de recursos humanos, estaré encantado de asistirle.
  >
  > Fuentes: search_employees
  >
  > quien cumple años pronto?
  >
  > Lo siento, pero la información sobre los próximos cumpleaños no está disponible en este momento. Si necesita ayuda con otra consulta de recursos humanos, estaré encantado de asistirle.
  >
  > Fuentes: upcoming_birthday

- **L2** (2026-10-25) Diagnóstico: en la VPS `Hcm__Source=Postgres` contra
  `127.0.0.1:5432/talentassistant_hcm`; `\d hcm_contract.talentassistant_employees` devuelve
  **3 columnas** (`employee_id`, `display_name`, `office_ids`) mientras el conector desplegado
  selecciona 8 (`PostgresWorkforceSource.cs:36-38`, falta `job_title`, `last_name`, `hire_date`,
  `age_on_next_birthday`) → *column does not exist* por cada herramienta, excepción tragada en
  `ChatOrchestrator.cs:320-323` → «no está disponible». La vista de presencia sí tiene la forma
  actual. La vista vieja devuelve 197 filas: es una versión anterior a la del repo (que filtra a 9).
- **L3** (2026-10-25) Decisión de producto del usuario, respuesta literal a la pregunta de alcance:
  **«Ampliar a los 209 empleados reales»** (frente a «Aplicar el views.sql del repo (9 fixtures)»).
- **L4** (2026-10-25) Decisión de producto del usuario, respuesta literal sobre el resumen de
  ausencias reales: **«Mismo texto en summary_es y summary_en»**.
- **L5** (2026-10-25) Mapeo con `gentle-ai-explore`: el alcance vive en `db/hcm-contract/views.sql`
  (filtro `L106`/`L132`, CASE de alias `L56-66`) y en `specs/012-hcm-real-schema-data/spec.md:124`;
  la vista `timeoff` no escala (reference/type/status/summary son CASE por `t.id` con literales,
  `views.sql:171-203`); el conector y Application no filtran fixtures; las pruebas que rompen son
  `PostgresWorkforceSourceParityTests` (directorio, apellido «z», cumpleaños ×2, presencia),
  `PostgresModeHttpContractTests` (5 aserciones de conteo) y `PostgresSourceSafetyTests:81`.
- **L6** (2026-10-25) T1 — RED observado antes de tocar las vistas: **12 fallos** todos atribuibles
  al alcance (`DirectoryParity`, `PresenceParity`, `TimeOffParity`, `NextBirthday…`, «z», 5 conteos
  HTTP, 2 de seguridad). Corregido un assert mío que comparaba `HcmEmployeeDirectoryEntry` con
  `Assert.Contains` (record equality compara `OfficeIds` por referencia): la comparación es por
  id y después campo a campo.
- **L7** (2026-10-25) T2 — `views.sql` reescrita y aplicada en local (`/contract/views.sql`,
  `ON_ERROR_STOP=1`, 5 `CREATE OR REPLACE`): 197 empleados activos, presencia hoy 197, ausencias
  583/620. Hallazgos que obligaron a corregir el diseño previsto:
  1. `display_name` `NULL` para los 95 activos sin `last_name` → `InvalidCastException` en el
     conector; derivación con `TRIM(CONCAT(...))` + fallback al id (aceptación de S1).
  2. `type_es` pasaba de `text` a `varchar` → `cannot change data type of view column`; cast
     `tt.name::text` y `t.note::text` para preservar el shape.
  3. `timeoff` se sirve **solo de empleados activos** (JOIN con `public.employee` + mismos
     filtros), 583 filas, para que ningún `employee_id` quede fuera del directorio.
- **L8** (2026-10-25) T3 — documentación en español: cabecera de `views.sql`, SPEC-012 §11 (gap
  sustituido por la ampliación), SPEC-011 AC-036 (paridad acotada a los nueve fixtures),
  `mvp-acceptance-criteria.md` (AC-036 con la evidencia nueva 185/185), `local-setup.md` y
  `vision.md`. Además `ChatOrchestrator.cs:54`: el texto del catálogo de herramientas pasa a
  nombrar «an exact value such as office-demo-tokyo **or a department name like Ingeniería**»,
  porque con 197 empleados el filtro `office` ya no es solo un id de oficina (ninguna prueba
  asserta ese texto).
- **L9** (2026-10-25) T4 — verificación: suite completa **185/185** (11 unit + 174 integración),
  `git diff --check` limpio, sin pruebas huérfanas (`ByReference`, `ReadDatabaseCurrentDateAsync`
  y el nuevo `ReadContractEmployeesAsync` siguen usados).
- **L10** (2026-10-25) T4 — banco vivo: API `talentassistant-api:local` reconstruida con el texto
  de herramientas nuevo; `verify-assistant-questions.ps1 -PaceSeconds 15` → **14/14 PASS,
  EXIT=0**; `verify-assistant-guardrails.ps1 -PaceSeconds 15` → **7/7 PASS, EXIT=0**.
  *Límite honesto*: los probes de identidad («próximo cumpleaños», «no han llegado») son
  dependientes del catálogo y se cumplen sobre el fuente sintético (9 fixtures); en modo Postgres
  con 197 empleados el siguiente cumpleaños ya no es Mauricio, por eso AC-029 sigue midiendo el
  catálogo de fixtures y la ruta de datos Postgres del conector queda cubierta por
  `PostgresModeHttpContractTests` y `PostgresCatalogScopeTests`.
- **L11** (2026-10-25) El botón del asistente «Cuantos empleados tiene la sede de Tokyo?» sigue
  respondiendo 4: los 200 empleados generados llevan nombres de departamento reales, nunca
  `office-demo-*`, así que el filtro por oficina no cambia de significado.
- **L12** (2026-10-25) Pregunta única de estrategia de entrega resuelta (texto de la respuesta del
  usuario): **«Push directo a main (Recomendado)»** — commits work-unit en
  `feat/hcm-contract-all-employees`, merge fast-forward a `main`, push a `origin/main` y después
  `workflow_dispatch` de `.github/workflows/deploy.yml` con aprobación del Environment
  `production`; sin PR. El diff acumulado (~700 líneas autorizadas) supera el heurístico de ~400,
  de ahí la pregunta.
- **L13** (2026-10-25) Commits work-unit en `feat/hcm-contract-all-employees` (desde `main` =
  `101c993`): `9a71b8f` `feat(hcm): serve every active employee from the contract views`
  (vistas + pruebas + texto del catálogo de herramientas), `5070c50`
  `docs(hcm): record the widened contract scope in specs and guides`, `af537c6`
  `docs(odd): add hcm-contract-all-employees feature log`; este documento cierra en el commit
  siguiente. Sin pushes todavía.
