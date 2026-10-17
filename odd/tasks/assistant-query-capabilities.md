# Seguimiento ODD: capacidades de consulta del asistente

Objetivo: cerrar los huecos de datos que impiden responder las preguntas de RR. HH. en lenguaje natural (directorio, presencia, TimeOff y tareas por empleado), con un banco de escenarios que mida las respuestas antes de incorporar IA.
Rama: `feat/assistant-query-capabilities`; entrega: `feature-branch-chain`; runner: SDK `10.0.402`, `dotnet build`, `dotnet test`, smoke Compose y navegador ES/EN; revisión nativa desactivada globalmente (`disabled/unmanaged`).

## Specs

### S1 — Directorio y presencia

Alcance: puerto `IHcmWorkforceSource` con búsqueda de empleados por nombre y consulta de presencia por fecha, casos de uso en Application, endpoints de la API protegidos por sesión, fixtures bilingües con nombres de demostración (Juan Pérez, Pedro Rojas, Mauricio Cardenas), panel «Presencia de hoy» en el home y retiro del campo manual de «ID de empleado» en la página de asistencia.

Texto exacto del usuario: “Dime cuales empleados ya estan trabajando. Dime que empleados no han llegado aun. Dime que empleados estan de vacaciones.”

Aceptación: la consulta de presencia devuelve cada empleado con el estado informado por la fuente; las preguntas 1–3 se responden con los fixtures; la búsqueda por nombre es insensible a mayúsculas y devuelve la lista ante varias coincidencias; el home muestra la presencia de hoy en ES/EN; la página de asistencia ya no pide id manual; los endpoints nuevos exigen sesión.

### S2 — TimeOff y tareas por empleado

Alcance: consultas de TimeOff y tareas por empleado (por id o nombre) con contratos propios, endpoints y fixtures bilingües; activación de FR-004.

Texto exacto del usuario: “Dime cuantos dias de vacaciones tiene Juan Perez. Dime cuando regresa Pedro Rojas. DIme que tareas tiene pendiente Mauricio Cardenas.”

Aceptación: las preguntas 4–6 se responden con los valores que informa la fuente (duración, unidad, fecha de fin y estado de tareas), sin cálculos de días ni reglas en Talent Assistant; FR-004 queda implementado y trazado.

### S3 — Límites arquitectónicos

Alcance: la fuente informa estados; Application no depende de Infrastructure; autorización en el backend; datos sintéticos bilingües; base de pruebas separada.

Aceptación: ningún cálculo de duración/calendario en Talent Assistant; `IHcmAttendanceSource` intacto; Domain/Application sin referencias a Infrastructure ni a la interfaz; endpoints nuevos protegidos por sesión; `talentassistant_tests` sin semilla de demostración.

### S4 — Banco de escenarios

Alcance: las seis preguntas literales del usuario convertidas en pruebas automatizadas de API con herramienta y respuesta esperadas, sobre fixtures deterministas.

Texto exacto del usuario: “Has un analisis y dime si vamos por el camino correcto” (el análisis condujo a este banco como evaluación pre-IA).

Aceptación: los escenarios 1–6 existen como pruebas en verde; el banco no depende de IA ni de red externa; sirve de referencia para la evaluación futura del asistente.

### S5 — Documentación y verificación

Alcance: visión, README y estado actual reanclados al norte conversacional; catálogos (FR-026, FR-027, AC-025..AC-028, FR-004 activo); registro de decisiones; guía actualizada.

Aceptación: la documentación comunica el objetivo del asistente con las preguntas del usuario; trazabilidad completa; suite verde; recorrido de navegador en ambos idiomas; verificador independiente sin bloqueos.

Risk: item 2 (nuevos endpoints con autorización de sesión). T1 y T2 requieren verificación independiente tras sus checks propios.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1, S3 | Inline | Implementada; suite 36/36, endpoints con 401 sin sesión, presencia en el home y poda verificada; verificador independiente 5/5 sin bloqueos | `46fadc0a25ec5ccfee17f5a98d6b2d7b1c756f77` |
| T2 | S2, S3 | Inline | Planificada | TimeOff y tareas por empleado + fixtures + endpoints + pruebas |
| T3 | S4, S5 | Inline | Planificada | Depende de T1/T2 |

Forecast inicial: aproximadamente 1200–1800 líneas authored entre capacidades, fixtures, pruebas, interfaz y documentación; archivos generados excluidos. Estrategia `feature-branch-chain`; el umbral de ~400 líneas es heurístico, no un límite de tamaño.

## Log

### L1 — Solicitud original (2026-10-17)

