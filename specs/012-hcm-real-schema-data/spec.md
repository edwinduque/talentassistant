# SPEC-012 — Esquema real del HCM y datos dummy robustos en PostgreSQL local

- **Estado:** Aprobado (2026-10-20, con la corrección del vocabulario de `status_id`: 1 pendiente, 2 activo, 3 eliminado). Nota de herencia (2026-10-21): desde SPEC-013/ADR-0013 la persistencia propia de la aplicación ya NO usa MySQL — su frase «el MySQL del stack sigue siendo identidad + asistencia sintética» describía el alcance en su momento y quedó superada por la migración a PostgreSQL.
- **Implementación:** Implementada en la rama `feat/hcm-real-schema-data` (resumen en §11).
- **Requisitos relacionados:** sin FR nuevo (preparación de la validación real bajo FR-032/FR-033 y NFR-022 existentes).
- **Criterios relacionados:** AC-039, AC-040, AC-041 (amplían la evidencia de AC-036..AC-038 de SPEC-011).
- **Decisión arquitectónica:** ninguna nueva; aplica ADR-0012 (las vistas son el contrato; los adaptadores no cambian).
- **Decisión de producto registrada:** el usuario aportó el esquema real de las tablas el 2026-10-20 con estas palabras: “quiero mostrarte un poco la estructura de las tablas de bd, para que te hagas una idea, esta informacion puede ser cambiante pero necesito crear unos datos dummies bastante robustos para estas tablas. de igual manera teniendo en cuenta de que la bd es postgres, podriamos tener un contenedor local pero no mysql sino postgres”.
- **Versión:** 0.1.

## 1. Objetivo

Sustituir las tablas de sustitución inventadas en T1 de SPEC-011 por una **réplica local con la forma real del HCM** (`employee`, `timeoff_type`, `timeoff`, `working_day`, `working_day_assigment`, `time_check`, `time_check_delay`) sobre el contenedor PostgreSQL existente, regada con **datos dummy robustos**, y **recablear las vistas del contrato** encima — sin tocar adaptadores, Application ni la Web: que toda la suite siga verde es la prueba de que el contrato funciona contra la forma real.

## 2. Alcance propuesto

### Incluido

- `db/hcm-real/schema.sql`: su DDL adaptado a PostgreSQL (identidades en lugar de `AUTO_INCREMENT`, `timestamp`, `smallint`, `boolean` para `bit(1)`, `text` para `mediumtext`, identificadores con dos puntos entrecomillados como `"job:title"`), **conservando sus nombres de columna y tablas literalmente**, y con las restricciones de integridad que las relaciones implican (FK `timeoff.employee_id → employee.id`, etc.) añadidas localmente — el paste no incluye FKs (supuesto, §10).
- `db/hcm-real/seed.sql`: generador determinista de datos dummy robustos (§6) — sintéticos, versionados, reproducibles en cualquier volumen nuevo.
- Sustitución de `db/hcm-contract/local/local_tables.sql` por el esquema real: las tablas locales `hcm.*` dejan de existir; el init local monta `schema.sql` → `seed.sql` → `views.sql` → `roles.sql`/`grants.sql` en el mismo contenedor y base `talentassistant_hcm`.
- Recableado de las cinco vistas `hcm_contract.*` sobre las tablas reales, con la derivación de presencia de §5 (la presencia no es tabla en su HCM: la vista la deriva, la aplicación no deriva nada).
- Reglas de presencia propuestas para aprobación en §5, basadas en su semántica literal de §4.
- Reconciliación de la paridad: los datos dummy se diseñan para reproducir los hechos del fuente sintético en lo que el banco de SPEC-009/011 verifica (mismos empleados de demostración incluidos en `employee`, ausencias y tareas equivalentes), de modo que **las pruebas de paridad existentes sigan en verde sin reescribirse**.

### Fuera de alcance

- **Sin MySQL nuevo**: el trabajo entero es PostgreSQL (el MySQL del stack sigue siendo identidad + asistencia sintética).
- Sin cambios en `PostgresWorkforceSource`, `PostgresAttendanceSource`, Application, Web ni contratos de puertos — si algo de eso necesita tocar, el diseño está mal (señalarlo y parar).
- Sin conexión a la base real del HCM (queda para cuando el usuario aporte credenciales: §2 de SPEC-011 sigue vigente).
- Sin escrituras ni servicios externos; sin datos reales de personas (los dummy son ficticios por definición).
- Sin ETL/CDC y sin MCP.

