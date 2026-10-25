# Contrato HCM: servir todo el catálogo de empleados activos

- **Objetivo**: ampliar el contrato HCM a todos los empleados activos, generar datos de demostración
  ficticios pero verosímiles a escala de cientos/miles, y cargar ese conjunto de forma segura en la
  VPS para que el asistente consulte personas, marcajes, ausencias y tareas.
- **Rama**: `feat/hcm-production-like-demo-data` (desde `main` = `8ebd026`).
- **Estrategia de entrega**: push directo a `main` (elección del usuario en L12). Esta ampliación es
  de seed/vistas; el conector Postgres desplegado no cambia, por lo que no se despacha otro deploy.
  El usuario aplica el bundle de datos/vistas en la VPS.
- **Runner**: `dotnet test TalentAssistant.sln --configuration Debug --no-restore` con
  `ConnectionStrings__TalentAssistantTests` (puerto 5434) y `HcmPostgres__ConnectionString` (puerto 5433).
- **Forecast de líneas autorizadas**: ~1.000–1.400 (semillas, generador VPS idempotente, pruebas,
  fixtures, scripts vivos y documentación); supera el heurístico de ~400.

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
- Los nombres visibles de las cinco filas `Empleado sintético 001..005` se sustituyen por nombres
  ficticios plausibles; los 200 generados reciben apellido no vacío. Los alias estables de los nueve
  fixtures (`employee-demo-*`) y sus hechos funcionales se conservan para mantener las pruebas de
  paridad y el banco de preguntas.
- **`display_name` nunca NULL** (hallazgo de T1, registrado en L7): 95 de los 197 empleados
  activos de la semilla no tienen `last_name`, así que `e.name || ' ' || e.last_name` daba
  `NULL` y el conector fallaba con `InvalidCastException: Column 'display_name' is null`.
  Derivación nueva: `COALESCE(NULLIF(TRIM(CONCAT(name, ' ', last_name)), ''), 'employee-' || id)`
  en `employees` y `presence`.
- **Aceptación**: `hcm_contract.talentassistant_employees` y `hcm_contract.talentassistant_presence`
  devuelven exactamente los empleados activos; los nueve ids `employee-demo-*` siguen presentes
  con los mismos hechos funcionales, salvo los nombres ficticios aprobados. *Supuesto*: los
  empleados borrados/inactivos no se sirven
  (el total servido es el de la tabla base con esos filtros, no 209 literal).
- **Checks**: `PostgresWorkforceSourceParityTests`, `PostgresModeHttpContractTests`,
  `PostgresSourceSafetyTests`.

### S2 — Vista de ausencias escalable

`hcm_contract.talentassistant_timeoff` deja de estar fijada a `t.id IN (1, 2, 3)` y sirve
todas las filas de `public.timeoff` cuyo empleado está activo; el dataset local y el de la VPS
alcanzan al menos 2.000 ausencias visibles, con el mismo esquema de columnas que espera el conector
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
- **Alcance de filas** (supuesto registrado en L7, sobre la semilla previa a S6): solo se sirven ausencias cuyo empleado está
  activo (misma regla que S1), para que ningún `employee_id` cuelgue del directorio → 583 de las
  620 filas de la semilla anterior; las 37 descartadas pertenecían a empleados borrados o inactivos.
- **Aceptación**: al menos 2.000 filas visibles para empleados activos; los tres fixtures conservan
  sus referencias y hechos de paridad; las generadas devuelven tipo/estado/resumen del origen.
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

Aplicar el contrato nuevo y el fichero de carga de datos verosímiles sobre `talentassistant_hcm` de
la VPS; verificar que el asistente responde en la UI. La carga no elimina filas ni reinicializa el
volumen. Si algún `CREATE OR REPLACE VIEW` no puede sustituir la vista vieja por cambio de columnas,
se reemplaza solo esa vista dentro de una transacción, sin `CASCADE`.

