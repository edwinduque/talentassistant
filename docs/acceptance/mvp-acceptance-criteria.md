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

## AC-012 — Stack local contenedorizado (propuesto en SPEC-004)

Desde un checkout limpio y con Docker Desktop disponible, el desarrollador puede construir y arrancar API, Web y MySQL usando sus Dockerfiles y Docker Compose; los servicios quedan saludables y la interfaz Web es accesible localmente.

## AC-013 — Semilla sintética reproducible (propuesto en SPEC-004)

La base local contiene una semilla determinista de fixtures sintéticos que consume la fuente HCM de la API. Reaplicar la inicialización no duplica registros ni altera las reglas/resultados HCM; no se utilizan datos reales.

## AC-014 — Guía de ejecución local contenedorizada (propuesto en SPEC-004)

La documentación explica prerrequisitos, configuración local de secretos, construcción, inicio, URLs, estado de salud, parada y conservación/reinicio de la base, incluyendo advertencias para operaciones que eliminan el volumen.

## AC-015 — Límites del entorno contenedorizado local (propuesto en SPEC-004)

Los puertos publicados se limitan a loopback; credenciales y valores locales no se incorporan a imágenes ni al repositorio. La propuesta no habilita exposición en redes compartidas ni despliegue productivo.

AC-012..AC-015 fueron aprobados como alcance de SPEC-004 el 2026-10-15. Siguen pendientes de implementación y verificación; no se consideran satisfechos.

## AC-016 — Autenticación de sesión (propuesto en SPEC-005)

Dado el stack local arrancado con el usuario sembrado, cuando el desarrollador abre la interfaz sin sesión entonces es redirigido a `/login`; con credenciales correctas accede a las páginas protegidas, ve los insights como antes y puede cerrar la sesión. Sin sesión válida, la API responde HTTP 401 controlado en `/attendance/insights`, mientras que `/health` y `/health/database` siguen públicas.

## AC-017 — Seguridad de credenciales locales (propuesto en SPEC-005)

Dado el flujo de login local, cuando una autenticación falla entonces la API devuelve una única respuesta 401 genérica, idéntica para usuario inexistente y contraseña errónea, sin enumerar usuarios ni exponer detalles internos, y tras intentos repetidos la cuenta queda bloqueada temporalmente. Las contraseñas se almacenan solo como hash; la clave de firma y las credenciales de siembra provienen exclusivamente de configuración externa; ni la API ni la interfaz registran secretos ni tokens.

## AC-018 — Compatibilidad con lo existente (propuesto en SPEC-005)

Dado el perfil host y el perfil Compose, cuando se aplica la autenticación entonces ambos siguen operativos, la suite completa permanece verde, el dataset de asistencia y su semilla no cambian, la base de pruebas separada no recibe usuarios de demostración y no se introduce ningún servicio externo ni dato real.

AC-016..AC-018 fueron propuestos por SPEC-005; T1/T2 aportan evidencia y T3 completa la documentación.

## AC-019 — Selector de idioma anónimo (propuesto en SPEC-006)

La persona sin sesión puede cambiar el idioma del login entre español e inglés; la elección se aplica de inmediato y persiste tras recargar.

## AC-020 — Perfil de usuario (propuesto en SPEC-006)

El usuario autenticado gestiona su perfil: el nombre visible aparece en el layout, el idioma elegido localiza home, asistencia y textos de navegación/error, y el cambio de contraseña exige la actual y respeta la política de Identity.

## AC-021 — Localización sin cambiar datos ni contratos (propuesto en SPEC-006)

La localización no altera los identificadores ni el contrato: el dataset sintético y los endpoints de asistencia conservan ids, fechas, zonas horarias y campos idénticos en cualquier idioma, mientras que los textos de presentación se sirven en el idioma del usuario, la migración es aditiva, la suite permanece verde y no se introducen servicios externos ni datos reales.

AC-019..AC-021 fueron propuestos por SPEC-006; T1/T2 aportan evidencia de implementación y T3 completa la documentación.

Nota: SPEC-007 enmienda AC-021; la igualdad en cualquier idioma se mantiene para identificadores y contrato, y cambia para los textos de presentación, que pasan a servirse por idioma.

## AC-023 — Contenido por idioma del usuario (propuesto en SPEC-007)

Con el usuario en español, la API devuelve reglas, evidencias, tipos y estados de TimeOff en español; en inglés, la variante inglesa; los identificadores y fechas son idénticos entre ambas respuestas. El cambio de idioma en el perfil se refleja en la siguiente consulta, sin volver a iniciar sesión.

## AC-024 — Semilla bilingüe idempotente (propuesto en SPEC-007)

La semilla es idempotente en la versión 2, un payload de versión desconocida sigue fallando de forma controlada, la suite permanece verde y `talentassistant_tests` no recibe la semilla.

## AC-025 — Presencia por fecha (propuesto en SPEC-008)

La consulta de presencia para una fecha devuelve cada empleado con el estado informado por la fuente; «trabajando», «no ha llegado» y «de vacaciones» responden las preguntas 1–3 con los fixtures. La presencia de hoy se ancla a la fecha local más dos días; para cualquier otra fecha la respuesta es vacía, nunca inventada.

## AC-026 — TimeOff por empleado (propuesto en SPEC-008)

La búsqueda por nombre localiza a Juan Pérez y Pedro Rojas **y es insensible a acentos («Juan Perez» sin acento localiza a Juan Pérez); una búsqueda corta compartida («Juan») devuelve a los dos Juans sin elegir en silencio**; sus respuestas de TimeOff informan días y fecha de regreso tal como los declara la fuente, sin cálculos de Talent Assistant. El filtro por identificador o nombre devuelve la colección completa sin filtro y, ante varios nombres coincidentes, los elementos de todos, sin selección silenciosa. *(Enmienda SPEC-008, 2026-10-18.)*

