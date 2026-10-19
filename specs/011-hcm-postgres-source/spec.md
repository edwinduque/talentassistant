# SPEC-011 — Conector HCM real (PostgreSQL, solo consulta)

- **Estado:** Aprobada para planificación (2026-10-19); implementada en la rama `feat/hcm-postgres-source`.
- **Implementación:** T1, T2 y T3 implementados en la rama `feat/hcm-postgres-source` (commits `065eecb`, `495c4b5`, `e920a33`; cierre documental T3 como commit pendiente). Evidencia: build sin errores y suite completa en verde (127/127: 10 unitarias + 117 de integración) con las dos bases de pruebas activas, paridad exacta entre el contrato sembrado y las fuentes sintéticas, y sondas de solo lectura (lectura de vistas permitida, escritura sobre la vista denegada, lectura de tablas físicas denegada). La validación contra la base real del HCM queda pendiente de los prerrequisitos de §10.
- **Requisitos relacionados:** FR-032, FR-033 y NFR-022. Relacionados existentes: FR-026, FR-027, FR-029; NFR-001, NFR-002, NFR-004, NFR-021.
- **Criterios relacionados:** AC-036, AC-037, AC-038.
- **Decisión arquitectónica:** ADR-0012 aceptado (consumo directo de solo lectura tras los puertos existentes, con exposición MCP como iteración futura).
- **Decisión de producto registrada:** el usuario definió la dirección el 2026-10-19: “el hcm real tiene datos en una bd de postgres aunque es posible que en el futuro sea otro motor de bd, debemos definir una estrategia de consumo a esa bd de solo consulta por ahora, podria ser un mcp o conexion directa, te lo dejo a tu decision pero que sea una decision correcta y segura”, y confirmó la elección con “vamos con la opcion 1 pero con la posibilidad de una iteracion futura de mcp”. Es una ampliación de alcance explícita frente a la regla MVP de datos sintéticos exclusivos.
- **Versión:** 0.1.

## 1. Objetivo

Que la aplicación pueda consumir **datos reales del HCM desde su PostgreSQL** —hoy el origen es sintético (fixtures en código y dataset versionado en MySQL)— mediante una estrategia de **solo consulta**, correcta y segura, sin que ni el modelo ni el código generen SQL arbitrario y sin que un cambio de motor de base de datos toque la aplicación.

## 2. Alcance propuesto

### Incluido

- **Contrato de vistas**: un conjunto de vistas (o, en su defecto, columnas concretas fijas) definidas en la base del HCM y versionadas en este repo como `db/hcm-contract/` — es la capa anti-corrupción que el DBA del HCM posee; esquema de descubrimiento documentado (tareas, columnas, tipos, cobertura de los conceptos que hoy sirven los puertos).
- **Un adaptador de Infrastructure por motor** (`PostgresWorkforceSource` y `PostgresAttendanceSource`) que implementa los puertos existentes (`IHcmWorkforceSource`, fuente de asistencia) con **consultas fijas y parametrizadas** escritas contra las vistas, sin EF para este camino (no es un esquema propio: vistas de solo lectura), usando únicamente el proveedor Npgsql (único paquete nuevo candidato).
- **Interruptor único de fuente** por configuración (`Hcm__Source=Sintética|Postgres`) que gobierna **ambos** puertos a la vez: o todo real o todo sintético — prohibido el modo mixto silencioso.
- **Contrato de seguridad** (cumplimiento verificable): rol de base de datos de la aplicación con SELECT sobre las vistas (grants documentados para el DBA), transacción de solo lectura con *statement timeout*, TLS obligatorio, credenciales solo en `.env`, logs de consulta sin valores de personas, red restringida al host.
- **Degradación honesta**: en modo Postgres, si la base no responde, los endpoints responden un error localizado de «fuente no disponible» — **nunca** vuelven a datos sintéticos por su cuenta (silenciar el origen de los datos sería mentir al usuario).
- **Pruebas deterministas sin datos reales**: contrato probado contra una base PostgreSQL local (servicio incluido) sembrada con filas sintéticas **en el esquema de las vistas** versionado en el repo; ningún test toca la base real del HCM.
- **Decisión de egreso con datos reales** (reabre ADR-0011): punto de aprobación explícito, apartado §10, requisito previo a cualquier prueba en vivo del asistente con personas reales — la implementación del conector no depende de ella.

### Fuera de alcance

- Escrituras de cualquier tipo sobre la base del HCM (lectura exclusiva; el MVP no tiene escrituras autónomas).
- SQL dinámico o consultas generadas por el modelo (regla del proyecto, FR-013/NFR-014).
- Exposición MCP: queda como **iteración futura** (publicar una capa delgada MCP sobre los mismos casos de uso si aparece otro consumidor), sin implementación en esta especificación.
- ETL/CDC o réplica local (alternativa no elegida; viable si la frescura deja de ser prioridad).
- Conexión a motores distintos de PostgreSQL: queda contenida en un adaptador futuro tras el mismo puerto (el objetivo es no depender del motor en Application).
- Traducción automática de campos: donde el HCM solo tenga un idioma, el payload lo declara igual en ambos idiomas; no se inventa traducción sin aprobación.