- *Supuesto*: los scripts de la VPS los ejecuta el usuario; el asistente no abre SSH.
- **Aceptación**: las vistas reflejan las filas activas; mínimos de S6 verificados; chat y presencia responden.
- **Checklist de migración** (aportado por la verificación independiente, L15) — en este orden:
  1. El preflight del bundle valida la firma de la semilla ficticia (209 filas, patrón de correos,
     status/referencias y solo placeholders o nombres ya actualizados); si no coincide, aborta antes
     de escribir. Los aliases se anclan por id, por lo que nombres duplicados no colisionan.
  2. `local_tables.sql` crea las sustituciones `hcm.*` que faltan con `IF NOT EXISTS`; un shape
     incompatible hará fallar la transacción completa, sin `CASCADE`.
  3. `CREATE OR REPLACE VIEW` no puede cambiar la lista de columnas, así que la vista vieja de
     empleados (3 columnas) se dropea dentro de la transacción y se recrea.
  4. `grants.sql` se aplica al final para restaurar `SELECT` sobre las vistas.
  5. Verificar: 9 columnas en `\d` (`employee_id`, `display_name`, `office_ids`, `job_title`,
     `birthday_month`, `birthday_day`, `last_name`, `hire_date`, `age_on_next_birthday`),
     `count(*)` = empleados activos, presencia de hoy = directorio,
     `display_name` sin NULL, y medir el coste de `talentassistant_presence` con el volumen real.
  6. `department = ''` real aparecería como office id `''` (`ARRAY_REMOVE` solo elimina NULLs);
     revisar si el dato real trae vacíos.

### S6 — Dataset ficticio de demostración con aspecto productivo

El usuario pidió «dame nombres mas reales quiero que sea lo mas similar a un entorno productivo»
y confirmó «Si» a estos mínimos: 209 empleados (197 activos con los filtros actuales), al menos
2.000 ausencias visibles, 20.000 marcajes `public.time_check` en una ventana de 45 días y 2.000
tareas visibles. Antes de esta enmienda, la semilla tenía 209 empleados y 18.651 marcajes, pero solo 620
ausencias y 4 tareas. Los datos y nombres seguirán siendo ficticios, conforme a `README.md` y
`AGENTS.md`; no se copiarán datos personales reales.

- Reemplazar los cinco nombres de marcador y corregir el generador de apellidos nulos; conservar
  alias `employee-demo-*` y hechos de fixture usados por las pruebas.
- Escalar ausencias, marcajes y tareas con ids/referencias estables, FKs válidas, estados y fechas
  distribuidos de forma determinista; todas las tareas apuntan a empleados activos del directorio.
- La fuente real-shape no contiene tablas de tareas: se mantiene el passthrough del contrato sobre
  `hcm.tasks` legado y se amplía esa fuente sintética con el mismo alias de empleado que el directorio.
- El script de VPS será idempotente y aditivo; puede actualizar solo las filas de demostración
  reconocidas para sustituir placeholders/apellidos nulos y añadir nuevos registros, pero nunca
  borra registros ni reinicializa volúmenes. Abortará antes de escribir si la base no coincide con
  las precondiciones documentadas.
- **Aceptación**: 209 empleados fuente, 197 activos; `timeoff` visible ≥2.000; `time_check` ≥20.000
  en 45 días; tareas visibles ≥2.000; nombres visibles plausibles y no vacíos; dos ejecuciones
  producen el mismo estado y la segunda no duplica registros.
- **Checks**: pruebas deterministas de semilla/volúmenes, `PostgresCatalogScopeTests`,
  `db/hcm-real/verify-demo-data.sql`, `scripts/verify-hcm-demo-loader.ps1` y verificación
  independiente del SQL que muta datos.

## Tasks

| ID | Specs | Ruta | Estado | Commit |
|----|-------|------|--------|--------|
| T1 | S1, S2, S3, S6 | inline | RED L19 → GREEN: nombres, ausencia/presencia/tasks y parity tests | `06ea529` |
| T2 | S1, S2, S3, S6 | inline | Implementada; aliases por id, apellidos plausibles y escalado de eventos | `06ea529` |
| T3 | S4, S6 | inline | Implementada; SPEC-007/012, AC y guía local/VPS actualizadas | `30d2f1e` |
| T4 | S1, S2, S3, S6 | inline | Suite 186/186; Q3/Q7/Q10 vivos PASS, banco completo parcial por 429 (L24) | `06ea529` + `30d2f1e` |
| T5 | S5, S6 | inline | Pendiente: aplicación manual del bundle en VPS | — |
| T6 | S1, S2, S6 | inline | Implementada; seed limpio verificado en BD aislada con mínimos | `06ea529` |
| T7 | S5, S6 | inline | Bundle/harness local PASS; ejecución real en VPS pendiente | `30d2f1e` |