## 3. Requisitos y límites

- **El contrato es la prueba**: si las vistas quedan bien cableadas, ni una línea de adaptador cambia y la suite completa (hoy132/132 de integración) permanece verde.
- **Volatilidad del esquema**: “esta informacion puede ser cambiante” — el DDL que aporte el usuario se conserva verbatim en `db/hcm-real/upstream.sql` (fuente de verdad del shape) y la adaptación a Postgres vive en `schema.sql` con cabecera que indique la fecha de la entrada; las vistas y el seed se revisan juntos cuando el shape cambie.
- Datos dummy **100% ficticios** (nombres, teléfonos, correos, IPs, fotos), deterministas (mismas semillas → mismas filas) y sin secretos.
- Nada de esto toca `.env` de credenciales reales; el servicio local mantiene credenciales por defecto solo-locales (SEC-002 vigente).

## 4. Semántica de entrada (verbatim del usuario,2026-10-20)

- `check_type_id = 1` entrada, `2` para salida.
- `checkout_type_id = 1` para pausa, `2` para almuerzo, `3` para finalización de jornada.
- “la forma de saber si un usuario no ha comenzado jornada es viendo que no tenga registros en ese día”.
- Tablas de referencia: `employee` (nombre compuesto por `name` + `last_name`, `department`, `hire_date`, `deleted_on`, `language_id`, `role_id`, `status_id`, supervisores, `job:title`), `timeoff_type` (catálogo con `amount`, `fiscal_year`, ventana), `timeoff` (ventana `date_start/date_end`, `status_id`, supervisor, `by_hour`, `note`), `working_day` (jornada con `enable_delays`, `web_allowed`, `mobile_allowed`, `is_default`), `working_day_assigment` (empleado↔jornada), `time_check` (marcaciones con `check_type_id`, `checkout_type_id`, UTC + timezone, geolocalización, `ip_address`, fotos, `delay_id`), `time_check_delay` (incidencias con `approved_by`/`status_id`).

## 5. Reglas propuestas de derivación de presencia (para aprobación)

La vista `talentassistant_presence` calcula el estado por (fecha, empleado) con esta prioridad:

1. **vacation**: existe `timeoff` cubriendo la fecha con `status_id = 2` (activo — vocabulario confirmado en §10; el estado aprobado/activo del registro es el que cubre la fecha).
2. **working**: tiene `time_check` con `check_type_id=1` ese día **y sin** `checkout_type_id=3` ese día (pausas y almuerzo no terminan jornada).
3. **absent**: tiene `checkout_type_id=3` ese día → jornada finalizada (ya no está trabajando; Q1 del banco lo excluye correctamente).
4. **not-arrived**: sin registros de `time_check` ese día (su regla literal), cuando no aplica vacation.

Sin asignación (`working_day_assigment`) también cae en not-arrived. Empleados con `deleted_on` no aparecen. La regla es independiente de la fecha (pasado/futuro deterministas); es la única lógica de negocio nueva y **vive en la vista, no en la aplicación**.

## 6. Robustez objetivo de los datos dummy

| Entidad | Volumen mínimo | Cobertura |
|---|---|---|
| `employee` | ≥200 (verificado: 209) | ≥8 departamentos, `status_id` y `role_id` distribuidos, ~5% con `deleted_on`, correos/teléfonos/fotos parciales, supervisores referenciando a otros empleados, `language_id` mezclado, contratos de `hire_date` de10 años |
| `timeoff_type` | ≥6 (verificado: 7) | tipos con `amount` distinto, ventanas de fiscal_year, algún tipo con `status_id` inactivo |
| `timeoff` | ≥2.000 visibles para empleados activos | estados mezclados, `by_hour` sí/no, notas, ventanas pasadas/futuras, supervisores y `date_supervised` |
| `working_day` | ≥7 | variantes con `enable_delows`/`web_allowed`/`mobile_allowed` y un `is_default` |
| `working_day_assigment` | 1 por empleado activo (verificado: 206 asignaciones) | algunos con asignación antigua duplicada (histórico) |
| `time_check` | ≥20.000 en la ventana móvil UTC de 45 días | secuencias completas `1 → (1/2 pausa/almuerzo) → 2/3`, casos de jornada finalizada, geolocalización e IP plausibles, `timezone` por oficina, `photo_face_url` parcial, `delay_id` enlazado |
| `time_check_delay` | ≥60 | estados de aprobación, `approved_by` sobre empleados, comentarios largos |
| `hcm.tasks` (sustitución local) | ≥2.000 tareas visibles | 4 tareas de fixture + tareas bilingües ficticias repartidas entre empleados activos |

