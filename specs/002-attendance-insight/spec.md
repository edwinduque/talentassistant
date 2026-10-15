# SPEC-002 — Explicación de incidencias de marcaje del HCM

- Estado: Implementada; verificación parcial (2026-10-15). La aceptación automatizada sigue pendiente.
- Aprobación: el usuario confirmó SPEC-002 y autorizó implementar solo este caso de uso. Las reglas detalladas adicionales de TimeOff quedan aplazadas.
- Versión: 0.1
- Objetivo: definir una primera vertical de asistencia que consulta una fuente HCM sintética y explica una incidencia con su evidencia.

## 1. Contexto y frontera de responsabilidad

Talent Assistant es una capa de asistencia integrada con un HCM existente; no es un HCM ni reemplaza sus procesos. El HCM es la fuente de verdad para empleados, jornadas, marcajes, TimeOff, Tasks y reglas de negocio.

Esta especificación define que Talent Assistant consulta un resultado de incidencia calculado por el HCM (representado por una fuente sintética), lo relaciona con la información pertinente disponible y presenta qué ocurrió, qué evidencia existe y qué falta revisar. No reproduce el motor de reglas del HCM ni crea flujos de fichaje, aprobación, ausencia o tareas.

## 2. Alcance aprobado para implementación

### Incluido

- Consultar empleados sintéticos, su asociación con oficinas (país y ciudad), jornadas y marcajes mediante un contrato que representa las respuestas HCM.
- Recibir de la fuente el resultado de la regla HCM, su referencia/versión, hechos de soporte, procedencia y elementos que requieran revisión.
- Relacionar datos de TimeOff solo cuando la fuente los exponga como pertinentes para el caso.
- Presentar una explicación verificable al responsable de RR. HH. mediante API y página Web.
- Usar una fuente sintética sustituible; una integración HCM real queda fuera de esta propuesta y requiere aprobación posterior.

### Fuera de alcance

- Crear o administrar empleados, horarios, marcajes, TimeOff o Tasks como procesos de negocio de Talent Assistant.
- Calcular de nuevo reglas de marcaje, aprobar registros manuales o programar notificaciones.
- Conectar con un HCM corporativo real o utilizar datos reales.
- Usar LLM, framework de agentes, Python, MCP o n8n.
- Implementar un caso de consulta de Tasks; sus datos pueden añadirse a un contrato futuro.

## 3. Reglas de TimeCheck pertenecientes al HCM

La revisión documental no encontró una especificación existente para las reglas indicadas a continuación. Se registran como conceptos comunicados para revisión, no como comportamiento ya aprobado ni como reglas locales de Talent Assistant:

| Concepto comunicado | Propietario | Detalles pendientes |
|---|---|---|
| Margen de gracia de 0 a 30 minutos por jornada | HCM | Valor efectivo, inclusión de límites, condiciones, excepciones y zona horaria |
| Ventana de almuerzo | HCM | Inicio/fin, reglas de aplicación y relación con marcajes |
| Aprobación de marcajes manuales | HCM | Estados, actores, transiciones y efecto sobre la evaluación |
| Notificación de errores a las 09:00 del siguiente día hábil | HCM | Zona horaria, calendario aplicable, disparador y destinatario |

La fuente sintética debe devolver el resultado de evaluación del HCM y la evidencia que la sustenta. No debe inventar valores por defecto para las decisiones pendientes. Talent Assistant presenta esos valores o resultados y no ejecuta una segunda evaluación.

## 4. Contrato de integración conceptual aprobado

Los nombres de interfaz y modelos conceptuales quedan aprobados para implementar la vertical; los tipos técnicos mínimos se concretan en el código sin fijar códigos, estados ni semánticas de reglas que permanecen pendientes.

