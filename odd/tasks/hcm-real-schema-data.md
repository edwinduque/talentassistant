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
