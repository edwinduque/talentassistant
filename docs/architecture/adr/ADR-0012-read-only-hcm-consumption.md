# ADR-0012 — Consumo directo de solo lectura del HCM (PostgreSQL) tras los puertos

- **Estado:** Aceptado (2026-10-19); excepción dinámica acotada aprobada por ADR-0014 (2026-10-25). Nota de herencia (2026-10-21): la persistencia propia corre ahora sobre PostgreSQL ([ADR-0013](ADR-0013-postgresql-persistence.md)); el contrato de vistas del HCM no cambia.
- **Fecha:** 2026-10-19.
- **Aprobación de arquitectura:** aprobado por el usuario el 2026-10-19 junto con SPEC-011 (log L3 del plan ODD).
- **Especificación relacionada:** SPEC-011 — Conector HCM real (PostgreSQL, solo consulta).

## Contexto

El HCM real del proyecto almacena sus datos en una base **PostgreSQL**, con posibilidad de cambiar de motor en el futuro, y se necesita una estrategia de consumo **de solo consulta por ahora**. Las opciones planteadas fueron MCP o conexión directa; el usuario encargó una "decisión correcta y segura" y confirmó: **conexión directa con posibilidad de una iteración futura de MCP**.

MCP, aplicado al modelo, quebraría el invariantes ya aprobados (ADR-0003, SPEC-010): el modelo no habla con datos, pide herramientas que su API ejecuta con la sesión del usuario. Un MCP intermedio como *forma de consumo* de nuestra API añadía solo saltos de red y superficie. La alternativa ETL/replica ofrecía desacopplamiento a costa de frescura (presencia "hoy") y un pipeline operativo, y quedó como viable futura.

## Decisión aceptada (enmendada por ADR-0014)

- **Conexión directa y de solo lectura desde Infrastructure**, implementando los puertos existentes con adaptadores PostgreSQL escritos con **Npgsql** (sin EF: son vistas de solo lectura). Las operaciones existentes mantienen consultas fijas parametrizadas. Como excepción de ADR-0014, `query_hr_data` permite SQL dinámico compilado solo por el backend desde un plan validado; el modelo nunca envía SQL libre y todos los valores se parametrizan.
- **Contrato de vistas** (`db/hcm-contract/` versionado en repo): las vistas son la capa anti-corrupción que el DBA del HCM posee; la aplicación solo conoce esas vistas. Si el HCM no expone un concepto (p. ej. presencia), lo expone derivado **la base del HCM**: la aplicación nunca deriva reglas de negocio del HCM.
- **Contrato de seguridad verificable**: rol de la aplicación con SELECT sobre vistas (grants documentados), transacción `READ ONLY` con *statement timeout*, identificadores resueltos desde metadatos confiables, parámetros para todo valor, TLS exigido, credenciales solo en `.env`, red restringida, logs sin valores de personas.
- **Interruptor único** `Hcm__Source=Sintética|Postgres` que gobierna ambos puertos: **modo mixto prohibido** y **fallback silencioso a sintético prohibido** (si falla la fuente real, error localizado de «fuente no disponible»: el origen de los datos nunca se oculta).
- **MCP queda como iteración futura de exposición**: si aparece otro consumidor de este contrato (otros agentes o herramientas), se publica una capa MCP delgada sobre los mismos casos de uso — el consumo interno de la aplicación no pasa por MCP hoy.
- La **válida contra la base real** y cualquier prueba del asistente con personas reales exigen credenciales locales del usuario y la decisión de egreso del §10 de SPEC-011 (reabre este ciclo ADR-0011).

## Impacto en decisiones vigentes