Ayudame con algo, me he dado cuenta que el proyecto está perdiendo la escencia, si leo la documentación ni yo entiendo el objetivo del proyecto. QUe es lo que yo quiero inicialmente: imaginate que tengo un sistema hcm(human capital management) con muchos datos de timechecks, timeoff, tasks, employees. yo como empleado de rrhh quiero tener un asistente tipo chatbot el cual me ayude a tener la informacion mucho mas rapido. Te doy ejemplos: Dime cuales empleados ya estan trabajando. Dime que empleados no han llegado aun. Dime que empleados estan de vacaciones. Dime cuantos dias de vacaciones tiene Juan Perez. Dime cuando regresa Pedro Rojas. DIme que tareas tiene pendiente Mauricio Cardenas. Esto es a lo que quiero llegar, aun no hemos integrado una ai pero quiero que vayamos hacia ese punto, creo que hay cosas en la pantalla de asistente que sobran y hay cosas que se pueden mejorar. Has un analisis y dime si vamos por el camino correcto, si nos estamos saliendo del alcance o si debemos ajustar algo antes de ir integrandonos a la ia

### L2 — Decisión de punto de partida (2026-10-17)

Texto exacto del usuario: “Norte + capacidades de datos (recomendado)”. SPEC-008 con las preguntas literales como escenarios y evaluación, capacidades de consulta que las resuelven y poda de la consola dentro de la entrega; todo sintético, sin IA todavía.

### L3 — Aprobación para planificación (2026-10-17)

Texto exacto del usuario: “Aprobar SPEC-008 y ADR-0010 (recomendado)”. Se aprueban `specs/008-assistant-query-capabilities/spec.md` y `docs/architecture/adr/ADR-0010-assistant-query-capabilities.md` para planificar; esto no autoriza cambios de código hasta autorizar tareas.

### L4 — Implementación y verificación de T1 (2026-10-17)

Texto exacto del usuario: “Autorizar T1 (recomendado)”.

Implementado: puerto `IHcmWorkforceSource` con `EmployeeSearchQuery`/`PresenceQuery` (ambos con `Language` por defecto `es`) y casos de uso `SearchEmployees`/`GetEmployeePresence`; fuente workforce sintética que deriva los5 empleados con incidentes del catálogo de asistencia (nombres idénticos entre idiomas) y añade a Juan Pérez, Pedro Rojas y Mauricio Cardenas, con presencia explícita para hoy y +1/+2 días anclada a la fecha local (otros días → vacío, nunca inventado); endpoints `/employees` y `/presence` con sesión obligatoria y resolución del idioma desde el perfil; métodos en el cliente de Web con su patrón de fallo tipado; panel «Presencia de hoy» en el home con estados localizados (Trabajando/No ha llegado/De vacaciones/Ausente con fallback al valor crudo); retiro del campo «ID de empleado» de la página de asistencia (la API sigue soportando `employeeId` para futuras herramientas).

Evidencia: build 0 warnings/0 errores; suite completa 36/36 (5 unit + 31 integración) con pruebas nuevas de búsqueda por nombre (insensible a mayúsculas; hallazgo: el orden mayúsculas dobla casos pero no acentos, corregido en el test), directorio completo bilingüe, múltiples coincidencias, presencia de hoy que responde las tres preguntas, fecha desconocida vacía y nombres por idioma; smoke API: `/employees` y `/presence` sin sesión → 401, `?name=juan` → Juan Pérez, `presence?date=hoy` → 8 empleados con `working/not-arrived/vacation`; navegador: panel en español con los tres estados y nombres de demostración, página de asistencia sin campo de id, consola sin errores.

Risk: item 2 (nuevos endpoints que añaden superficie de autorización de sesión).

### L5 � Verificaci�n independiente de T1 (2026-10-17)

El verificador valid� los cinco claims sin bloqueos: alcance del diff acotado (15 ficheros, sin cambios en Domain ni csproj, sin secretos); superficie de autorizaci�n en vivo (401 sin token en /employees y /presence, 200 con token); sin derivaci�n de reglas (est�ticos por fecha, fecha desconocida ? vac�o, directorio derivado del cat�logo compartido y nombres de demostraci�n id�nticos entre idiomas); comportamiento de API (8 empleados, name=juan ? Juan P�rez, presencia de hoy con los tres estados, fecha inv�lida ? 400 con texto exacto, insights sin regresi�n y employeeId sigue soportado en el contrato); y suite 36/36 desde el workspace.

Advisories registrados: el ancla est�tica `Today` congela la presencia de un proceso largo al cruzar medianoche (dentro del supuesto �8 del spec; candidato a mejora futura); la b�squeda no pliega acentos (la S1 solo exige insensibilidad a may�sculas); y las respuestas HTTP de validaci�n no tienen pruebas automatizadas (cubiertas por probes). Se difiere el test de oficina propuesto.

Risk: item 2 (nuevos endpoints con autorizaci�n de sesi�n) � verificado sin bloqueos.