## 3. Requisitos y límites arquitectónicos

- Application y el asistente **no cambian**: el conector se enchufa a los puertos existentes (ADR-0003); el catálogo de herramientas y los guardrails (SPEC-010) operan igual sobre la fuente real.
- Solo lectura real: grants de la base + transacción `READ ONLY` + consultas fijas parametrizadas; imposible escribir por diseño, no por convención.
- El motor de datos es una decisión de Infrastructure: cambiar de PostgreSQL a otro motor sustituye un adaptador, sin tocar casos de uso, interfaz ni asistente.
- Los datos reales de personas no aparecen en logs, errores ni artefactos del repo; las pruebas usan siempre filas sintéticas.
- Sin credenciales de la base del HCM en el repositorio, en imágenes ni en la conversación: solo `.env` local (NFR-004).

## 4. Flujo local esperado

1. El DBA del HCM crea (o acuerda) las vistas del contrato y un rol de solo lectura; sus credenciales llegan a `.env` (`HCM__*`) por canal local.
2. `Hcm__Source=Postgres` en el entorno local apunta al servidor real (o a la PostgreSQL de desarrollo con el contrato sembrado).
3. «Dime cuando regresa Pedro Rojas» → el mismo caso de uso → `PostgresWorkforceSource` → consulta fija a la vista de ausencias → misma respuesta de antes, ahora con el dato real.
4. Si la base no responde: mensaje localizado de fuente no disponible; la suite y el modo sintético siguen intactos.
5. Una caída o cambio de motor exige tocar solo el adaptador correspondiente.

## 5. Componentes candidatos

| Componente | Responsabilidad propuesta | Línea base que debe conservar |
|---|---|---|
| Contrato `db/hcm-contract/` | Vistas + grants + semilla de prueba, versionados. | Esquema estable; sin datos reales en el repo. |
| Infrastructure — adaptador workforce | Consultas fijas contra vistas → DTOs de los puertos, payload bilingüe honesto. | Sin SQL dinámico; timeouts. |
| Infrastructure — adaptador de asistencia | Ídem para insights/asistencia. | Mismo interruptor; sin datos sintéticos mezclados. |
| Configuración | `Hcm__Source`, `HCM__Host/Port/Database/User/Password/SSLMode/Timeout`. | Secretos fuera de Git; TLS. |
| Degradación | Error localizado de fuente no disponible. | Nunca fallback silencioso a sintético. |
| Pruebas | PostgreSQL local con el contrato + filas sintéticas. | Cero contacto con la base real. |
| Documentación | Guía (DBA, grants, `.env`, modos), FR/AC, registro, visión. | Comandos verificados; sin secretos. |

## 6. Supuestos y puntos que el descubrimiento debe confirmar

1. La base del HCM es PostgreSQL alcanzable desde el host de desarrollo (host/puerto/TLS por confirmar) — prerrequisito que aporta el usuario en `.env`, nunca en la conversación.
2. Existencia de tablas que cubran: directorio de empleados, ausencias/vacaciones con fechas y estados, tareas, marcaciones/asistencia y presencia «hoy» — **si la presencia no existe como tal en el HCM, el DBA la expone derivada en la vista**: la aplicación no deriva presencias ni duraciones (los modelos de negocio son del HCM, principio rector).
3. Identidad estable de empleado y disponibilidad de nombre en uno o dos idiomas (si solo hay uno, se declara igual en ES/EN).
4. Volúmenes pequeños (un directorio y sus ausencias): consultas por id/nombre con índices de la vista; sin paginación masiva en v1.

## 7. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Evidencia futura |
|---|---|---|
| AC-036 | Con `Hcm__Source=Postgres`, las cinco capacidades del asistente responden con los datos de la PostgreSQL local sembrada en el esquema del contrato (mismos formatos de antes), y con `Sintética` responden con los fixtures — nunca se mezclan orígenes en una misma respuesta. | Pruebas de integración del adaptador contra PostgreSQL local y E2E en ambos modos. |
| AC-037 | El rol de la aplicación no puede escribir (grants de solo lectura aplicados), toda petición corre en transacción `READ ONLY` con *statement timeout*, las consultas son fijas y parametrizadas (grep de SQL dinámico vacío), TLS está activo y los logs no contienen valores de personas. | Script de grants revisado, pruebas del cliente (SQL fijo, timeouts) y revisión de logs en el recorrido. |
| AC-038 | Si la base del HCM no responde en modo Postgres, los endpoints devuelven el error localizado de fuente no disponible sin datos sintéticos; la suite completa permanece en verde sin tocar ninguna base real. | Prueba de degradación (base apagada) y suite completa. |