- ADR-0003 (límite del servicio de IA): reforzado — la base real se cuelga tras los mismos puertos que ya protegían al asistente.
- ADR-0004 (MySQL/EF): sin cambios para los datos sintéticos; este camino **no** usa EF ni esquema propio — es una fuente de solo lectura concurrente al modo sintético, no un reemplazo del almacén de la aplicación.
- ADR-0006/ADR-0010: las capacidades existentes no cambian de contrato; solo cambia de dónde leen cuando el interruptor lo indica.
- ADR-0011: queda **reabierto para egreso con personas reales** - decisión explícita **resuelta el 2026-10-19** en SPEC-011 §10 (Groq aceptado para la fase de prueba de concepto; reabre antes de la fase productiva).

## Consecuencias esperadas

### Positivas

- La conexión de datos original permitió responder con los mismos contratos en Application y Web. La capacidad genérica posterior se añade conforme a ADR-0014 y sigue usando los puertos/fuente seleccionados.
- La sustitución futura de motor o la incorporación de ETL/MCP tocan exclusivamente Infrastructure.
- Seguridad por diseño: la gramática y los grants impiden escritura; SQL dinámico solo se compone desde identificadores confiables y valores parametrizados, credenciales fuera del repo y pruebas ajenas a datos reales.

### Compromisos

- Nueva clase de secreto en juego: la credencial de la base del HCM (gestión en `.env`, nunca en la conversación ni en el repo).
- Dependencia operativa de disponibilidad de la base del HCM: timeouts cortos y degradación honesta son obligatorios, no opcionales.
- El contrato de vistas requiere coordinación con el DBA del HCM (descubrimiento de esquema en T1); si no puede crear vistas, se fija un conjunto de tablas/columnas concretas igualmente estable.
- Con datos reales entra PII: la decisión de egreso al proveedor de IA (SPEC-011 §10) es bloqueante para uso real conversacional.

## Alternativas consideradas

- **MCP como forma de consumo de la API:** mismo dato, más piezas; descartado para consumo interno, conservado como iteración de exposición futura según instrucción del usuario.
- **MCP dejando que el modelo acceda a la base:** incompatible con ADR-0003, autorización en backend y guardrails de SPEC-010; descartado por principios, no por preferencia.
- **ETL/CDC a réplica propia:** cero credenciales vivas y cero acoplamiento en tiempo real; descartado en v1 por frescura (presencia de hoy) y coste operativo; viable si cambian las prioridades.
- **EF Core con `Npgsql`:** aportaría mapeo y migraciones, que no aplican sobre vistas ajenas; se mantiene Npgsql crudo. El SQL fijo sigue siendo la opción para las operaciones existentes; la excepción de consulta dinámica está limitada por ADR-0014.

## Verificación de la decisión

La decisión original se consideró implementada cuando AC-036..AC-038 verificaron modos de fuente sin mezcla, grants de solo lectura, transacción `READ ONLY`, TLS, consultas parametrizadas, degradación y suite. La excepción dinámica aprobada por ADR-0014 se verifica además mediante AC-055..AC-059 de SPEC-020; no habilita SQL libre ni escritura.

## Actualizaciones (2026-10-19)

Implementado en la rama `feat/hcm-postgres-source` como interruptor `Hcm:Source` que registra ambos puertos sin modo mixto, adaptadores Npgsql 10.0.3 con comandos fijos parametrizados sobre las vistas del contrato (`db/hcm-contract/`, con la semilla reconciliada a la convención `office-demo-*`) y degradación a 503 `{"reason":"SourceUnavailable"}` localizado. Verificado con la suite completa en verde (127/127: 10 unitarias + 117 de integración) con las dos bases de prueba activas, compilación sin errores y sondas de solo lectura (lectura de vistas permitida, escritura denegada, tablas físicas denegadas). Commits: `065eecb` (contrato de vistas + base local de pruebas), `495c4b5` (SPEC-011 y ADR-0012) y `e920a33` (adaptadores, interruptor y pruebas); el cierre documental (T3) queda como commit pendiente. Siguen pendientes la validación contra la base real del HCM (credenciales locales y `Ssl Mode=VerifyFull`). **Resolución del §10 (2026-10-19):** egreso aceptado con Groq para la fase de prueba de concepto; se reabre antes de la fase productiva con personas reales.
