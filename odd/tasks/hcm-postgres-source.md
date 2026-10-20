# Seguimiento ODD: conector HCM real (PostgreSQL, solo consulta)

Objetivo: consumir los datos del HCM real desde su PostgreSQL con una estrategia de solo consulta correcta y segura —contrato de vistas, consultas fijas, interruptor único de fuente y degradación honesta— detrás de los puertos existentes, sin que un cambio de motor toque la aplicación, con MCP reservado como iteración futura de exposición.
Rama: `feat/hcm-postgres-source`; entrega: `feature-branch-chain`; runner: SDK `10.0.402`, `dotnet build`, `dotnet test` (integración contra PostgreSQL local con el contrato sembrado), smoke Compose; revisión nativa desactivada (`disabled/unmanaged`).

## Specs

### S1 — Contrato de vistas y PostgreSQL de pruebas

Alcance: `db/hcm-contract/` versionado en repo (`views.sql`, `grants.sql`, `seed.sql` con filas sintéticas en el esquema real), servicio `postgres` en Compose (solo desarrollo/pruebas), guía de grants para el DBA. Los conceptos que el HCM no exponga (p. ej. presencia «hoy») se derivan **en la vista, por el HCM** — la aplicación nunca deriva reglas del HCM.

Texto exacto del usuario: “el hcm real tiene datos en una bd de postgres aunque es posible que en el futuro sea otro motor de bd, debemos definir una estrategia de consumo a esa bd de solo consulta por ahora, podria ser un mcp o conexion directa, te lo dejo a tu decision pero que sea una decision correcta y segura”.

Aceptación: el contrato cubre los cinco conceptos de los puertos (directorio, presencia, ausencias, tareas, asistencia), el seed es100% sintético, ningún test ni prueba toca una base real.

### S2 — Adaptadores Npgsql de solo lectura

Alcance: cliente Npgsql con comandos **preparados fijos por consulta** (sin EF, sin SQL dinámico), `PostgresWorkforceSource` y `PostgresAttendanceSource` implementando los puertos existentes, payload bilingüe honesto (si el HCM solo tiene un idioma, se declara igual en ES/EN), `Command Timeout` y búsqueda explícita.

Texto exacto del usuario sobre la elección: “vamos con la opcion 1 pero con la posibilidad de una iteracion futura de mcp”.

Aceptación: AC-036 (modo Postgres responde con el contrato sembrado, modo Sintético con fixtures, nunca mezclado) y AC-037 (SQL fijo por grep, transacción READ ONLY, TLS, logs sin valores de personas).

### S3 — Interruptor único y degradación honesta

Alcance: `Hcm__Source=Sintética|Postgres` gobiern ambos puertos en el registro de DI (en Postgres no se registran los adaptadores sintéticos: imposible el modo mixto); si la base real no responde → error localizado de «fuente no disponible» (paralelo a `NotConfigured`), sin reintentos ciegos y **sin fallback silencioso a sintético**.

Aceptación: AC-038 (prueba de degradación con la base apagada) y revisión de que no existe camino de retorno automático a datos sintéticos.

### S4 — Pruebas y verificación

Alcance: pruebas de integración del adaptador contra la PostgreSQL local con el contrato sembrado (datos sintéticos en el esquema real), pruebas de grants/SQL-fijo/TLS/degradación, suite completa en ambos modos.

Aceptación: suite verde completa; ningún test contacta la base real; evidencia de los pins de seguridad.

### S5 — Documentación y cierre

Alcance: guía para el DBA (vistas, grants, TLS, alcanzabilidad), `.env.example` con `HCM__*` vacías, FR-032/FR-033, NFR-022, AC-036..AC-038 con trazabilidad, ADR-0012 a Aceptado en el registro, visión y estado de SPEC-011. El §10 (egreso de personas reales al proveedor de IA) queda documentado como decisión pendiente no bloqueante del conector.

Aceptación: trazabilidad completa, guía reproducible sin secretos, suite verde y cierre con Risk.

