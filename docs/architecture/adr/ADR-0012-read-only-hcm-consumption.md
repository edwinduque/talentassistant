# ADR-0012 — Consumo directo de solo lectura del HCM (PostgreSQL) tras los puertos

- **Estado:** Propuesto (2026-10-19).
- **Fecha:** 2026-10-19.
- **Aprobación de arquitectura:** pendiente de aprobación del usuario junto con SPEC-011.
- **Especificación relacionada:** SPEC-011 — Conector HCM real (PostgreSQL, solo consulta).

## Contexto

El HCM real del proyecto almacena sus datos en una base **PostgreSQL**, con posibilidad de cambiar de motor en el futuro, y se necesita una estrategia de consumo **de solo consulta por ahora**. Las opciones planteadas fueron MCP o conexión directa; el usuario encargó una "decisión correcta y segura" y confirmó: **conexión directa con posibilidad de una iteración futura de MCP**.

MCP, aplicado al modelo, quebraría el invariantes ya aprobados (ADR-0003, SPEC-010): el modelo no habla con datos, pide herramientas que su API ejecuta con la sesión del usuario. Un MCP intermedio como *forma de consumo* de nuestra API añadía solo saltos de red y superficie. La alternativa ETL/replica ofrecía desacopplamiento a costa de frescura (presencia "hoy") y un pipeline operativo, y quedó como viable futura.

## Decisión propuesta

- **Conexión directa y de solo lectura desde Infrastructure**, implementando los puertos existentes (`IHcmWorkforceSource` y la fuente de asistencia) con un adaptador PostgreSQL por puerto, escrito con **Npgsql y consultas fijas parametrizadas** (sin EF: no es un esquema propio sino vistas de solo lectura; sin SQL dinámico jamás).
- **Contrato de vistas** (`db/hcm-contract/` versionado en repo): las vistas son la capa anti-corrupción que el DBA del HCM posee; la aplicación solo conoce esas vistas. Si el HCM no expone un concepto (p. ej. presencia), lo expone derivado **la base del HCM**: la aplicación nunca deriva reglas de negocio del HCM.
- **Contrato de seguridad verificable**: rol de la aplicación con SELECT sobre vistas (grants documentados), transacción `READ ONLY` con *statement timeout*, TLS exigido, credenciales solo en `.env`, red restringida, logs sin valores de personas.
- **Interruptor único** `Hcm__Source=Sintética|Postgres` que gobierna ambos puertos: **modo mixto prohibido** y **fallback silencioso a sintético prohibido** (si falla la fuente real, error localizado de «fuente no disponible»: el origen de los datos nunca se oculta).
- **MCP queda como iteración futura de exposición**: si aparece otro consumidor de este contrato (otros agentes o herramientas), se publica una capa MCP delgada sobre los mismos casos de uso — el consumo interno de la aplicación no pasa por MCP hoy.
- La **válida contra la base real** y cualquier prueba del asistente con personas reales exigen credenciales locales del usuario y la decisión de egreso del §10 de SPEC-011 (reabre este ciclo ADR-0011).

## Impacto en decisiones vigentes

- ADR-0003 (límite del servicio de IA): reforzado — la base real se cuelga tras los mismos puertos que ya protegían al asistente.
- ADR-0004 (MySQL/EF): sin cambios para los datos sintéticos; este camino **no** usa EF ni esquema propio — es una fuente de solo lectura concurrente al modo sintético, no un reemplazo del almacén de la aplicación.
- ADR-0006/ADR-0010: las capacidades existentes no cambian de contrato; solo cambia de dónde leen cuando el interruptor lo indica.
- ADR-0011: queda **reabierto para egreso con personas reales** — decisión explícita pendiente en SPEC-011 §10 (Groq aceptado, Ollama local por configuración, o minimización).

## Consecuencias esperadas

### Positivas

- El asistente y la interfaz responden con datos reales **sin cambios** en Application, Web ni catálogos: es la prueba de que la frontera de puertos funcionó.
- La sustitución futura de motor o la incorporación de ETL/MCP tocan exclusivamente Infrastructure.
- Seguridad por diseño: imposible escribir (grants + transacción + consultas fijas), credenciales fuera del repo, pruebas ajenas a datos reales.

### Compromisos

- Nueva clase de secreto en juego: la credencial de la base del HCM (gestión en `.env`, nunca en la conversación ni en el repo).
- Dependencia operativa de disponibilidad de la base del HCM: timeouts cortos y degradación honesta son obligatorios, no opcionales.
- El contrato de vistas requiere coordinación con el DBA del HCM (descubrimiento de esquema en T1); si no puede crear vistas, se fija un conjunto de tablas/columnas concretas igualmente estable.
- Con datos reales entra PII: la decisión de egreso al proveedor de IA (SPEC-011 §10) es bloqueante para uso real conversacional.

## Alternativas consideradas

- **MCP como forma de consumo de la API:** mismo dato, más piezas; descartado para consumo interno, conservado como iteración de exposición futura según instrucción del usuario.
- **MCP dejando que el modelo acceda a la base:** incompatible con ADR-0003, autorización en backend y guardrails de SPEC-010; descartado por principios, no por preferencia.
- **ETL/CDC a réplica propia:** cero credenciales vivas y cero acoplamiento en tiempo real; descartado en v1 por frescura (presencia de hoy) y coste operativo; viable si cambian las prioridades.
- **EF Core con `Npgsql`:** aportaría mapeo y migraciones, que no aplican sobre vistas ajenas; se elige Npgsql crudo con comandos preparados para minimizar dependencias y mantener el SQL fijo y auditable.

## Verificación de la decisión

La decisión se considerará implementada cuando AC-036..AC-038 tengan evidencia observada: capacidades reales y sintéticas en sus modos sin mezcla, grants de solo lectura + transacción `READ ONLY` + TLS + SQL fijo verificados, degradación localizada con la base apagada, suite completa en verde y ningún test tocando la base real. La aprobación de ADR-0012 autoriza planificar; la implementación se ejecuta por tareas.
