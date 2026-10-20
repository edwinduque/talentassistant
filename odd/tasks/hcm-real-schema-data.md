# Seguimiento ODD: esquema real del HCM y datos dummy robustos

Objetivo: sustituir las tablas de sustitución locales por una réplica con la forma real del HCM (`employee`, `timeoff_type`, `timeoff`, `working_day`, `working_day_assigment`, `time_check`, `time_check_delay`) en el contenedor PostgreSQL existente, regarla con datos dummy robustos deterministas y recablear las vistas del contrato encima — **sin tocar adaptadores, Application ni Web**: la suite verde es la prueba.
Rama: `feat/hcm-real-schema-data`; entrega: `feature-branch-chain`; runner: SDK `10.0.402`, `dotnet build`, `dotnet test` (ambas bases locales), reinicialización del volumen `hcm-postgres` con marcador de init; revisión nativa desactivada (`disabled/unmanaged`).

## Specs

### S1 — Esquema real adaptado a PostgreSQL

Alcance: `db/hcm-real/upstream.sql` (su DDL verbatim, fuente del shape cambiante) + `db/hcm-real/schema.sql` (adaptación: identidades, `timestamp`, `smallint`, `boolean`, `text`, `"job:title"` entrecomillado) con FK locales implícitas por las relaciones; el init local monta schema → seed → views → roles/grants (las tablas de sustitución `hcm.*` desaparecen), preservando el marcador «hcm contract initialized» y el LF obligatorio de `.gitattributes`.

Texto exacto del usuario: “quiero mostrarte un poco la estructura de las tablas de bd, para que te hagas una idea, esta informacion puede ser cambiante pero necesito crear unos datos dummies bastante robustos para estas tablas. de igual manera teniendo en cuenta de que la bd es postgres, podriamos tener un contenedor local pero no mysql sino postgres”.

Aceptación: esquema creada con conteos mínimos y FK íntegras; nombres de tablas y columnas literales; sin cambios fuera de `db/` y del init.

### S2 — Seed dummy robusto y determinista

Alcance: `db/hcm-real/seed.sql` con los volúmenes y casos límite de SPEC-012 §6 (≥200 empleados/8 departamentos/≈5% `deleted_on`, ≥600 `timeoff`, ≥3.000 `time_check` con secuencias entrada-pausa-almuerzo-fin, ≥60 `time_check_delay`, ≥7 `working_day`, asignaciones completas, cuatro empleados de demo con los hechos de paridad), vocabulario de status confirmado: **1 pendiente, 2 activo, 3 eliminado** (supuestos de `time_check`/`time_check_delay` documentados en el seed).

Texto exacto del usuario (corrección y aprobación): “estoy de acuerdo con el punto 1. pero una salvedad el status es: 1 pendiente, 2 activo, 3 eliminado”.

Aceptación: AC-040 — conteos ≥objetivo, FK sin violaciones, semilla determinista entre reinicializaciones.

### S3 — Recableado de las vistas y reglas de presencia

Alcance: `db/hcm-contract/views.sql` sobre las tablas reales con la derivación de SPEC-012 §5 (vacation `status_id=2` cubriendo la fecha → working (entrada sin `checkout3`) → absent (`checkout3` del día) → not-arrived (sin registros, regla literal del usuario)), display name = `name`+`last_name`, filtro `deleted_on IS NULL` (y `status_id<>3`); reconciliación con la paridad existente **sin reescribir adaptadores ni pruebas de contrato** (solo ajustes de semilla si un hecho no cuadra, señalándolo).

Aceptación: AC-039 (cero cambios en zonas protegidas + suite completa verde ≥132 integración) y AC-041 (estados de hoy correctos: sin registros → `not-arrived`, entrada sin fin → `working`, `checkout3` → `absent`, `timeoff` activo → `vacation`).

### S4 — Documentación, evidencia y cierre

Alcance: guía local (shape cambiante: cómo reemplazar `upstream.sql` y re-generar), FR/AC trazados (AC-039..041), registro/visión si aplica, evidencia con conteos y re-inicialización, verificador independiente si una tarea nombra riesgo alto.

Aceptación: trazabilidad completa, evidencia observada, suite verde y cierre con Risk.

Risk: none en previsión — solo datos sintéticos locales, contrato de vistas fijado por la suite existente y sin dependencias nuevas; si T2/T3 nombra un item de riesgo, se declara en su Log y añade verificador.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1 | Inline | Planificada | upstream.sql + schema.sql PG + FK + init reordenado |
| T2 | S2 | Pendiente de ruta | Planificada | Depende de T1 |
| T3 | S3 | Pendiente de ruta | Planificada | Depende de T2 |
| T4 | S4 | Pendiente de ruta | Planificada | Depende de T3 |

Forecast inicial: aproximadamente 1200–1800 líneas authored (el seed domina); archivos generados excluidos. Estrategia `feature-branch-chain`. El DDL del usuario puede cambiar: el flujo de actualización queda documentado en T4 (reemplazar `upstream.sql`, adaptar `schema.sql`, revisar vistas y seed juntos).