Más casos límite obligatorios: empleados de demostración del banco (Juan Pérez, Pedro Rojas, Mauricio Cardenas, Juan Herrera) con los hechos equivalentes que la paridad exige (vacaciones de Juan13→16, Pedro15→20 con `checkout` cubriendo su patrón, tareas implícitas solo donde el banco lo verifica); un empleado sin ninguna marcación hoy; otro con `checkout_type_id=3` hoy; otro con `timeoff` hoy; el estado de cada `status_id` documentado como supuesto en §10.

## 7. Componentes candidatos

| Componente | Responsabilidad | Línea base que conserva |
|---|---|---|
| `db/hcm-real/upstream.sql` | DDL del usuario verbatim (entrada) | Trazabilidad del shape cambiante |
| `db/hcm-real/schema.sql` | Adaptación PostgreSQL + FK locales | Nombres de tablas/columnas literales |
| `db/hcm-real/seed.sql` + `demo-scale.sql` | Generador base de una ejecución + top-up idempotente (§6) | Solo datos ficticios; nombres plausibles y conteos objetivo |
| `db/hcm-contract/views.sql` | Recableado sobre las tablas reales + §5 | Mismas cinco vistas; desde 2026-10-25 el directorio añade `area`, `gender`, `years_in_company` y jefe directo (ver enmienda de §11) |
| `db/hcm-contract/local/*` | Init de shape real, escala y tareas/insight legados | Marcador «hcm contract initialized» y LF obligatorio |
| Pruebas | Suite existente como detector de regresión | Adaptadores y puertos sin tocar |

## 8. Criterios de aceptación propuestos

| ID | Criterio verificable | Evidencia futura |
|---|---|---|
| AC-039 | Con las vistas recableadas sobre el esquema real local, **cero cambios** en adaptadores, Application, Web y puertos, y la suite completa permanece verde (integración ≥132 con ambas bases) | `git diff` sin zonas prohibidas + suite completa (verificado: build 0/0, suite completa `SUITE_EXIT=0` con integración 132/132) |
| AC-040 | La inicialización ficticia cumple los volúmenes y casos límite de §6 con integridad referencial e idempotencia; dos inicializaciones el mismo día UTC producen los mismos conteos: `employees=209`, activos `=197`, `timeoff≥2000`, `time_check≥20000` en 45 días y tareas `≥2000` | `verify-demo-data.sql`, suite, checksum de dos inicializaciones y segunda ejecución del cargador VPS sin duplicados |
| AC-041 | La presencia derivada (§5) reproduce los estados esperados para hoy: los empleados sin registros de hoy figuran como `not-arrived`, los que tienen entrada sin fin de jornada como `working`, los de jornada finalizada como `absent` y los con `timeoff` aprobado como `vacation`; las preguntas del banco responden igual | Pruebas de la vista + pasada en vivo opcional de las siete preguntas con la clave (verificado: 9 estados de hoy alineados con el sintético corregido; la pasada en vivo opcional queda pendiente) |

## 9. Plan SDD propuesto

1. Añadir AC-039..AC-041 con trazabilidad (sin FR nuevo) y este spec al registro.
2. Rama `feat/hcm-real-schema-data` desde `main` (`b3d331a`) y plan ODD: **T1** (upstream.sql + schema.sql adaptado + FK + init local re-ordenado), **T2** (seed dummy robusto + conteos/casos §6), **T3** (recableado de vistas con §5 + reconciliación de paridad + suite completa), **T4** (documentación, evidencia y verificador independiente — item... solo si aparece riesgo nombrado).
3. Verificación: suite completa tras cada tarea, re-inicialización del volumen con conteos, y comprobación de que `git diff` no toca las zonas prohibidas.

## 10. Supuestos y confirmaciones abiertas