## AC-027 — Tareas por empleado (propuesto en SPEC-008)

Las tareas de Mauricio Cardenas se consultan por nombre y la fuente determina qué significa «pendiente»; FR-004 queda implementado. Los estados de tarea se presentan tal como los declara la fuente.

## AC-028 — Banco de preguntas y verificación (propuesto en SPEC-008)

El banco de preguntas automatizado cubre las seis preguntas literales, la suite completa permanece verde, el home muestra «Presencia de hoy», la página de asistencia ya no pide id manual y todo el contenido nuevo existe en español e inglés. El banco no depende de IA ni de red externa y sirve de referencia para la evaluación futura del asistente.

## AC-029 — Banco de preguntas del chat (propuesto en SPEC-009)

Las seis preguntas de SPEC-008, formuladas en el chat, producen respuestas que contienen los valores esperados de los fixtures y citan la herramienta usada.

## AC-030 — Catálogo fijo, sesión y sin escrituras (propuesto en SPEC-009)

El modelo solo invoca el catálogo fijo, las invocaciones usan la sesión e idioma del usuario, no hay escrituras y una pregunta sin datos recibe una respuesta de «sin información» del propio asistente.

## AC-031 — Degradación y verificación (propuesto en SPEC-009)

Sin clave o sin red la aplicación degrada con mensajes localizados controlados; secretos solo en `.env`; límites de tiempo/rondas activos; suite completa en verde y recorrido de navegador del chat en ES y EN.

## AC-032 — Desambigüación de nombres en el chat (propuesto en SPEC-009)

Una pregunta por una persona con nombre compartido («Juan») recibe una respuesta del asistente que lista los candidatos y pregunta cuál antes de responder; tras la elección responde con el dato del fixture, y un nombre sin acento («Juan Perez») resuelve al empleado correcto. *(Enmienda 2026-10-18.)*

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
| AC-012 | NFR-017, FR-010 | Requiere la aprobación de SPEC-004 y de la propuesta ADR-0006; afecta el supuesto actual de ejecución local en el host. |
| AC-013 | FR-006, FR-020, NFR-005, NFR-017 | Solo persistirá el fixture mínimo necesario para representar respuestas sintéticas del HCM; no crea módulos HCM. |
| AC-014 | FR-010, NFR-008, NFR-017 | La guía actual se actualiza cuando la implementación Docker sea aprobada y verificada. |
| AC-015 | NFR-004, NFR-005, NFR-017 | Desarrollo local solamente; no sustituye autenticación ni despliegue productivo. |
| AC-016 | FR-021, FR-022, NFR-018 | Pertenece a SPEC-005: login y cierre de sesión en Web, token exigido en endpoints de negocio y sondas de salud públicas. |
| AC-017 | NFR-004, NFR-018, NFR-006 | Respuestas de login genéricas, hash de contraseñas y secretos solo en configuración externa, según SPEC-005 y ADR-0007. |
| AC-018 | FR-006, FR-007, NFR-005, NFR-017 | La autenticación no cambia el dataset sintético, la fuente HCM ni añade servicios externos ni datos reales. |
| AC-019 | FR-024, NFR-019 | Selector anónimo en el login que fija la cookie de cultura; SPEC-006 y ADR-0008. |
| AC-020 | FR-023, NFR-018 | Perfil con nombre visible, idioma preferido y cambio de contraseña exigiendo la actual; SPEC-006 y ADR-0008. |
| AC-021 | FR-024, NFR-005, NFR-017, NFR-019 | La localización es solo de interfaz: datos y contratos intactos, migración aditiva y sin servicios externos ni datos reales. Enmienda SPEC-007: los textos de presentación se sirven por idioma. |
| AC-023 | FR-025, NFR-020 | Contenido sintético por idioma del usuario autenticado con identificadores idénticos; SPEC-007. |
| AC-024 | FR-025, NFR-006, NFR-017 | Semilla bilingüe idempotente en versión 2 y fallo controlado ante versión desconocida; SPEC-007. |
| AC-025 | FR-026, FR-006 | Presencia por fecha con estados informados por la fuente; SPEC-008. |
| AC-026 | FR-027, FR-008 | TimeOff por empleado con valores literales de la fuente, sin cálculos; SPEC-008. Enmienda SPEC-008 (2026-10-18): búsqueda insensible a acentos y doble «Juan» sin selección silenciosa. |
| AC-027 | FR-004, FR-027 | Tareas por empleado; FR-004 queda activo; SPEC-008. |
| AC-028 | FR-011, NFR-015 | Banco de las seis preguntas literales como pruebas automatizadas; SPEC-008. |
| AC-029 | FR-029 | Banco de las seis preguntas en el chat con valores del fixture y herramienta citada; SPEC-009. |
| AC-030 | FR-029, FR-030 | Catálogo fijo con sesión e idioma del usuario y sin escrituras; SPEC-009. |
| AC-031 | FR-030, NFR-021 | Degradación controlada, secretos solo en `.env`, límites activos y E2E del chat en ES y EN; SPEC-009. |
| AC-032 | FR-027, FR-029 | Desambigüación de nombres en el chat; SPEC-009. Enmienda 2026-10-18. |

Los AC-003 a AC-006 no se consideran satisfechos por la estructura arquitectónica creada en SPEC-001. Cada caso de asistencia necesita requisitos, pruebas y verificación propios; Tasks no forma parte de la primera vertical. NFR-011 no tiene todavía un umbral medible y deberá concretarse cuando se definan dataset y entorno. NFR-012 se verifica en cada especificación de interfaz; estos criterios no inventan un umbral cuantitativo de contraste.