Forecast de esta ampliación: **~1.000–1.400 líneas**. Se conserva la estrategia de entrega ya
elegida en L12 (push directo a `main`); commits work-unit en la rama indicada en la cabecera.

- **T1** — RED para nombres plausibles, los mínimos de las cuatro colecciones y la ausencia de
  duplicados tras re-ejecutar el loader.
- **T2** — GREEN en `db/hcm-real/seed.sql`, `db/hcm-contract/local/local_seed.sql` y aliases de
  `views.sql`; cambiar fuentes sintéticas y expectativas de los nueve fixtures junto al mismo
  comportamiento, preservando hechos de cumpleaños/presencia/ausencias.
- **T3** — Actualizar SPEC-011/012, AC y guía VPS/local en español; documentar que la carga del
  servidor solo usa datos ficticios.
- **T4** — Verificación: suite completa; preguntas vivas afectadas por identidad/birthday/vacaciones
  en `verify-assistant-questions.ps1` (Q3, Q7, Q10 individuales); Postgres por `PostgresModeHttpContractTests` +
  `PostgresCatalogScopeTests`; segunda ejecución del loader aislada. Guardrails de prompts no cambian
  en esta tarea, así que su prueba viva previa sigue siendo evidencia y no se repite.
- **T5** — El usuario aplica contrato y carga en la VPS; verificar conteos y chat/presencia sin
  que el agente use SSH ni imprima credenciales.
- **T6** — Semilla determinista local: nombres ficticios plausibles; 2.000 ausencias visibles,
  20.000 marcajes de ventana 45 días y 2.000 tareas. Risk: none (solo init local de datos ficticios).
- **T7** — Fichero autocontenido con preflight, transacción, ids calculados frente a `max(id)`,
  referencias estables y `ON CONFLICT`; probar dos ejecuciones en BD aislada, sin borrar volumen.
  `scripts/verify-hcm-demo-loader.ps1` automatiza la actualización legacy simulada, el hash antes/después,
  la segunda ejecución y el preflight negativo. Risk: item 1 (actualiza exclusivamente filas demo identificadas y persiste el top-up; preflight
  estricto, transacción y prueba de repetición para evitar borrar o duplicar registros).

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
  **3 columnas** (`employee_id`, `display_name`, `office_ids`) cuando el contrato del repo tiene
  9; `SearchEmployeesSql` selecciona 7 (`PostgresWorkforceSource.cs:36-38`, la vista vieja no tiene
  `job_title`, `last_name`, `hire_date`, `age_on_next_birthday`) → *column does not exist* por cada herramienta, excepción tragada en
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
- **L14** (2026-10-25) Publicación según la estrategia elegida: merge fast-forward de
  `feat/hcm-contract-all-employees` en `main` y push `101c993..a4d6997` a `origin/main`
  (`9a71b8f`, `5070c50`, `af537c6`, `a4d6997`), más `gh workflow run deploy.yml --ref main` →
  run `40163343898` (el Environment `production` pide la aprobación del mantenedor; SPEC-015:
  despliegue nunca automático). Sigue pendiente T5: aplicar `db/hcm-contract/views.sql` en la
  VPS, que es lo que repara el chat de producción.
- **L15** (2026-10-25) Verificación independiente (`gentle-ai-verify`, sesión
  `ses_e8e95d011ffe7rmXIa3mvGx6rI`) sobre `101c993..612bab7`: **S1, S2, S3 y S4 CUMPLE**,
  **S5 pendiente** (sin acceso remoto autorizado), **0 blockers**. Evidencia observada por el
  verificador: suite **185/185** en su propia ejecución (`SUITE_EXIT=0`), conteos 197/197/583/0
  (`display_name` sin NULL), 9 alias fixtures presentes y sin ids duplicados, 0 filas generadas
  con `summary_es ≠ summary_en`, 52 resúmenes `NULL` por `note` NULL, `\d` con las 13 columnas de
  `timeoff`, barrido de `specs/` y `docs/` sin texto de alcance vigente acotado a nueve fixtures,
  SQL parametrizado y cero secretos en el diff. Advisories: el texto del catálogo de herramientas
  (`ChatOrchestrator.cs:54`) no tiene prueba que lo aserte; warnings CS8601/CS8604 preexistentes
  en `Api/Program.cs`; 20 grupos de `display_name` duplicado en la semilla (identidad por id).
  Riesgo residual del `Risk: item 5` convertido en el checklist de migración de S5 (colisión de
  alias, tablas legadas, `ON_ERROR_STOP`, re-grant, coste de presencia, departamentos vacíos).