1. **`status_id` — CONFIRMADO por el usuario (2026-10-20)**: «el status es: 1 pendiente, 2 activo, 3 eliminado». Se aplica en el seed y en las vistas: empleado activo = `2` (pendientes = `1`, eliminados = `3` además de `deleted_on`), `timeoff` activo/aprobado = `2` (base de la regla §5.1), `time_check` y `time_check_delay` usan el mismo vocabulario en el seed (supuesto del generador documentado en su código para revisión en T2).
2. **`'job:title'` — CONFIRMADO por el usuario (2026-10-23)**: el nombre exacto entrecomillado `"job:title"` corresponde al cargo del empleado y puede proyectarse como `job_title` en el contrato de lectura.
3. Las FK no aparecen en su DDL; se añaden localmente para integridad (si el HCM real no las tiene, no cambia el contrato de vistas).
4. Los estados derivados de §5 son la única semántica nueva; cualquier corrección suya se aplica antes de T3.
5. La existencia de `timeoff` con `date_end` vacío se trata como “hasta nuevo aviso” (cubre la fecha si `status_id` es aprobado).

## 11. Estado y aprobación

SPEC-012 fue aprobada por el usuario el 2026-10-20 (Log L2 del plan ODD), con la corrección del vocabulario de `status_id` — «1 pendiente, 2 activo, 3 eliminado» — ya aplicada en §5 y §10; la aprobación para planificar autorizó después las tareas T1..T4.

Queda implementada en la rama `feat/hcm-real-schema-data` con estos commits:

| Commit | Contenido |
|---|---|
| `9b83859` | Esquema real adaptado (T1): `db/hcm-real/upstream.sql` verbatim + `schema.sql` de PostgreSQL con FK locales y wiring del contenedor. |
| `07dd453` | Seed dummy determinista (T2): `db/hcm-real/seed.sql` y su cableado en el init. |
| `7d27955` | Documentación: este spec y su ADR asociado. |
| `9a37b5e` | Recableado de las vistas del contrato sobre el shape real (T3), con alcance a los nueve fixtures y derivación de presencia §5. |
| «Commit pendiente» | Documentación y cierre (T4): guía local, catálogo AC-039..AC-041, visión y seguimiento ODD. |

Evidencia verificada (2026-10-20): una inicialización limpia auto-carga schema+seed con el marcador «hcm contract initialized»; volúmenes de 209 empleados / 7 `timeoff_type` / 620 `timeoff` / 7 `working_day` / 206 asignaciones / 18.651 `time_check` (~45 días incluyendo hoy) / 60 retrasos; huellas md5 de tabla completa idénticas tras dos reinicializaciones completas; 12 sondas de integridad (FK y enlaces) a cero; los nueve identificadores exactos del contrato servidos desde `public.employee`; mapa de presencia de hoy de 9 estados alineado con el fuente sintético corregido; `ends_before` 16/17/20 con duraciones derivadas 3/1/5; tareas=4 e insights=1 desde la sustitución legada; el rol de aplicación denegado en las tablas físicas (`permission denied for table employee`) y en escritura a través de las vistas (`42501` o `55000`); build 0/0 y suite completa `SUITE_EXIT=0` con integridad de integración 132/132.

Correcciones de fixture aprobadas y aplicadas (2026-10-20): presencia de hoy de Pedro `vacation → not-arrived` (ancla temporal expirada), duración de Mauricio `2 → 1` y de Pedro `4 → 5` (regla derivada `ends_before − starts_on`), con dos aserciones duras de prueba y la expectación de seguridad del INSERT hostil reconciliada (`42501` o `55000`).

Gaps conocidos (documentados, no corregidos): el shape real no expone tabla de tareas ni tabla de hallazgos de regla evaluados, por lo que esas dos vistas del contrato siguen leyendo la sustitución legada hasta que el usuario comparta sus DDLs. Siguen abiertas las decisiones de las FK añadidas localmente y el `date_end` nulo cubierto como «hasta nuevo aviso».

