# SPEC-008 — Capacidades de consulta del asistente y su corpus de preguntas

- **Estado:** Aprobado; implementado y verificado (2026-10-17).
- **Implementación:** T1 y T2 implementadas y verificadas; T3 (banco de escenarios y documentación) implementado; pendiente E2E final y verificación independiente.
- **Requisitos relacionados (propuestos):** FR-026 y FR-027. Relacionados existentes: FR-002, FR-003, FR-004, FR-006, FR-010, FR-011, FR-025.
- **Criterios relacionados (propuestos):** AC-025, AC-026, AC-027 y AC-028.
- **Decisión arquitectónica:** ADR-0010 propuesto.
- **Decisión de producto registrada:** el usuario eligió “Norte + capacidades de datos (recomendado)” el 2026-10-17, con el análisis de realineamiento del proyecto.
- **Versión:** 0.1.

## 1. Objetivo

Reanclar el proyecto en su norte —un asistente para responsables de RR. HH. que respondan preguntas en lenguaje natural sobre los datos del HCM— y cerrar los huecos de datos que hoy impiden responder esas preguntas. El asistente conversacional con IA llega después, con proveedor aprobado; esta especificación construye la **superficie de consultas** (las herramientas que el futuro asistente llamará) y congela el **corpus de preguntas del usuario como evaluación**.

## 2. Alcance propuesto

### Incluido

- Nuevo puerto de capacidades de consulta en Application, con casos de uso y contratos propios:
  - Directorio de empleados con búsqueda por nombre (y por oficina).
  - Estado de presencia de los empleados para una fecha (trabajando, no ha llegado, ausente, de vacaciones), informado por la fuente HCM.
  - Solicitudes de TimeOff por empleado (referencia, tipo, estado, fechas, duración y unidad).
  - Tareas por empleado (referencia, título, estado, fecha límite si existe) — activa FR-004.
- Endpoints de la API para estas capacidades, protegidos por sesión (el futuro chatbot los llamará como herramientas autorizadas por el backend).
- Fixtures sintéticos bilingües para las nuevas capacidades, con nombres reconocibles de demostración (Juan Pérez, Pedro Rojas, Mauricio Cardenas, y Juan Herrera como segundo «Juan» para exigir desambigüación) junto a los empleados existentes. *(Enmienda 2026-10-18.)*
- Mejora visible en la interfaz: panel de «Presencia de hoy» en el home y retiro del campo manual de «ID de empleado» en la página de asistencia (sustituido por la búsqueda por nombre disponible en las nuevas capacidades).
- Corpus de evaluación pre-IA: las preguntas literales del usuario convertidas en escenarios automatizados (herramienta esperada y respuesta esperada contra los fixtures).
- Actualización de la documentación de producto: visión, README y estado actual reanclados al norte conversacional.

### Fuera de alcance

- El chat, los modelos de lenguaje, la selección de proveedor y los agentes: SPEC posterior una vez aprobado el proveedor (FR-012, FR-016, FR-017 y sus límites siguen vigentes).
- Saldos, calendarios, cálculo de días hábiles o cualquier regla del HCM: la fuente informa; Talent Assistant no calcula.
- Gestión de ausencias o tareas (crear, aprobar, modificar): el HCM conserva sus procesos.
- Roles y permisos más allá de «usuario autenticado», notificaciones y conectores HCM reales.

## 3. Requisitos y límites arquitectónicos

- El HCM sigue siendo el sistema de verdad: los estados de presencia, los tipos y estados de TimeOff y los estados de tareas llegan como valores de la fuente; Talent Assistant no deriva ni infiere reglas (consistente con FR-008 y SPEC-002).
- Las capacidades se exponen como operaciones tipadas detrás de `IHcmWorkforceSource` (nombre propuesto), sin romper el puerto existente de asistencia; Application no depende de Infrastructure ni de la interfaz (NFR-001/NFR-002).
- Los endpoints nuevos exigen sesión (política de autorización vigente); serán las herramientas que el asistente invocará, con autorización siempre en el backend.
- Los datos siguen siendo sintéticos, bilingües (es/en, según el usuario) y deterministas; `talentassistant_tests` no recibe la semilla.
- Búsqueda por nombre: coincidencia insensible a mayúsculas **y acentos** (ambos lados se normalizan antes de comparar, para que «Juan Perez» localice a «Juan Pérez»; enmienda 2026-10-18) que contiene el texto, adecuada mientras el dataset sea pequeño (mismo criterio ya asumido para el filtro por JSON); múltiples coincidencias devuelven la lista, no eligen en silencio.

## 4. Preguntas del usuario como escenarios

Las siguientes preguntas literales guían los criterios y el banco de pruebas:

1. «Dime cuales empleados ya estan trabajando.» → consulta de presencia para hoy; respuesta: lista de empleados en estado «trabajando».
2. «Dime que empleados no han llegado aun.» → consulta de presencia para hoy; respuesta: lista con estado «no ha llegado».
3. «Dime que empleados estan de vacaciones.» → consulta de presencia para hoy; respuesta: lista con estado «de vacaciones».
4. «Dime cuantos dias de vacaciones tiene Juan Perez.» → búsqueda de empleado por nombre y consulta de sus solicitudes de TimeOff; respuesta: duración y unidad de la solicitud vigente según la fuente.
5. «Dime cuando regresa Pedro Rojas.» → búsqueda por nombre y consulta de TimeOff; respuesta: fecha de fin de la ausencia vigente.
6. «Dime que tareas tiene pendiente Mauricio Cardenas.» → búsqueda por nombre y consulta de tareas; respuesta: tareas con estado que la fuente marca como pendientes.

Cada escenario se automatiza en pruebas contra los fixtures, con la misma forma que usará el asistente (búsqueda → herramienta → respuesta), de modo que el banco sirva de evaluación antes y después de incorporar IA.

## 5. Componentes candidatos

| Componente | Responsabilidad propuesta | Línea base que debe conservar |
|---|---|---|
| Application — capacidades | Puerto `IHcmWorkforceSource` y casos de uso de directorio, presencia, TimeOff y tareas. | Sin dependencia de Infrastructure; contratos propios y sustituibles. |
| Infrastructure — fixtures | Ampliar el catálogo bilingüe con empleados, presencia por fecha, solicitudes y tareas. | Mismo patrón de textos por clave e idioma; identificadores compartidos. |
| Infrastructure — fuente workforce | Implementar el puerto sobre los fixtures (y más adelante sobre el HCM real). | La fuente informa estados; no calcula reglas. |
| API — endpoints | Endpoints de consulta protegidos por sesión, con errores controlados. | Formato de error coherente con los endpoints existentes. |
| Web — home y asistencia | Panel «Presencia de hoy» y retiro del campo de id manual. | Interfaz localizada ES/EN; sin acceso a base de datos. |
| Pruebas — escenarios | Banco de preguntas del usuario como pruebas de extremo a extremo de API. | Deterministas sobre fixtures; sin dependencia de IA. |
| Documentación | Visión, README y estado actual reanclados al norte; guía actualizada. | Comandos verificados; sin secretos. |

## 6. Contratos propuestos (Application)

- `HcmEmployeeDirectoryEntry`: `Id`, `DisplayName`, `OfficeIds`.
- `HcmEmployeePresence`: `Employee` (referencia), `State` (valor de la fuente, p. ej. `working`, `not-arrived`, `vacation`, `absent`), `Summary` opcional de la fuente.
- `HcmPresenceSnapshot`: `Date` y colección de `HcmEmployeePresence`.
- `HcmTimeOffRequest`: `Reference`, `EmployeeId`, `Type`, `Status`, `StartsOn`, `EndsBefore`, `Duration`, `DurationUnit`, `Summary`.
- `HcmTask`: `Reference`, `EmployeeId`, `Title`, `Status`, `DueOn` opcional, `Summary`.
- Consultas: `EmployeeSearchQuery(Name?, OfficeId?)`; `PresenceQuery(Date)`; `EmployeeDataQuery(EmployeeId? | Name?)`.

## 7. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Evidencia futura |
|---|---|---|
| AC-025 | La consulta de presencia para una fecha devuelve cada empleado con el estado informado por la fuente; «trabajando», «no ha llegado» y «de vacaciones» responden las preguntas 1–3 con los fixtures. | Pruebas de escenario 1–3 contra la API. |
| AC-026 | La búsqueda por nombre localiza a Juan Pérez y Pedro Rojas **y es insensible a acentos («Juan Perez» sin acento localiza a Juan Pérez); una búsqueda corta compartida («Juan») devuelve a los dos Juans sin elegir en silencio**; sus respuestas de TimeOff informan días y fecha de regreso tal como los declara la fuente, sin cálculos de Talent Assistant. *(Enmienda 2026-10-18.)* | Pruebas de escenario 4–5, prueba de acentos y de doble «Juan», y verificación de que no hay lógica de cálculo. |
| AC-027 | Las tareas de Mauricio Cardenas se consultan por nombre y la fuente determina qué significa «pendiente»; FR-004 queda implementado. | Pruebas de escenario 6 y trazabilidad FR-004. |
| AC-028 | El banco de preguntas automatizado cubre las seis preguntas literales, la suite completa permanece verde, el home muestra «Presencia de hoy», la página de asistencia ya no pide id manual y todo el contenido nuevo existe en español e inglés. | Suite completa, recorrido de navegador y revisión de la poda. |

## 8. Diseño propuesto para planificar