Risk: item 2 (nueva credencial de la base del HCM, grants y manejo de secretos) y item 5 (nueva dependencia Npgsql). T2 lleva verificación independiente al final de la característica; el plan declara además que ninguna prueba toca la base real.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1 | Inline | Implementada; contrato con paridad9/9/3/4/1 y sondas de solo lectura (vista legible, escritura y tablas físicas denegadas); init con ON_ERROR_STOP detectó un alias ausente (corregido) | Commit en esta entrada |
| T2 | S2, S3, S4 | Delegada (context backstop) + reconciliación del padre | Implementada; build0/0 y suite127/127 (10 unit +117 integración) con las dos bases; paridad exacta de oficinas tras corregir la semilla de T1 (`office-demo-*`) | Commit en esta entrada |
| T3 | S5 | `docs/`, `.env.example`, `specs/011-hcm-postgres-source/spec.md` | Implementada | Commit pendiente |

Forecast inicial: aproximadamente 800–1300 líneas authored (contrato SQL, dos adaptadores, cliente, interruptor, pruebas, documentación); archivos generados excluidos. Estrategia `feature-branch-chain`. El §10 de SPEC-011 (egreso con datos reales) se trae como decisión propia antes de cualquier prueba en vivo con personas reales; la implementación y las pruebas de este plan son100% sintéticas.

## Log

### L1 — Solicitud original (2026-10-19)

el hcm real tiene datos en una bd de postgres aunque es posible que en el futuro sea otro motor de bd, debemos definir una estrategia de consumo a esa bd de solo consulta por ahora, podria ser un mcp o conexion directa, te lo dejo a tu decision pero que sea una decision correcta y segura

### L2 — Elección confirmada (2026-10-19)

Texto exacto del usuario: “vamos con la opcion 1 pero con la posibilidad de una iteracion futura de mcp”. Decisión registrada en ADR-0012: conexión directa de solo lectura tras los puertos; MCP como iteración futura de exposición.

### L3 — Aprobación para planificación (2026-10-19)

Texto exacto del usuario: “Aprobar SPEC-011 y ADR-0012 (recomendado)”. Se aprueban `specs/011-hcm-postgres-source/spec.md` y `docs/architecture/adr/ADR-0012-read-only-hcm-consumption.md` para planificar; esto no autoriza cambios de código hasta autorizar tareas. Decisión pendiente explícita del spec §10: egreso de personas reales al proveedor de IA (no bloquea el conector; bloquea pruebas en vivo con personas reales).

### L4 — Evidencia de T1: contrato de vistas y PostgreSQL de pruebas (2026-10-19)

Texto exacto del usuario: “Autorizar T1 (recomendado)”. Implementado en línea. Contrato versionado: `db/hcm-contract/views.sql` (esquema `hcm_contract` con las cinco vistas: directorio, presencia, ausencias, tareas y el insight de asistencia con regla ya evaluada por el origen y pares de idiomas en cada campo localizable), `roles.sql` (rol NOLOGIN de solo lectura + rol de aplicación LOGIN, idempotente y sin contraseñas en el repo), `grants.sql` (USAGE del esquema + SELECT solo sobre las vistas, aplicable igual en la base real), `local/local_tables.sql` (tablas de sustitución solo-para-pruebas), `local/local_seed.sql` (semilla íntegramente sintética con los identificadores, valores y textos bilingües exactos de `SyntheticWorkforceSource`:9 empleados, presencia de hoy relativa a CURRENT_DATE con Pedro en vacación por coherencia,3 ausencias —incluida la calamidad rechazada de Mauricio—,4 tareas y1 insight con evidencia y ausencia relacionada) y `local/01_init.sh` (pipeline explícito con `ON_ERROR_STOP=1` y la contraseña del rol de aplicación desde entorno). Servicio `hcm-postgres` (postgres:17-alpine, solo en loopback `127.0.0.1:5433`, montajes de init y contrato, healthcheck pg_isready) y bloque `HCM_POSTGRES_*` en `.env.example` con valores por defecto solo-locales.