- **L16** (2026-10-25) La primera aplicación VPS no llegó a ejecutar SQL: `\i /tmp/views.sql` no
  encontró el fichero; `DROP VIEW` estaba dentro de `BEGIN` y la sesión cerró al fallar. El usuario
  verificó que la vista seguía con las 3 columnas originales, por lo que el rollback quedó confirmado.
- **L17** (2026-10-25) Ampliación autorizada, texto literal del usuario: «y olvidemonos de eso de
  datos sinteticos con empleado sintetico 1. dame nombres mas reales quiero que sea lo mas similar
  a un entorno productivo»; «Si y si es el caso, cientos de empleados, miles de ausencias, miles de
  marcajes y tareas». Se mantiene la regla del repo: datos ficticios, no personas reales.
- **L18** (2026-10-25) El usuario respondió «Si» a la propuesta concreta: 209 empleados (197 activos),
  ≥2.000 ausencias visibles, ≥20.000 marcajes en 45 días y ≥2.000 tareas; actualizar semilla local
  y preparar carga idempotente en VPS sin borrar lo existente. No se re-inicializa el volumen.
- **L19** (2026-10-25) T1 RED observado: `PostgresCatalogScopeTests` → 2 fallos y 3 verdes; el
  directorio aún muestra los cinco nombres placeholder y 95 apellidos vacíos; ausencias visibles
  **583** frente al mínimo 2.000; tareas **4** frente a 2.000. El nuevo guard `verify-demo-data.sql`
  en la base local reportó además **18.506** marcajes en la ventana UTC de 45 días (mínimo 20.000).
- **L20** (2026-10-25) Primer preflight local de `demo-scale.sql` abortó antes de escribir: esperaba
  95 apellidos NULL en todo el rango 101..300, pero el conteo era 100 (95 activos + 5 inactivos).
  Corregido el fingerprint a 100/0; la transacción revertida no cambió filas.
- **L21** (2026-10-25) T2 GREEN: seed limpio en BD aislada validó **209/197/2000/20000/2000**;
  `vps-demo-upgrade.psql` ejecutado dos veces en `talentassistant_hcm` local dejó ambos conteos
  idénticos (**209/197/2000/20000/2000**) y `verify-demo-data.sql` pasó en las dos corridas. La
  segunda ejecución insertó 0 ausencias, 0 marcajes y 0 tareas; no se borró ninguna fila.
- **L22** (2026-10-25) Suite completa tras los cambios: **186/186** (11 unitarias + 175 integración).
  La pasada focal de HCM/nombres (102/102) también quedó verde. Los scripts vivos y el rebuild local
  de API aún están pendientes de T4.
- **L23** (2026-10-25) El bundle `vps-demo-upgrade.psql` se simuló dos veces en el contenedor local
  con las carpetas `db/hcm-contract`/`db/hcm-real` copiadas a un scratch temporal: ambos runs
  terminaron `COMMIT`; conteos antes/después `209/197/2000/20000/2000`; segunda pasada insertó 0
  ausencias, marcajes o tareas. Scratch `hcm-demo-check-d99bb57a` eliminado. No se conectó a la VPS.
- **L24** (2026-10-25) API local reconstruida y saludable (`/health` → `healthy`).
  `verify-assistant-questions.ps1 -PaceSeconds 15` se ejecutó dos veces; ambas terminaron con
  HTTP `429`, razón `RateLimited` (run 1 al llegar a cumpleaños, Q7; run 2 en regreso de Pedro, Q5).
  Probes individuales afectados: Q3 (vacaciones → Mireia Pardo y Bruno Castaño), Q7
  (`upcoming_birthday` → Mauricio Cárdenas, 30-11-2026, 38 años) y Q10 (apellidos con z → Gael
  Domínguez y Juan Pérez) → todos PASS, EXIT=0. El banco completo queda pendiente por el límite;
  no se atribuye el rate limit al cambio. El guardrail vivo no se volvió a lanzar porque su prompt
  no cambió; suite automatizada 186/186.