- Puerto en Application: `IHcmAttendanceSource`.
- Adaptador del MVP en Infrastructure: `SyntheticHcmAttendanceSource`.
- Consulta conceptual: `GetAttendanceInsightContext(employeeId?, from, to)`.
- Respuesta conceptual: un `HcmAttendanceContext` que contiene referencias sintéticas de empleados y su asociación con oficinas, una colección de oficinas con país, ciudad e identificador de zona horaria IANA, jornadas, marcajes, resultados de reglas del HCM, evidencia/procedencia, elementos que requieren revisión y TimeOff relacionado cuando la fuente lo indique.

El resultado de regla debería incluir, como mínimo conceptual, una referencia/versionado de regla, resultado del HCM, explicación o hechos de soporte, fecha de evaluación y referencias de evidencia. Cada oficina incluye país, ciudad e identificador de zona horaria IANA; los datos de empleado contienen una referencia a la(s) oficina(s) indicada(s) por la fuente. No se fijan aún la cardinalidad, códigos de regla, enumeraciones de estado, códigos canónicos de país ni formato productivo.

El caso de uso de Application dependerá solo del puerto. Infrastructure podrá sustituir la fuente sintética por un adaptador real sin reescribir el caso de uso. Para esta vertical, Infrastructure usará un fixture en memoria determinista; no se crea esquema HCM ni migración MySQL. Si el volumen sintético crece, se evaluará un script generador en una especificación posterior.

## 5. Interfaz, API y flujo aprobados

- Página Web: `/attendance-insights`, con filtro opcional de empleado y rango temporal requerido.
- API: `GET /attendance/insights?from=YYYY-MM-DD&to=YYYY-MM-DD&employeeId=`; `employeeId` es opcional.
- Application: consulta `GetAttendanceInsights` que solicita el contexto al puerto HCM y prepara una vista explicativa sin recalcular las reglas de marcaje.
- Respuesta: incidencia informada por el HCM, resultado y referencia de regla, hechos/evidencias de soporte, procedencia, oficina del empleado con país, ciudad y zona horaria IANA y elementos adicionales que la fuente pida revisar; TimeOff se incluye únicamente si la fuente lo relaciona.
- Web consume la API mediante el cliente HTTP tipado existente. No accede a DbContext ni a Infrastructure.
- Entradas inválidas producen error controlado; la indisponibilidad de la fuente se comunica sin stack trace, credenciales ni detalles internos.

La ruta y los parámetros quedan aprobados para esta vertical. `from` y `to` son obligatorios y usan formato ISO `yyyy-MM-dd`; `from` es inclusivo y `to` exclusivo. El filtro usa la fecha local de `IncidentAtUtc` en la zona horaria IANA de la oficina asociada, no la hora de evaluación de la regla ni la notificación. El rango requiere `from < to`. Los valores HCM que no están especificados siguen pendientes y no deben inventarse.

## 6. Criterios de aceptación aprobados

| ID | Criterio verificable | Evidencia futura |
|---|---|---|
| SPEC-002-AC-001 | El caso usa solo respuestas sintéticas HCM reproducibles; no requiere endpoint corporativo ni datos reales. | Prueba de contrato/fuente y revisión de configuración |
| SPEC-002-AC-002 | La respuesta conserva el resultado de regla que suministra el HCM, la referencia de regla y la evidencia/procedencia; Talent Assistant no lo recalcula. | Pruebas de Application con respuesta sintética conocida |
| SPEC-002-AC-003 | La explicación muestra qué datos sustentan la incidencia y qué elementos requieren revisión según la fuente. | Prueba de aceptación de API y Web |
| SPEC-002-AC-004 | TimeOff aparece como contexto solo cuando la fuente lo relaciona como pertinente; no hay operaciones de creación o aprobación. | Pruebas de contrato y aceptación |
| SPEC-002-AC-005 | Application depende de `IHcmAttendanceSource`; sustituir el adaptador sintético no cambia el caso de uso. | Prueba con doble de contrato y revisión de dependencias |
| SPEC-002-AC-006 | La Web usa la API y no tiene acceso directo a datos, DbContext o al proveedor HCM. | Revisión de referencias y prueba HTTP |
| SPEC-002-AC-007 | Los filtros inválidos y fallos de integración generan errores controlados sin secretos ni stack traces. | Pruebas de API |
| SPEC-002-AC-008 | No se construye un módulo HCM ni un motor local de reglas; Tasks queda fuera de esta vertical. | Revisión de alcance y pruebas de aceptación |
| SPEC-002-AC-009 | Cada oficina incluida en la respuesta sintética tiene país, ciudad y zona horaria IANA; la persona puede identificar la oficina asociada al empleado. | Prueba de contrato y aceptación de Web |
| SPEC-002-AC-010 | El límite `from` incluye su fecha local y `to` excluye su fecha local según la zona horaria IANA de cada oficina; `from >= to` se rechaza. | Pruebas de límites temporales con oficinas en distintas zonas |