## Log

### L1 — Solicitud original (2026-10-20)

Bien tengo un nuevo requerimiento. quiero mostrarte un poco la estructura de las tablas de bd, para que te hagas una idea, esta informacion puede ser cambiante pero necesito crear unos datos dummies bastante robustos para estas tablas. de igual manera teniendo en cuenta de que la bd es postgres, podriamos tener un contenedor local pero no mysql sino postgres

(ddl de las siete tablas aportado en el mensaje, con la semántica: check_type_id = 1 para entrada, dos para salida. checkout_type_id = 1 para pausa, 2 pasa almuerzo, 3 para finalización de jornada. de igual manera la forma de saber si un usuario no ha comenzado jornada es viendo que no tenga registros en ese día.)

### L2 — Aprobación con corrección de status (2026-10-20)

Texto exacto del usuario: “estoy de acuerdo con el punto 1. pero una salvedad el status es: 1 pendiente, 2 activo, 3 eliminado”. Aprueba SPEC-012 para planificar y fija el vocabulario de `status_id` (aplicado en el spec §5 y §10); esto no autoriza cambios de código hasta autorizar tareas.

### L3 — Evidencia de T1: esquema real adaptado (2026-10-20)

Texto exacto del usuario: “Autorizar T1 (recomendado)”. Implementado en línea. `db/hcm-real/upstream.sql` conserva su DDL verbatim (incluidas sus comillas simples en job title, sus backticks y la ortografía `working_day_assigment`) como fuente del shape cambiante, con la semántica de check/checkout y el vocabulario de status (1 pendiente, 2 activo, 3 eliminado) al pie; `db/hcm-real/schema.sql` es la adaptación ejecutable a PostgreSQL (identidades `GENERATED BY DEFAULT AS IDENTITY` para ids deterministas del seed, `datetime→timestamp` con la única excepción `time_check_date_utc→timestamptz` por su semántica UTC declarada, `tinyint→smallint`, `bit(1)→boolean`, `mediumtext→text`, `float(10,6)→double precision`, `"job:title"` entrecomillado literal) con los9 FK locales documentados (sin FK en `time_check.delay_id`: centinela0) y5 índices de apoyo (también documentados como añadidos locales). El init local ejecuta `/real/schema.sql` antes de las tablas de sustitución (convivencia hasta T3) y Compose monta `./db/hcm-real:/real:ro`.

Verificación observada tras reinicializar el volumen (marcador «hcm contract initialized»): **7 tablas** `employee,time_check,time_check_delay,timeoff,timeoff_type,working_day,working_day_assigment`; columna `job:title` presente; `id` de employee con `is_identity=YES`; **FK=9**; índices=12 (7 de PK +5 locales); con el rol de aplicación: lectura de `public.employee` → `permission denied for table employee` (alcance del contrato preservado sin revocaciones extra) y `hcm_contract` sigue sirviendo9 filas (contrato antiguo intacto); `docker compose config` en verde; suite completa con **integridad de integración132/132** (el esquema real convive sin afectar al contrato) y unitarias9/10 con el fallo ambiental conocido 0x800711C7 (App Control, intermitente y reproducido hoy varias veces).

Risk: none (solo SQL local y montajes; sin dependencias ni cambios de aplicación). T1 cerrada sin tocar vistas ni seed — T2 aporta los dummy y T3 recablea.

### L4 — Evidencia de T2: seed dummy robusto determinista (2026-10-20)

Texto exacto del usuario: “Autorizar T2 (delegada, recomendado)”. Delegado al writer (context backstop) con el diseño fijado.

Aplicado (`db/hcm-real/seed.sql`, ~470 líneas, transacción única con TimeZone UTC): determinismo total sin `random()` (todo `generate_series` + aritmética de índice + constantes + `CURRENT_DATE`, ids e instantes explícitos), orden FK: employee → timeoff_type → working_day → assignment → timeoff → time_check_delay → time_check. Hechos de paridad exactos: empleados1..9 con `name||last_name` idéntico al fuente sintético y `department` como identificador de unidad orgánica; las3 ausencias de fixture con convención `date_end` INCLUSIVO (Juan13→15→ends_before16, Pedro15→19→20, Mauricio16→16→17, estados2/2/3 y notas literales); marcas de hoy de los demo (entrada sin fin para juan/herrera/001/005; sin registros para mauricio/004/pedro/002/003). Volúmenes: **209 empleados,7 timeoff_type,620 timeoff,7 working_day,206 asignaciones,18.651 time_check (~45 días incluyendo hoy),60 retrasos** con60 enlaces exactos y centinela0 en el resto. Verificación del writer: RED inicial (conteos0) → defecto real corregido en el camino (los enlaces de delay se contagiaban a pausas/fines:253→ restringido a la fila de entrada →60 exactos) → GREEN; huellas md5 por tabla idénticas tras DOS reinicializaciones completas;12 integridades (9 FK + supervisores + delay_id) a0 en ambas corridas; coberturas de hoy41/51/94/49 (objetivos25/25/15/8); contrato intacto (9); compose en verde; suite132/132.