Verificación observada: tras corregir un alias ausente en la vista de asistencia —el init abortó limpio con exit3 gracias a `ON_ERROR_STOP`, que es el comportamiento deseado— y reinicializar el volumen de pruebas recién creado, los conteos de las cinco vistas son `employees=9`, `presence_today=9`, `timeoff=3`, `tasks=4`, `insights=1`; valores clave idénticos a la fuente sintética (`pedro_state=vacation`, `juan_duration=3 ends=2026-10-16`). Sondas de seguridad con el rol de aplicación: lectura de vistas OK (`app_view_read=9`), `INSERT` sobre la vista denegado (`permission denied for view talentassistant_employees`), lectura de la tabla física denegada (`permission denied for schema hcm`) — el alcance del grant es exactamente el contrato. `docker compose config --quiet` en verde. Sin paquetes nuevos; sin tocar código.

Risk: item 2 (manejo de secretos de la base: contraseñas solo vía entorno/`.env`, nunca en el repo) — verificado con las sondas.

### L5 — Evidencia de T2: adaptadores, interruptor y pruebas (2026-10-19)

Texto exacto del usuario: “Autorizar T2 (delegada, recomendado)”. Delegado al writer (context backstop del padre) con el diseño cerrado; recepción del reporte truncada, por lo que los hechos de cierre son los observados por el padre.