El usuario aprobó estos criterios como alcance de implementación de SPEC-002. No están verificados y no declaran aceptación del MVP.

## 7. Decisiones pendientes para revisión

1. Resuelto: el usuario aprobó el caso de uso y los criterios SPEC-002-AC-001..010 para implementación de esta vertical.
2. Resuelto para esta vertical: el adaptador sintético devolverá el resultado de regla/evaluación del HCM con referencia y evidencia; no habrá evaluación local de reglas de marcaje.
3. Cada oficina debe exponer país, ciudad y zona horaria IANA; cada empleado referencia la(s) oficina(s) que indique la fuente. La cardinalidad y taxonomía de país siguen pendientes.
4. Resuelto: `from` inclusivo y `to` exclusivo, fechas `yyyy-MM-dd` en la zona local de cada oficina; el adaptador futuro debe normalizar la zona a IANA.
5. Los detalles adicionales de TimeOff se aplazan por decisión del usuario. Las cuatro reglas comunicadas siguen siendo propiedad del HCM; sus parámetros y semánticas no se codificarán en Talent Assistant.
6. Resuelto: la fuente sintética usa un fixture en memoria determinista en Infrastructure; no requiere MySQL, migraciones ni un esquema HCM completo. Un generador se reconsiderará si crece el dataset.
7. La pertinencia de TimeOff y los elementos de revisión vendrán indicados por los datos de la fuente sintética.
8. El proveedor, protocolo y autenticación de una integración real permanecen fuera de esta implementación.

## 8. Verificación y límites

SPEC-002 está aprobada para implementar únicamente el alcance indicado. La implementación del caso de uso, el adaptador sintético en memoria, la API y la página Web está realizada; la aceptación permanece parcial.

- `dotnet build TalentAssistant.sln --configuration Debug --no-restore --verbosity minimal` con SDK `10.0.402`: PASS, 0 warnings y 0 errors; compiló ambos proyectos de pruebas, sin ejecutarlos.
- Smoke HTTP local: `/health` 200; `/attendance/insights` 200 con dos hallazgos sintéticos para el rango consultado; rango inválido 400; Web `/` y `/attendance-insights` 200.
- Smoke interactivo Web: la consulta mostró oficinas (país, ciudad y zona horaria), resultado/evidencia HCM y TimeOff relacionado. La entrada con `from >= to` mostró un error controlado.
- Los proyectos UnitTests e IntegrationTests contienen pruebas para el caso, pero `dotnet test` no se ejecutó. La restricción conocida del runner se conserva; no se modificaron políticas ni se volvió a investigar ese bloqueo.
- El adaptador usa fixtures en memoria. No se añadieron tablas, migraciones, dependencias, conexión HCM real ni configuración de entorno.

### Evidencia por criterio de aceptación

Estados: **observado parcialmente** significa que existe evidencia directa para una parte del criterio, no que esté aceptado; **compilado, no ejecutado** significa que el proyecto de pruebas compila pero no se ejecutó; **sin evidencia suficiente** significa que el registro actual no demuestra el criterio.