## 8. Diseño propuesto para planificar

1. Descubrimiento inicial y contrato de vistas (`db/hcm-contract/views.sql`, `grants.sql`, `seed.sql` con filas sintéticas de prueba en el esquema real).
2. Cliente Npgsql fijo: comandos preparados por sentencia (una clase por consulta), *connection string* desde configuración con `SSL Mode` exigido, `Command Timeout` y `Search Path` explícito; sin EF en este camino.
3. `Hcm__Source` gobierna los dos puertos en `Program.cs` (registro condicional como ya hace `Attendance__Source`); en Postgres, los adaptadores sintéticos no se registran — imposible el modo mixto.
4. Degradación: excepción de indisponibilidad traducida a respuesta localizada de «fuente no disponible» (paralelo al `NotConfigured` del asistente), sin reintentos ciegos.
5. Servicio `postgres` en Compose solo para desarrollo/pruebas con el contrato sembrado; los tests de integración usan esa instancia (o una por test) con datos sintéticos.
6. Documentación: guía para el DBA (vistas/grants/TLS), `.env.example` con `HCM__*` vacías, catálogos FR/AC, registro ADR-0012, visión.

## 9. Plan SDD propuesto

1. Añadir FR-032 (consulta del HCM real tras los puertos, solo lectura), FR-033 (interrupción honesta: sin fuente real no hay datos sintéticos silenciosos) y NFR-022 (contrato de seguridad de la base: rol de solo vistas, TLS, consultas fijas, logs sin PII) con trazabilidad; ADR-0012 a aprobación.
2. Rama `feat/hcm-postgres-source` desde `main` (`9c6883c`) y plan ODD: T1 (contrato de vistas + PostgreSQL local de pruebas + grants documentados), T2 (adaptadores + interruptor + degradación + pruebas), T3 (documentación y cierre); la validación contra la base real del HCM y cualquier E2E del asistente con personas reales son un paso aparte que exige tus credenciales locales y la decisión de egreso del §10.
3. Verificación: suite completa en ambos modos, prueba de degradación con la base apagada y verificación de los grants.

## 10. Decisión pendiente (aprobación explícita requerida)

**Estado: decisión pendiente; bloquea solo las pruebas en vivo con personas reales.**

**Egreso de datos reales al proveedor de IA.** Con `Hcm__Source=Postgres` y el asistente respondiendo, las preguntas del usuario y los resultados de las herramientas (nombres, ausencias, tareas de personas reales) viajan a Groq. ADR-0011 lo dejó como compromiso a reabrir. Opciones: (a) aceptar el egreso a Groq con este HCM, (b) modo de modelo local (Ollama) para el modo «datos reales» — el cliente ya es compatible OpenAI, es solo configuración, o (c) minimizar/redactar campos antes de enviar. Esta decisión **no bloquea** la implementación del conector (que se prueba con datos sintéticos) pero **sí bloquea** cualquier prueba en vivo del asistente con personas reales.

## 11. Estado y aprobación

SPEC-011 y ADR-0012 fueron aprobados para planificación el 2026-10-19 (log L3); la aprobación no autorizaba cambios de código hasta autorizar tareas, y T1 y T2 se autorizaron después por separado.

Estado de implementación en la rama `feat/hcm-postgres-source`:

| Tarea | Contenido | Commit |
|---|---|---|
| T1 | Contrato de vistas versionado (`db/hcm-contract/`) y base PostgreSQL local de pruebas con semilla sintética y sondas de solo lectura | `065eecb` |
| Planificación | Especificación (SPEC-011) y ADR-0012 | `495c4b5` |
| T2 | Adaptadores Npgsql, interruptor único, degradación honesta y pruebas | `e920a33` |
| T3 | Documentación y cierre (FR/NFR/AC, guía local, `.env.example`, registro, visión) | Commit pendiente |

Evidencia observada: build sin errores, suite completa en verde (127/127: 10 unitarias + 117 de integración) con las dos bases de pruebas activas, paridad exacta con las fuentes sintéticas tras reconciliar la semilla a la convención `office-demo-*`, y sondas de solo lectura (lectura de vistas permitida como rol de aplicación, `INSERT` sobre la vista denegado, lectura de tablas físicas denegada). Queda pendiente la validación contra la base real del HCM —prerrequisitos: PostgreSQL alcanzable, rol de solo lectura sobre las vistas del contrato, credenciales solo en `.env` local y `Ssl Mode=VerifyFull`— y la decisión de egreso del §10, que sigue pendiente y bloquea solo las pruebas en vivo con personas reales.