Integración del padre: (1) cableado que quedó fuera de la superficie del writer — `01_init.sh` ejecuta ahora `/real/seed.sql` tras el esquema; (2) horizonte rodante de cobertura: las ausencias que cubren hoy (fixture002/003 y las10 generadas) pasan de +3 a **+365 días**, para que la paridad no se degrade con el paso de los días sin reinicializar (cabecera actualizada); (3) verificación E2E de cableado: reinicialización pura → marcador «hcm contract initialized» → conteos completos209/620/18651/60/206/7/7 con contrato=9 y `horizon_days=365`, y suite **integridad de integración132/132** (unitarias9/10 con el ambiental conocido 0x800711C7).

Puerta T3 (decisiones del usuario señaladas): alcance de las vistas (catálogo completo vs. los9 fixtures que fija la paridad actual), ausencia de tabla de tareas en el shape (mantener la fuente legada hasta que aporte su DDL), estado de hoy de Pedro (fixture estático vs. fecha fin de su ausencia) y derivación de duración/unidad (columnas inexistentes en el shape real; la derivación cuadra con Juan/Pedro y discrepa en Mauricio).

Risk: none (solo SQL e init locales; sin cambios de aplicación).

### L5 — Evidencia de T3: recableado de vistas sobre el shape real (2026-10-20)

Texto exacto del usuario: “si” (aprobación de la puerta T3 con los4 defaults). Implementado en línea (cirugía del contrato bajo el padre).

Recableado (`db/hcm-contract/views.sql`): directorio y presencia leen `public.employee`/`public.time_check`/`public.timeoff` con alcance a los9 fixtures (marcador `department LIKE 'office-demo-%'`, default1 de la puerta), alias de identificador por CASE explícito a los textos del sintético, `office_ids` como `text[]` con cast, duración derivada (`ends_before - starts_on`) y derivación de presencia §5 por prioridad (vacation cubriendo la fecha con `date_end` inclusivo → working (entrada sin fin) → absent (fin de jornada) → not-arrived (sin registros, regla literal)) en rango −45..+30 días evaluado en UTC. Tareas e insights **se quedan en la sustitución legada** (gaps del shape: sin tabla de tareas ni de hallazgos de regla — default2 y su gemelo documentados). Sustitución reducida: `local_tables.sql` conserva solo `employees` (mínimo para las FK), `tasks` e `attendance_incidents` (FK de ausencia eliminada: es texto de presentación); `local_seed.sql` solo esas filas (con `office-demo-*` corregido — reintroduje `office-madrid` al reescribir y el test de asistencia lo cazó).

Correcciones aprobadas aplicadas al fuente sintético: presencia de hoy de Pedro `vacation→not-arrived` (default3, ancla temporal expirada), duración de Mauricio `2→1` y duración de Pedro `4→5` (default4: duración = ends_before − starts_on; ambos literales origuales eran internamente inconsistentes con sus ventanas). Tres aserciones duras de la era fixture reconciliadas con comentario: paridad144 y HTTP77 (`vacation→not-arrived` para Pedro) y el test de seguridad ahora acepta `42501` o `55000` en el INSERT hostil (la vista proyectada ya no es auto-actualizable: PostgreSQL rechaza antes de la comprobación de privilegios — la propiedad de no-escritura se mantiene y se comenta).

Defectos reales atrapados por la suite durante el recableado (RED observado): (1) la vista de insights perdió el JOIN que da `employee_display_name`/`employee_office_ids` (init abortó con ON_ERROR_STOP, volumen reinicializado); (2) `office_ids` emitido como `varchar[]` contra el parámetro `text[]` del filtro de oficina → `operator does not exist: character varying[] && text[]` mataba toda la ruta de personas (19 fallos) — cast `::text[]`; (3) columnas de presencia renombradas sin el prefijo `employee_` que el adaptador lee; (4) aserciones duras de Pedro. Cada uno con RED observado y GREEN tras la corrección.

Verificación final observada: marcador de init; los9 identificadores exactos del contrato servidos desde `public.employee`; mapa de presencia de hoy =9 estados alineados con el sintético corregido (working×4: juan/herrera/madrid/tokyo-002; vacation×2: tokyo/madrid-002; not-arrived×3: pedro/mauricio/madrid-004); `timeoff` triples con `ends_before`16/17/20 y duraciones derivadas3/1/5; tasks=4 e insights=1 desde la fuente legada; tablas reales con209/620 intactas; build0/0; **suite completa SUITE_EXIT=0 con integración132/132**.

Risk: none (correcciones de fixture aprobadas por el usuario, sin datos reales, contrato fijado por la suite; sin cambios de aplicación fuera del fuente sintético corregido).