- **L25** (2026-10-25) Se volvió a probar el bundle VPS tras pulir notas ficticias: inicialización
  limpia y dos ejecuciones del master script completan `verify-demo-data.sql` con
  `209/197/2000/20000/2000`; el scratch `hcm-demo-check-ce0ec3c2` se eliminó. El alias SSH del
  prompt `srv1904834` no resuelve desde el PC del usuario; todavía no se copió el bundle ni se
  ejecutó nada en la VPS.
- **L26** (2026-10-25) `scripts/verify-hcm-demo-loader.ps1` pasó en dos BD scratch internas al
  contenedor: migración legacy 209/197/583/18506/4 → 209/197/2000/20000/2000, segunda ejecución
  con snapshots idénticos y escenario negativo de email/firma no ficticia que aborta sin cambiar
  el hash. El script limpia sus DBs y carpeta temporal al terminar. `.gitattributes` fija LF para
  `*.psql` para que los comandos `\ir` sobrevivan al checkout/transferencia desde Windows.
- **L27** (2026-10-25) Verificación independiente (`gentle-ai-verify`, sesión
  `ses_e8c9ea803ffezfmXwdzT9ZZ8AO`): **S1–S4 y S6 CUMPLE; S5 PENDIENTE; 0 blockers**. En scratch
  ejecutó suite **186/186**, init limpia 209/197/2000/20000/2000, upgrade legacy
  209/197/583/18506/4 a los mínimos, segunda ejecución con snapshots idénticos y firma negativa
  abortada sin cambio de hash. Grants de solo lectura confirmados como rol app; asociaciones de
  tareas activas, sin `DELETE`/`TRUNCATE`/`DROP TABLE`/`CASCADE` en el bundle. Sus advisories de
  `*.psql` LF y overflow de ids de timeoff se corrigieron con `.gitattributes` y guard de max(id);
  `verify-hcm-demo-loader.ps1` pasó otra vez después. Sin acceso a la VPS.
- **L28** (2026-10-25) `scripts/verify-hcm-demo-loader.ps1` → PASS en scratch local: legacy
  209/197/583/18506/4 → upgraded 209/197/2000/20000/2000, segunda ejecución idéntica y firma
  alterada rechazada antes de mutar (hash intacto); ambas DBs y la carpeta temporal se limpiaron.
- **L29** (2026-10-25) El espejo Engram `odd/hcm-contract-all-employees/tasks` queda pendiente:
  las herramientas Engram no están disponibles en el catálogo de esta sesión. Este archivo local
  conserva el documento completo; resync cuando vuelva el proveedor de memoria.
- **L30** (2026-10-25) Commit work-unit T6 en `feat/hcm-production-like-demo-data`: `06ea529`
  `feat(hcm): add realistic fictional HCM activity data` — nombres y alias estables, seed de 2.000
  ausencias/20.000 marcajes/2.000 tareas, paridad actualizada y pruebas asociadas. T7 loader y guía
  VPS continúan en el siguiente work-unit.
- **L31** (2026-10-25) Commit work-unit T7/T3 en `feat/hcm-production-like-demo-data`: `30d2f1e`
  `feat(hcm): add idempotent VPS demo-data upgrade` — bundle transaccional, guard de volúmenes,
  harness negativo/idempotente y guía de transferencia; `dotnet test` 186/186 y
  `verify-hcm-demo-loader.ps1` PASS. T5 sigue pendiente: el usuario debe subir las carpetas con su
  destino SSH habitual y ejecutar el bundle en `talentassistant_hcm`.
- **L32** (2026-10-25) No se ejecuta `workflow_dispatch`: el diff no cambia `PostgresWorkforceSource`
  ni los contratos de API; la API desplegada ya consulta las vistas. T5 es solo el bundle SQL en
  la VPS, ejecutado por el usuario.
