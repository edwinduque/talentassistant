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
| FR-020 | En el entorno local, la fuente HCM sintética consultará desde MySQL fixtures de respuestas de asistencia cargados de forma reproducible; Application continuará consumiéndolos mediante su contrato, sin crear módulos HCM. | Must |

FR-020 queda aprobado para planificación mediante SPEC-004; su implementación permanece pendiente de aprobación del plan de trabajo.

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