Aplicado: único paquete nuevo **Npgsql 10.0.3**; `HcmPostgresOptions.Validate` (fuente Postgres con cadena vacía o valor desconocido → error de arranque; sin fallback silencioso) con test unitario; `HcmSourceUnavailableException` (marcador en Application); extractor compartido `Common/NameMatching` (matcher único de plegado de acentos usado por ambas fuentes — la extracción de `SyntheticWorkforceSource` dejó el comportamiento idéntico); `PostgresWorkforceSource` (5 sentencias `const` con parámetros Npgsql contra las vistas calificadas `hcm_contract.*`, transacción `READ ONLY` por operación, filtro de oficina en SQL con `&& ARRAY[@office_id]`, filtro de nombre en C# con el matcher compartido, resolución de ids con unión ante ambigüedad, selección `_es`/`_en` por idioma) y `PostgresAttendanceSource` (insight de34 columnas con rango `work_date`, jsonb de punches/evidencia/revisión con elección de idioma, ausencia relacionada nula a nulo); conmutador `Hcm:Source` en `Program.cs` que registra ambos puertos sin modo mixto y mapea el marcador a **503 `{"reason":"SourceUnavailable"}`** en los cinco endpoints de datos, con Web extendido (enums de fallo + cadenas localizadas ES/EN en los pares resx de Home y AttendanceInsights).

Pruebas nuevas (12 ficheros en `tests/IntegrationTests/HcmPostgres/` + unitarias): paridad workforce (directorio9 ids/nombres/**oficinas exactas**, estados de hoy por id, timeoff y tareas por referencia en ES y EN), paridad de asistencia (incidente, regla, punches, ausencia relacionada), seguridad (inyección `'; DROP TABLE...` →0 resultados y tabla intacta, INSERT como rol de aplicación denegado con `InsufficientPrivilege`, lectura de tabla física denegada), degradación (puerto cerrado en loopback → marcador), validación de configuración, registro Postgres sin fuentes sintéticas (fábrica con `Hcm__Source=Postgres`) y contrato HTTP en modo Postgres.

Reconciliación del padre: la semilla de T1 usaba `office-madrid`/`office-tokyo` mientras **ambas fuentes sintéticas** usan `office-demo-*` (verificado en `SyntheticWorkforceSource` y `SyntheticHcmAttendanceSource`) — corregida la semilla (10 apariciones), volumen de pruebas reinicializado, y los tests subidos a **paridad exacta** (fuera el comparador `CityToken` y la consulta dual de oficina; aserción de asistencia a `office-demo-madrid`).

Verificación final observada por el padre: `office_sample=office-demo-madrid`, `presence_today=9`, build0/0 con0 avisos, **suite127/127** (10 unit — incluida la ambiental `ArchitectureBoundaryTests`, que esta corrida pasó — +117 integración) con `ConnectionStrings__TalentAssistantTests` y `HcmPostgres__ConnectionString` activas, y `docker compose config` en verde.

Risk: item 2 (credencial de la base: las pruebas se conectan como rol de aplicación de solo lectura; contraseñas solo vía entorno) y item 5 (dependencia Npgsql 10.0.3) — ambos verificados con las sondas y la suite.

### L6 — Cierre de T3 y de la característica (2026-10-19)

Texto exacto del usuario: “Autorizar T3 (recomendado)”. T3 delegado al writer (context backstop del padre; T1/T2 también se delegaron — rutas en la tabla).

Evidencia de cierre (10 ficheros): FR-032/FR-033, NFR-022, AC-036..AC-038 textuales (cotejo programático spec↔catálogo: True/True/True) con trazabilidad, guía «Conector HCM (PostgreSQL, solo consulta)» en local-setup (modos, servicio local, reinicio de volumen, patrón de credenciales nunca impresas, degradación, checklist del DBA con `Ssl Mode=VerifyFull` y aviso de secretos), bloque `Hcm__` vacío en `.env.example` (el bloque local `HCM_POSTGRES_*` intacto), fila ADR-0012 en el registro como Aceptado, ADR-0012 con «Actualizaciones», visión §7 y spec §10/§11 con la decisión de egreso **pendiente** (bloquea solo pruebas en vivo con personas reales). Secretos con regex de forma de clave =0 hits. Suite 127/127 y build0/0 re-verificados en el árbol documental por el writer y con spot check del padre.

Estado: rama `feat/hcm-postgres-source` con4 commits (`065eecb`, `495c4b5`, `e920a33`, cierre de T3), árbol limpio, sin push. Siguiente: verificador independiente por los items de riesgo 2 y 5 nombrados en T2, y decisión de publicación del usuario.

### L7 — Verificación independiente de la característica (2026-10-19)

El verificador validó los cinco claims sobre `9c6883c..6d78234` **sin bloqueos** y con veredicto **MET separado para cada item de riesgo nombrado**: (2) credenciales y grants — SQL siempre `private const string` con vistas calificadas y solo parámetros Npgsql (grep de interpolación en SQL: 0), transacción `READ ONLY` nombrada línea a línea en ambas fuentes, cero credenciales en `src/`, lectura de la cadena solo desde configuración con validación de arranque, ambos-puertos-o-ninguno verificado por test de registro, y degradación 503 `SourceUnavailable` cubierta en los cinco endpoints con pruebas HTTP; sonda en vivo con el rol de aplicación: lectura=9, INSERT denegado, tabla física denegada, oficina `office-demo-madrid`. (5) Npgsql — único `PackageReference` nuevo (`10.0.3` verificado en el assets), proveedor ADO único sin EF, contenido en los dos adaptadores tras los puertos, build net10.0 0 errores.

Suite leída solo: **127/127** (10 unit +117 integración) con ambas bases; filtrado `HcmPostgres`:50 pruebas (5 unit +45 integración) en verde; `docker compose config` en verde; trazabilidad documental completa (AC-036..038 verbatim True/True/True, ADR-0012 Aceptado, §10 del spec **pendiente** como debe ser, guía con `Ssl Mode=VerifyFull` y aviso de secretos); secretos =0 hits; workspace idéntico antes/después.

Advisories honestados: (A1) el claim del padre mencionaba `tests/IntegrationTests/Workforce/**` sin cambios reales (sobreafirmación del alcance, sin defecto); (A2) fallos exóticos de tipo IOException no mapearían a 503 `SourceUnavailable` (los caminos realistas — puerto cerrado y timeouts — son `NpgsqlException` y están probados); (A3) `.env.example` sin salto de final de archivo (cosmético). Ninguno requiere corrección en esta característica.

### L8 — Resolución del §10 y cierre de decisiones (2026-10-19)

Texto exacto del usuario: “1. aceptar groq como fase inicial ya que es una prueba de concepto aun, en una iteracion futura decidiria que hacer si esto entra a fase productiva 2. cuando tenga la bd lo haré 3. ir limpiando las ramas locales”. Registrado: egreso aceptado con Groq para la fase de prueba de concepto (SPEC-011 §10 resuelto, ADR-0012 actualizado, ADR-0011 marcado como resuelto y reabrible antes de fase productiva); la validación contra la base real queda para cuando el usuario aporte credenciales locales y mapeo de vistas; se eliminan las ramas fusionadas locales.

## T4 — Cierre de hallazgos de la revisión de seguridad (2026-10-19)

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T4 | AC-037 reforzado (SEC-001/002/003) | Inline | Implementada | Validación TLS en arranque, init con variable enlazada, nota de guía, tests |

### L9 — Revisión de seguridad y decisión del usuario (2026-10-19)

Texto exacto del usuario (aprobación del skill y lanzamiento): “Si, está bien”. Texto exacto del usuario (decisión sobre el hallazgo candidato T4): “1”. Ejecutada la primera revisión con el skill `security-review` en modo diff sobre `9c6883c..6510104` (informe completo en la conversación): hallazgos SEC-001 (Medium — TLS solo documentado), SEC-002 (Low — credenciales por defecto del servicio de pruebas) y SEC-003 (Low — contraseña interpolada en el SQL de init); controles VC-1..VC-9 verificados con evidencia; NV-1 (advisories de Npgsql) queda pendiente de autorización.

T4 implementa el cierre de los tres hallazgos: `HcmPostgresTls.Validate` en Infrastructure exige `Ssl Mode=VerifyFull` para hosts no-loopback (bucles `localhost`/`127.0.0.1`/`::1` exentos por la base de pruebas local) invocado en el arranque de `Program` tras el validador existente; `01_init.sh` usa la variable enlazada `:'app_password'` de psql (SEC-003); guía con checklist de no-reutilizar credenciales por defecto (SEC-002); AC-037 reforzado a la par en SPEC-011 §7 y en `mvp-acceptance-criteria.md` (texto verbatim), con la fila resumida y el estado del §10 corregidos a “resuelto el 2026-10-19”. Tests nuevos: `PostgresTlsValidationTests` (4 casos: sin Ssl Mode falla, Require más débil falla, VerifyFull pasa, bucles exentos) y arranque con host no-loopback sin TLS que aborta (fábrica `PostgresModeNonLoopbackWithoutTlsFactory`).

Risk: item 2 (se añade un control de seguridad en el arranque — validación de transporte); lleva verificación independiente tras los checks propios.

### L10 — Correcciones tras la verificación de T4 (2026-10-20)

El verificador de T4 validó SEC-001/002/003 (MET, control de arranque real y probado, AC-037 verbatim sincronizado) con UN bloqueo causado por mi cambio: `CS8602` en `HcmPostgresTls.cs` (host anotado nullable). Corrección en una sola tanda: manejo nulo-seguro con error explícito si falta el host, más los tests de regresión propuestos por el verificador (modos `Disable/Allow/Prefer/VerifyCA` deben abortar, `verifyfull` minúsculas pasa por el parseo de Npgsql, `LOCALHOST` en mayúsculas exento, IPv6 completo `0:0:0:0:0:0:0:1` añadido a los bucles, host ausente → error) — build posterior a0 errores y **0 avisos**.

Hallazgo propio durante el refresco del volumen (remedio que el propio test documenta para el desfase de fecha tras la mediodía UTC): el init abortaba con `/bin/sh^M: bad interpreter` — el working copy de `01_init.sh` tenía CRLF (el contenedor monta el working copy, y en Windows `autocrlf` puede reescribirlo), con un volumen inicializado a medias como consecuencia. Causa raíz corregida: script reescrito en LF + **`.gitattributes` nuevo con `*.sh text eol=lf`** (índice y working copy verificados `i/lf w/lf attr/text eol=lf`; los ficheros `.sh` del repo son solo este). Volumen reinicializado con espera explícita del marcador «hcm contract initialized»: `db_today=2026-10-20 presence=9 employees=9 timeoff=3 tasks=4 insights=1` — los2 tests de paridad con desfase de fecha quedaron en verde por el remedio documentado.

Evidencia final: build0/0 sin incremental; suite **integridad total en integración:132/132** (124 +8 casos TLS nuevos); unitarias9/10 con el fallo ambiental conocido reproducido (`FileLoadException` 0x800711C7, App Control). Risk: item 2 — cerrado con verificación independiente (veredicto MET del item) + recheck limitado al bloqueo (CS8602 resuelto).