1. Nuevo puerto `IHcmWorkforceSource` en Application; el puerto de asistencia existente no cambia.
2. Fuente workforce implementada sobre fixtures ampliados; los estados llegan como valores de la fuente.
3. Endpoints de la API: `/employees`, `/presence`, `/timeoff`, `/tasks`, con sesión obligatoria y errores controlados coherentes con los existentes.
4. Los fixtures añaden empleados de demostración (Juan Pérez, Pedro Rojas, Mauricio Cardenas y Juan Herrera —segundo «Juan» para desambigüación—) más presencia de hoy, solicitudes de TimeOff y tareas, en los dos idiomas.
5. Web: panel de presencia en el home y retiro del campo de id en asistencia.
6. Banco de escenarios automatizados con las seis preguntas; visión/README/estado actual reanclados.

Supuestos explícitos para aprobación: la demostración usa nombres propios ficticios claramente sintéticos; «presencia de hoy» usa la fecha del entorno local como en los fixtures; el chat llega en una especificación posterior con proveedor aprobado; no se prometen saldos de vacaciones más allá de lo que informe la fuente.

## 9. Plan SDD propuesto

1. Añadir FR-026 y FR-027 y los AC-025..AC-028 con trazabilidad, y marcar FR-004 como activo.
2. Aceptación de ADR-0010 y actualización del registro de decisiones.
3. Rama `feat/assistant-query-capabilities` desde `main` y plan ODD: T1 (directorio, presencia, panel y poda), T2 (TimeOff y tareas por empleado), T3 (banco de escenarios, documentación y verificación final).
4. Verificación: suite completa, escenarios automatizados, recorrido de navegador y verificador independiente por tocar autorización de endpoints nuevos.
5. Cierre con matriz requisito/criterio/evidencia y commits por work unit.

## 10. Estado y aprobación

SPEC-008 y ADR-0010 quedan propuestos a la espera de aprobación para planificación; la aprobación no autoriza cambios de código hasta autorizar tareas.

## 11. Verificación de T3

T3 documenta y reancla la narrativa de producto; sus artefactos:

- `docs/requirements/functional-requirements.md`: FR-026 y FR-027 añadidos con su aprobación vía SPEC-008/ADR-0010 y FR-004 activo.
- `docs/acceptance/mvp-acceptance-criteria.md`: AC-025..AC-028 con sus secciones y filas de trazabilidad (AC-025 → FR-026/FR-006; AC-026 → FR-027/FR-008; AC-027 → FR-004/FR-027; AC-028 → FR-011/NFR-015).
- `docs/decisions/decision-register.md`: ADR-0010 registrado como aceptado e implementado con limitaciones documentadas.
- `docs/product/vision.md`: sección 7 con el estado respecto a la visión; el norte conversacional permanece.
- `README.md`: estado actual reanclado a SPEC-008 con el banco y el resultado de la suite.
- `docs/development/local-setup.md`: sección «Consultas del asistente (API)» con los cuatro endpoints, obtención de token y ejemplo de curl.

Banco de escenarios (S4): `tests/TalentAssistant.IntegrationTests/Workforce/AssistantScenarioBenchTests.cs` automatiza las seis preguntas literales del usuario (§4) como pruebas deterministas sobre los fixtures, sin IA ni red externa:

1. «Dime cuales empleados ya estan trabajando.»
2. «Dime que empleados no han llegado aun.»
3. «Dime que empleados estan de vacaciones.»
4. «Dime cuantos dias de vacaciones tiene Juan Perez.»
5. «Dime cuando regresa Pedro Rojas.»
6. «Dime que tareas tiene pendiente Mauricio Cardenas.»

El recorrido de navegador E2E final y la verificación independiente quedan pendientes y los registra el orquestador en el log ODD; esta sección no los declara completados.

El recorrido de navegador final validó el panel de presencia en español e inglés (estados localizados y nombres propios compartidos), la restauración del perfil a español y el logout al login en español, con la consola sin errores. El banco de los seis escenarios quedó registrado en 	ests/TalentAssistant.IntegrationTests/Workforce/AssistantScenarioBenchTests.cs (suite de integración 43/43). Commits de T3: 9ee00d96d61497c2476f3bb8a0d019c0f4c89f9 (banco y documentación) y el de esta evidencia.

Enmienda 2026-10-18 (AC-026): verificada por `UnaccentedNameFoldsToTheAccentedDisplayName`, `SharedFirstNameReturnsBothJuansWithoutChoosing`, `OfficeIdMatchingStaysExactAndUnfolded` y `AccentFoldingFlowsThroughTimeOffAndTasksByIdResolution`, más la pasada en vivo de las siete preguntas del asistente (`scripts/verify-assistant-questions.ps1 -PaceSeconds 9`, salida `0`).