| Criterio | Evidencia documentada | Estado de evidencia |
|---|---|---|
| SPEC-002-AC-001 | Se implementó una fuente sintética en memoria y el smoke HTTP devolvió resultados sintéticos. | Observado parcialmente; no demuestra por sí solo todas las condiciones de reproducibilidad/configuración. |
| SPEC-002-AC-002 | El smoke interactivo mostró resultado y evidencia HCM; los proyectos de pruebas compilaron. | Observado parcialmente; pruebas compiladas, no ejecutadas. |
| SPEC-002-AC-003 | El smoke Web mostró resultado/evidencia; el registro no identifica una comprobación completa de todos los elementos de revisión. | Observado parcialmente. |
| SPEC-002-AC-004 | El smoke Web mostró un contexto TimeOff relacionado. No se registró una prueba ejecutada para contexto nulo o para ausencia de operaciones de escritura/aprobación. | Observado parcialmente; pruebas compiladas, no ejecutadas. |
| SPEC-002-AC-005 | El puerto `IHcmAttendanceSource` y su adaptador sintético están implementados; el build compiló el proyecto de pruebas. | Sin evidencia suficiente sobre sustitución del adaptador; pruebas compiladas, no ejecutadas. |
| SPEC-002-AC-006 | Las rutas Web respondieron HTTP 200; no se registró una revisión de referencias que demuestre toda la frontera arquitectónica. | Sin evidencia suficiente para el criterio completo. |
| SPEC-002-AC-007 | Se observó HTTP 400 para rango inválido. No se documentó una comprobación de fallo de la fuente sin filtración de detalles. | Observado parcialmente. |
| SPEC-002-AC-008 | El alcance y la implementación documentados usan fixture sintético y excluyen Tasks y motor local de reglas. | Sin evidencia suficiente de una revisión completa de alcance del repositorio. |
| SPEC-002-AC-009 | El smoke interactivo mostró país, ciudad y zona horaria de la oficina. | Observado parcialmente; no demuestra todas las asociaciones de empleado/oficina. |
| SPEC-002-AC-010 | Se observó rechazo de `from >= to`; la semántica de fechas por zona horaria está implementada y existen pruebas en el proyecto, pero xUnit no se ejecutó. | Observado parcialmente para rango inválido; límites por zona horaria sin evidencia de ejecución. Pruebas compiladas, no ejecutadas. |

La compilación de los proyectos de pruebas no equivale a ejecución ni permite declarar cobertura aprobada. La evidencia HTTP/Web observada no sustituye las pruebas automatizadas ni demuestra los criterios marcados como parciales o sin evidencia suficiente. No se reintentó ni investigó el bloqueo de xUnit.

Los criterios SPEC-002-AC-001..010 no se declaran todos aprobados: las comprobaciones HTTP observadas no sustituyen las pruebas unitarias/automatizadas pendientes. SPEC-001 y sus decisiones técnicas permanecen intactas.

### Actualización de verificación durante SPEC-003 (2026-10-15)

La compilación PASS con SDK `10.0.402` registrada arriba corresponde a la verificación previa de SPEC-002. Durante el trabajo de SPEC-003, el intento de compilar desde el repositorio falló porque `global.json` requiere `10.0.402` y el entorno solo tiene `10.0.401`. A petición del usuario se compiló después desde `C:\repos`, sin modificar archivos de configuración: `dotnet --version` seleccionó `10.0.401` y `dotnet build TalentAssistant\TalentAssistant.sln --configuration Debug --no-restore --verbosity minimal` terminó con 0 advertencias y 0 errores, incluidos ambos proyectos de pruebas. No se instaló SDK ni se cambió `global.json`. Los tests quedaron compilados, no ejecutados; `dotnet test` no se ejecutó. Esta compilación no sustituye una verificación con el SDK fijado `10.0.402`.
