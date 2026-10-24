# Requisitos funcionales

## Convenciones

Prioridad: Must, Should o Could.

Estado inicial: Propuesto. La aprobación del alcance convierte los requisitos seleccionados en la línea base del MVP.

| ID | Requisito | Prioridad |
|---|---|---|
| FR-001 | El sistema presentará resúmenes o indicadores derivados de información suministrada por el HCM y mostrará su procedencia cuando corresponda. | Must |
| FR-002 | El usuario podrá consultar incidencias de marcaje y los datos que el HCM exponga para explicarlas, incluyendo la oficina del empleado y el país y ciudad de cada oficina. | Must |
| FR-003 | El asistente podrá presentar como contexto de un caso de uso la información de TimeOff que el HCM exponga y relacione; conservará los valores y estados disponibles y no gestionará solicitudes, aprobaciones ni reglas de ausencias. | Must |
| FR-004 | El asistente podrá consultar información de Tasks del HCM en casos de uso posteriores aprobados; no gestionará su ciclo de vida. | Must |
| FR-005 | Los casos de uso permitirán aplicar filtros definidos por su especificación y contrato; en la vertical de marcajes, `from` es inclusivo y `to` exclusivo en la zona horaria de la oficina. | Must |
| FR-006 | El sistema dispondrá de respuestas sintéticas reproducibles que representen los contratos del HCM, no módulos completos del HCM. | Must |
| FR-007 | La interfaz obtendrá resultados mediante contratos de API, sin acceso directo a la base de datos ni al adaptador de integración. | Must |
| FR-008 | Las reglas de negocio propias del HCM se aplicarán en el HCM o se recibirán como resultados de la fuente; Talent Assistant no duplicará su motor. | Must |
| FR-009 | Los errores de API o de la fuente de integración se comunicarán mediante respuestas coherentes, sin exponer detalles internos. | Must |
| FR-010 | El sistema contará con documentación para ejecutar la aplicación y sus pruebas localmente. | Must |
| FR-011 | Los requisitos aprobados estarán vinculados con criterios de aceptación verificables. | Must |
| FR-012 | La aplicación podrá incorporar un asistente conversacional en una fase posterior. | Should |
| FR-013 | El asistente podrá consultar información de negocio mediante herramientas autorizadas por el backend. | Should |
| FR-014 | El sistema podrá recuperar conocimiento documental mediante RAG y mostrar las fuentes utilizadas. | Should |
| FR-015 | Las futuras operaciones de escritura asistidas por IA requerirán autorización y controles explícitos. | Must para fases con escritura |
| FR-016 | Las capacidades de IA dispondrán de un conjunto de evaluación reproducible. | Must para fases con IA |
| FR-017 | Se podrá evaluar una arquitectura con Supervisor y agentes especializados frente a una alternativa de agente único. | Could |
| FR-018 | La fuente HCM sintética podrá sustituirse por un adaptador real mediante un contrato estable, sin reescribir los casos de uso de Application. | Must |
| FR-019 | Las explicaciones mostrarán la evidencia disponible, la procedencia del resultado y la información que la fuente marque para revisión. | Must |
| FR-020 | En el entorno local, la fuente HCM sintética consultará desde PostgreSQL (originalmente aprobado sobre MySQL en su momento; ADR-0013/SPEC-013) fixtures de respuestas de asistencia cargados de forma reproducible; Application continuará consumiéndolos mediante su contrato, sin crear módulos HCM. | Must |
| FR-021 | El usuario podrá autenticarse en la aplicación local; la API exigirá autenticación en los endpoints de negocio y la sesión podrá cerrarse desde la interfaz. | Must |
| FR-022 | El entorno local dispondrá de usuarios sintéticos creados de forma idempotente desde configuración externa, sin credenciales por defecto ni en el repositorio. | Must |
| FR-023 | El usuario podrá gestionar su perfil local: nombre visible, idioma preferido y contraseña, con la contraseña actual exigida para el cambio. | Must |
| FR-024 | La interfaz se mostrará por completo en el idioma seleccionado (español o inglés), incluido el login sin sesión; los problem details de la API no se localizan. El localizado de los datos del HCM pasó a FR-025 (SPEC-007). | Must |
| FR-025 | El contenido sintético del HCM se servirá en el idioma preferido del usuario autenticado (español o inglés), manteniendo identificadores, fechas y valores compartidos. | Must |
| FR-026 | El usuario podrá consultar el directorio de empleados por nombre u oficina y el estado de presencia de cada empleado para una fecha; esos estados los informará el HCM y Talent Assistant no los derivará. | Must |
| FR-027 | El usuario podrá consultar las solicitudes de TimeOff y las tareas declaradas por el HCM para un empleado, por identificador o por nombre. | Must |
| FR-029 | El usuario podrá conversar con un asistente en lenguaje natural que responderá con datos del backend mediante un catálogo fijo de herramientas ejecutadas con la sesión e idioma del usuario, sin escrituras. | Must |
| FR-030 | El asistente conversacional aplicará límites de tiempo de espera, de rondas y de longitud de respuesta; los errores del proveedor se traducirán a mensajes localizados y la aplicación funcionará sin IA. | Must |
| FR-031 | El asistente conversacional operará únicamente en el ámbito de RR. HH./HCM y solo con los datos que devuelvan sus herramientas; a las peticiones ajenas —programación, conocimiento general, traducciones, recetas o escritura creativa— responderá con un rechazo localizado que recuerda su función y redirige al ámbito del equipo, nunca con código ni conocimiento general; si las herramientas no devuelven el dato, declarará la información no disponible en lugar de inventarla. | Must |
| FR-032 | La aplicación consultará el HCM real desde su PostgreSQL de solo lectura mediante los puertos existentes, con un interruptor único de fuente (`Hcm__Source=Sintética|Postgres`) que gobierna a la vez todos los puertos de datos, sin SQL dinámico y con el cambio de motor de base de datos contenido en un adaptador de Infrastructure. | Must |
| FR-033 | En modo Postgres, si la base del HCM no está disponible, los endpoints de datos devolverán un error localizado de fuente no disponible; el sistema nunca volverá a datos sintéticos de forma silenciosa. | Must |
| FR-034 | La interfaz Web ofrecerá en `/` una landing pública que explique Talent Assistant y permita navegar a sus secciones informativas y al login; las páginas funcionales seguirán protegidas por autenticación. | Must |
| FR-035 | La interfaz Web retirará la página `/attendance-insights` y su enlace de navegación; la información de asistencia seguirá disponible mediante el asistente (herramienta `attendance_insights`) y el endpoint `GET /attendance/insights`, que conservan su contrato y autorización. | Must |