Ampliación de alcance (2026-10-25, decisión literal del usuario: «Ampliar a los 209 empleados reales»): las vistas de personas, presencia y ausencias sirven ya a **todos los empleados activos** (`deleted_on IS NULL AND status_id <> 3`; 197 filas de las 209 de `public.employee`), no solo a los nueve fixtures con `department LIKE 'office-demo-%'`. Los nueve conservan sus alias `employee-demo-*` y el resto recibe `'employee-' || id`. El `display_name` se deriva de `name` + `last_name` tolerando apellidos nulos (95 empleados activos de la semilla no tienen `last_name`; antes la concatenación producía `NULL` y el conector fallaba) y `office_ids` de `department` sin `NULL`s dentro del array. En ausencias, `reference` de los tres fixtures se conserva y el resto usa `'timeoff-' || id` (la convención anticipada en SPEC-016); el tipo sale del catálogo `public.timeoff_type` con la traducción fija de esos siete tipos, el estado de `status_id` (1 Pendiente/Pending, 2 Aprobado/Approved, 3 Rechazado/Rejected) y `summary_es`/`summary_en` proyectan el mismo `note` para las ausencias reales (decisión literal del usuario: «Mismo texto en summary_es y summary_en»), conservando los textos bilingües literales de los tres fixtures de paridad. La paridad con el fuente sintético queda redefinida (SPEC-011 AC-036) a los nueve fixtures por id, porque `SyntheticWorkforceSource` no escala; el catálogo ampliado se verifica contra la tabla base (conteos, ids generados, tipo/estado/resumen de origen) en `PostgresCatalogScopeTests`.

Enmienda de dataset de demostración (2026-10-25, texto del usuario: «dame nombres mas reales quiero que sea lo mas similar a un entorno productivo»; mínimos ratificados por «Si»): los cinco nombres de placeholder se sustituyen por nombres plausibles **ficticios**, y se corrigen apellidos nulos del generador. La carga local queda en 209 empleados (197 activos), `timeoff` visible ≥2.000, ≥20.000 `time_check` dentro de los últimos 45 días UTC y ≥2.000 tareas visibles. Las tareas siguen en `hcm.tasks`, porque el shape real no define tabla de tareas. `db/hcm-real/verify-demo-data.sql` valida los mínimos sin imprimir filas personales. `db/hcm-contract/vps-demo-upgrade.psql` es idempotente y no borra registros ni volúmenes; su preflight comprueba la firma de la semilla ficticia y aborta antes de mutar si la base no coincide. Verificado en contenedor local: seed limpio y dos ejecuciones del bundle mantienen `209/197/2000/20000/2000`; la carga en la VPS sigue pendiente del usuario.

Enmienda de dataset realista y consultable (2026-10-25, texto del usuario: «regenerar los datos demo, podemos borrar los actuales para evitar conflictos, preguntaré cosas como cargo, area, tiempo en la empresa, cantidad por area, cuantos hombres hay en la compania, cuantas mujeres, quien es el jefe de xxxxxx. pero con datos que parezcan reales, no employee-229»). Los 200 generados pasan a tener nombre completo único (mezcla independiente de nombre, apellido y área; antes 197 activos compartían 29 nombres y cada nombre determinaba el área) y un cargo plausible por área. El contrato de `talentassistant_employees` añade `area` (=`department` escalar), `gender`, `years_in_company` (derivado de `hire_date`, como `age_on_next_birthday`) y `manager_employee_id`/`manager_display_name` (desde `vacancy_supervisor`; supuesto: «jefe» = supervisor del puesto). `gender` es un añadido local del dataset ficticio: no está en el shape real pegado y el contrato de producción solo podrá servirlo si el HCM lo expone. Los fixtures 1..9 conservan identidad, cargo, apellido, fecha de alta y cumpleaños, y su `department` pasa de `office-demo-*` a áreas reales; la ubicación de la oficina sigue en `hcm.attendance_incidents`. El cambio de columnas exige recrear la base demo (no admite `CREATE OR REPLACE`); el usuario autorizó borrar los datos actuales.

Enmienda de correos ficticios (2026-10-25, texto del usuario: «podemos hacer que los correos sean algo como nombre.apellido{random}@example.com para que sean mas realistas, ademas para que en caso de pedir el correo de un empleado se vea mas realista»). Los 209 empleados reciben un `first_email` con formato `nombre.apellidoNN@example.com` (minúsculas, sin acentos, sufijo numérico determinista); el contrato lo expone como `email` en el directorio y en el dataset `employees` para responder «el correo de un empleado». `second_email` no se expone. El valor sigue siendo ficticio y usa el dominio reservado `example.com`.
