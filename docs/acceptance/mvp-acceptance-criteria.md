# Criterios de aceptación del MVP

## AC-001 — Ejecución local

Dado un entorno que cumple los prerrequisitos documentados, el desarrollador puede iniciar la aplicación siguiendo las instrucciones del repositorio, sin credenciales corporativas ni servicios productivos.

## AC-002 — Datos sintéticos

El adaptador sintético puede devolver respuestas HCM reproducibles para los casos de uso aprobados. La regeneración sigue un procedimiento documentado; los datos no son reales y no modelan un HCM completo.

## AC-003 — Dashboard

Los resúmenes del asistente se derivan de información suministrada por el HCM. Se distingue qué resultado fue calculado por el HCM y cuál es una presentación o relación hecha por Talent Assistant; las fuentes y definiciones relevantes son trazables.

## AC-004 — TimeCheck

En el caso de uso aprobado, el responsable de RR. HH. puede consultar una incidencia de marcaje reportada por el HCM, ver el resultado de su regla, los datos y la evidencia que lo sustentan, la información que requiere revisión y el país y ciudad de la oficina asociada al empleado. El rango incluye `from` y excluye `to` según la zona horaria de esa oficina. Talent Assistant no recalcula la regla de marcaje.

## AC-005 — TimeOff

La información de TimeOff del HCM se presenta como contexto de un caso de asistencia cuando la fuente la relacione como pertinente. Talent Assistant no crea, modifica ni aprueba solicitudes.

## AC-006 — Task Control

Tasks pertenece al HCM. No se requiere un caso de uso de Task Control para aceptar la primera vertical; cualquier consulta de tareas necesita una especificación posterior y no implica gestionar su ciclo de vida.

## AC-007 — Límites arquitectónicos

El dominio no depende de Infrastructure, API, frontend, esquema HCM ni SDK específicos de IA. Application define el contrato de lectura; Infrastructure implementa la fuente sintética sustituible. Talent Assistant no contiene un segundo motor de reglas HCM.

## AC-008 — Errores de API

Las entradas inválidas y los fallos de la fuente de integración producen respuestas controladas. Las respuestas y logs no exponen stack traces, credenciales ni datos sensibles.

## AC-009 — Integración continua

La canalización ejecuta compilación y pruebas automatizadas. Un fallo bloquea la validación satisfactoria del cambio.

Este criterio permanece vigente para el MVP. Está fuera de SPEC-001 y debe asignarse a una especificación posterior antes de aceptar el MVP; las comprobaciones locales no lo satisfacen.

## AC-010 — Trazabilidad SDD

Cada funcionalidad entregada está vinculada con requisitos y criterios de aceptación. Las pruebas pertinentes están identificadas y sus resultados se documentan.

## AC-011 — Ausencia de dependencias de IA en el MVP base

El MVP puede ejecutar sus funcionalidades operativas sin claves de proveedores LLM, frameworks de agentes, n8n o MCP.

## Condiciones generales

Los criterios deberán verificarse con pruebas automatizadas cuando sea razonable y con pruebas de aceptación documentadas cuando la automatización no aporte suficiente valor.

No se declarará el MVP aceptado si existen criterios obligatorios sin verificar.

## Trazabilidad con requisitos generales

Los identificadores de esta tabla pertenecen al catálogo general y no se reutilizan como IDs de requisitos de SPEC-001.

| Criterio | Requisitos generales relacionados | Alcance respecto a SPEC-001 |
|---|---|---|
| AC-001 | FR-010, NFR-004, NFR-008 | La fundación documenta su flujo local; no implica que ya se haya implementado. |
| AC-002 | FR-006, NFR-005, NFR-008 | La estrategia de datos sintéticos se aplica cuando existan entidades; SPEC-001 permite aplazar la carga si no hay modelo de negocio. |
| AC-003 | FR-001, FR-005, FR-008, FR-011, NFR-003 | Resúmenes y cálculos del asistente requieren una especificación funcional sobre datos y resultados del HCM. |
| AC-004 | FR-002, FR-005, FR-006, FR-007, FR-008, FR-019, NFR-010 | SPEC-002 aprobada define la explicación de incidencias; sus criterios aún no están verificados y las reglas las evalúa el HCM. |
| AC-005 | FR-003, FR-006, FR-007, FR-019, NFR-010 | TimeOff es contexto suministrado por el HCM; no es un flujo de gestión de Talent Assistant. |
| AC-006 | FR-004, FR-006, FR-007, NFR-010 | Tasks sigue siendo propiedad del HCM; una consulta de tareas se reserva para una especificación posterior. |
| AC-007 | FR-018, NFR-001, NFR-002 | Los límites de Clean Architecture y el puerto/adaptador HCM se verifican en cada especificación funcional. |
| AC-008 | FR-009, NFR-004, NFR-006, NFR-007 | Los errores controlados incluyen fallos de la fuente HCM, sin exponer datos internos. |
| AC-009 | NFR-009 | CI queda aplazada a una especificación posterior y sigue sin cumplir. |
| AC-010 | FR-011, NFR-015 | SPEC-001 incorpora IDs únicos y una matriz de criterio a requisito y verificación. |
| AC-011 | FR-012–FR-017, NFR-013, NFR-014, NFR-016 | Las capacidades de IA permanecen fuera del alcance de la fundación. |

Los AC-003 a AC-006 no se consideran satisfechos por la estructura arquitectónica creada en SPEC-001. Cada caso de asistencia necesita requisitos, pruebas y verificación propios; Tasks no forma parte de la primera vertical. NFR-011 no tiene todavía un umbral medible y deberá concretarse cuando se definan dataset y entorno. NFR-012 se verifica en cada especificación de interfaz; estos criterios no inventan un umbral cuantitativo de contraste.