FR-020 fue aprobado mediante SPEC-004 y está implementado y verificado (commits en `main`); el perfil local Compose sirve los fixtures desde PostgreSQL (motor sustituido por ADR-0013/SPEC-013).

FR-021 y FR-022 quedan aprobados para planificación mediante SPEC-005 y ADR-0007; su implementación está verificada en T1/T2 con la documentación pendiente en T3.

FR-023 y FR-024 fueron aprobados mediante SPEC-006 y ADR-0008 y están implementados y verificados en T1/T2.

FR-025 fue aprobado mediante SPEC-007 y ADR-0009 y está implementado y verificado en T1.

FR-026 y FR-027 fueron aprobados mediante SPEC-008 y ADR-0010 y están implementados y verificados en T1/T2; FR-004 queda activo mediante esta especificación.

FR-029 y FR-030 fueron aprobados mediante SPEC-009 y ADR-0011 y están implementados y verificados en T1/T2 de la rama `feat/conversational-assistant`.

FR-031 fue aprobado mediante SPEC-010 (sin ADR nuevo; refuerza ADR-0003 y ADR-0011) y está implementado y verificado en T1/T2 de la rama `feat/response-guardrails`.

FR-032 y FR-033 fueron aprobados mediante SPEC-011 y ADR-0012 y están implementados y verificados en T1/T2 de la rama `feat/hcm-postgres-source` (suite completa en verde con ambas bases de prueba activas); la validación contra la base real del HCM queda pendiente de credenciales locales y de la decisión de egreso de SPEC-011 §10.

FR-034 fue aprobado mediante SPEC-017; su landing pública usa información del proyecto y datos sintéticos, conserva el acceso al login en `/login` y mantiene autenticado el resumen operativo en `/overview`.

FR-035 fue aprobado mediante SPEC-019; el retiro se limita a la interfaz Web (página, navegación y cliente HTTP específico), sin tocar el endpoint, el caso de uso, las fuentes ni la herramienta del asistente.

## Reglas de negocio iniciales

Las siguientes reglas funcionales siguen propuestas y deben concretarse con ejemplos y datos de prueba antes de implementar:

- Talent Assistant debe conservar el resultado y la evidencia que devuelve el HCM, sin recalcular sus reglas de marcaje.
- Los filtros y límites de fecha deben definirse por caso de uso y contrato; sus semánticas aún están pendientes.
- Los indicadores propios de Talent Assistant deben distinguirse de los resultados calculados por el HCM.
- Los datos sintéticos deben representar respuestas del HCM y poder regenerarse sin servicios externos.

### Reglas de TimeCheck propiedad del HCM

La integración debe permitir consumir la evaluación del HCM y la evidencia necesaria para explicarla. Se registran estos conceptos comunicados para revisión, sin fijar todavía su semántica definitiva:

- Margen de gracia de 0 a 30 minutos por jornada; valor efectivo, límites, excepciones y zona horaria pendientes.
- Ventana de almuerzo; horario y efecto sobre una incidencia pendientes.
- Aprobación de marcajes manuales; estados, actores y efecto del resultado pendientes.
- Notificación de errores a las 09:00 del siguiente día hábil; zona horaria, calendario, disparadores y destinatario pendientes.

El HCM aplica estas reglas o expone sus resultados. No se implementará un segundo motor de reglas en Talent Assistant. No se fijan reglas adicionales de jornada, calendarios, festivos ni ausencias.

### Dominio TimeOff propiedad del HCM

TimeOff representa ausencias laborales solicitadas por los empleados y sujetas al flujo de aprobación del HCM. Puede incluir vacaciones, cumpleaños, calamidad, maternidad y otros tipos configurados por la empresa; estos ejemplos no forman un catálogo exhaustivo. Los tipos, sus reglas y los estados disponibles pertenecen al HCM.

Talent Assistant conservará y presentará el tipo, estado, referencia, fechas, duración y unidad únicamente cuando la fuente los proporcione. No inventará estados ni semánticas ausentes del contrato. Una solicitud pendiente, rechazada o cancelada no se presentará como una ausencia aprobada; un estado desconocido se conservará sin reclasificarlo.

La duración, las unidades de cómputo, los días hábiles, los festivos y demás políticas pueden variar por país y tipo de ausencia. Son responsabilidad del HCM: Talent Assistant no calculará ni generalizará estas reglas. Una relación TimeCheck-TimeOff solo se presentará cuando la fuente la declare; la coincidencia de fechas no crea una relación ni demuestra que la ausencia justifique la incidencia. Un contexto relacionado nulo significa que la fuente no lo proporcionó, no que no existan ausencias.

## Trazabilidad

Cada requisito deberá incluir, como mínimo:

- Identificador estable.
- Descripción verificable.
- Prioridad.
- Estado de aprobación.
- Criterios de aceptación.
- Especificación o tarea asociada.
- Pruebas que demuestran su cumplimiento.
