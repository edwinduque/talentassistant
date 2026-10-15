# SPEC-003 — Revisión contextual de incidencias TimeCheck y ausencias TimeOff

- Estado: Aprobada para implementar el alcance sintético (2026-10-15).
- Aprobación: el usuario respondió «Si continua» después de revisar esta propuesta.
- Implementación: cambios aplicados dentro del alcance; verificación parcial/bloqueada (2026-10-15).
- Contratos: se autoriza la ampliación opcional del contrato sintético/API descrita aquí; no se define un contrato de integración HCM real.
- Versión: 0.1
- Relacionada con: SPEC-002 — Explicación de incidencias de marcaje del HCM; FR-003 — contexto TimeOff propiedad del HCM.

## 1. Objetivo

Proponer una vista de solo lectura para que responsables de RR. HH. revisen incidencias de TimeCheck junto con los registros de TimeOff que la fuente HCM declare relacionados. La vista debe preservar el estado, tipo, fechas, duración, resultado de evaluación y evidencia proporcionados por el HCM, sin decidir si una ausencia justifica una incidencia.

El HCM sigue siendo el sistema de registro para empleados, TimeCheck, TimeOff, sus estados, calendarios y políticas. Talent Assistant presenta y organiza información; no gestiona ausencias ni vuelve a evaluar reglas.

## 2. Alcance propuesto

### Incluido

- Consultar incidencias de TimeCheck en un rango de fechas y mostrar los registros de TimeOff relacionados explícitamente por la fuente sintética.
- Mostrar el tipo, estado, fechas y duración únicamente cuando estén disponibles en la fuente.
- Conservar los valores originales y su procedencia; identificar datos ausentes sin completar ni inferirlos.
- Facilitar la revisión de varias incidencias vinculadas al mismo TimeOff solo si la fuente proporciona una referencia cuya identidad y cardinalidad sean confirmadas.
- Mantener los filtros temporales de incidencias ya definidos en SPEC-002: `from` inclusivo, `to` exclusivo y fecha local de la oficina asociada.
- Usar exclusivamente datos sintéticos deterministas y operaciones de consulta.

### Fuera de alcance

- Crear, modificar, cancelar, aprobar o rechazar solicitudes de ausencia.
- Sustituir el flujo de aprobación del HCM o emitir recomendaciones de aprobación.
- Calcular días hábiles, festivos, duración, límites, solapamientos o cobertura.
- Inferir que una ausencia causa, explica o justifica una incidencia de TimeCheck.
- Construir módulos de TimeCheck, TimeOff o Tasks, conectar un HCM real, introducir LLM/agentes o añadir dependencias.
- Cambiar SPEC-001, las decisiones arquitectónicas aprobadas o el runner de pruebas.

## 3. Dominio TimeOff

La definición reutilizable está en `docs/requirements/functional-requirements.md`, sección «Dominio TimeOff propiedad del HCM», y se aplica a esta propuesta. TimeOff representa ausencias solicitadas por empleados y gobernadas por el HCM; tipos y estados son datos de la fuente, no un catálogo ni un flujo de Talent Assistant.

La vista conserva los valores proporcionados. No interpreta estados desconocidos, no presenta una solicitud pendiente, rechazada o cancelada como aprobada y no deduce tipo, estado, duración o unidad cuando faltan. Las reglas de calendario y duración son del HCM y pueden variar por país y tipo.

Una relación de TimeOff se presenta solo si la fuente la declara. No se deriva de coincidencias de fechas o empleado. Una relación no prueba causalidad, cobertura ni justificación. Un `RelatedTimeOff` nulo significa que la fuente no proporcionó contexto relacionado; no prueba que no existan ausencias.

## 4. Comportamiento esperado

1. Para cada incidencia, presentar el resultado de TimeCheck, la referencia de regla, la evidencia/procedencia y los elementos que el HCM pida revisar, conservando el resultado original.
2. Añadir contexto TimeOff solo si la fuente lo declara relacionado con esa incidencia. Una coincidencia de empleado, fecha o intervalo no crea una relación.
3. Presentar los valores HCM disponibles de tipo y estado, y las fechas o duración con su unidad tal como las suministre la fuente. No calcular duración ni convertir fechas a una semántica no definida.
4. No presentar una solicitud pendiente, rechazada o cancelada como ausencia aprobada. Si falta el estado, mostrar que la fuente no lo proporcionó; no inferir aprobación por la existencia de una relación.
5. Si falta tipo, fechas, duración o unidad, mostrar únicamente la información disponible e identificar el dato faltante. No usar valores por defecto.
6. Si existe una referencia estable de TimeOff, proponer agrupar por esa referencia y conservar el detalle individual de cada incidencia. Si no existe o su semántica no está confirmada, presentar los registros individualmente sin inferir identidad.
7. Mantener el filtro de fechas de SPEC-002 sobre la fecha local de la incidencia TimeCheck; las fechas de TimeOff no alteran la inclusión en el período.

## 5. Datos y evidencia necesarios

Para TimeCheck, se reutiliza el contexto aprobado en SPEC-002: identificador de incidencia, empleado y oficina, fecha/hora de incidencia, resultado y referencia de regla HCM, evidencia/procedencia y estado de revisión.

Para TimeOff, la fuente sintética necesitaría exponer, cuando existan en el contrato HCM:

- Una referencia de origen que permita identificar el registro, si la fuente la proporciona.
- Tipo y estado originales.
- Fechas de inicio/fin y sus semánticas, solo si están definidas por la fuente.
- Duración y unidad originales, cuando se proporcionen.
- La relación explícita entre el registro TimeOff y la incidencia TimeCheck, si el HCM la informa.
- Procedencia suficiente para distinguir valores fuente de presentaciones de Talent Assistant.

La implementación existente de SPEC-002 modela `HcmTimeOffContext` como contexto nullable con `Reference`, `StartsOn`, `EndsBefore` y `Summary`. No modela explícitamente tipo, estado, duración, unidad ni una semántica de zona horaria/límite para las fechas; tampoco confirma que `Reference` sea estable y única o que la relación pueda ser múltiple. Este borrador no cambia ese contrato. La disponibilidad, cardinalidad e interpretación de esos datos requieren información del HCM y aprobación antes de proponer cambios de contrato.

## 6. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Escenario/evidencia sintética |
|---|---|---|
| SPEC-003-AC-001 | Cuando la fuente proporcione tipo, estado, referencia, fechas, duración o unidad, la vista conserva y presenta los valores originales disponibles, sin calcularlos ni sustituirlos. | Un TimeOff sintético de vacaciones aprobadas devuelve fechas y duración con unidad explícita; la vista presenta exactamente esos datos. |
| SPEC-003-AC-002 | El estado suministrado se distingue. Una solicitud pendiente, rechazada o cancelada nunca se presenta como ausencia aprobada; estados no reconocidos no se reclasifican. | Fixtures de cumpleaños pendiente, calamidad rechazada/cancelada y vacaciones aprobadas; el estado mostrado corresponde al valor de cada fuente. |
| SPEC-003-AC-003 | Si falta tipo, estado, fecha, duración o unidad, la vista identifica el dato no proporcionado y no inventa un valor o significado. | Un fixture con estado o duración omitidos conserva el resto de los datos y señala la ausencia del campo. |
| SPEC-003-AC-004 | Solo se presenta una relación TimeCheck-TimeOff declarada por la fuente. Coincidencias de empleado o fechas no crean una relación. | Dos registros con fechas coincidentes, uno relacionado explícitamente y otro no, conservan esa diferencia. |
| SPEC-003-AC-005 | La relación se presenta como contexto, no como causa, cobertura ni justificación; mostrar TimeOff no cambia el resultado, la regla ni la evidencia de TimeCheck informados por el HCM. | Una incidencia vinculada a un TimeOff pendiente, rechazado, cancelado o aprobado conserva su evaluación original. |
| SPEC-003-AC-006 | Las incidencias se agrupan por referencia solo si su estabilidad, unicidad y ámbito están definidos por la fuente; cada grupo conserva el detalle individual. Sin esa garantía, los registros se presentan individualmente. | Dos incidencias comparten una referencia estable declarada; el resumen muestra ambas sin fusionar su evaluación. |
| SPEC-003-AC-007 | El rango filtra incidencias por la fecha local de la oficina según SPEC-002; las fechas de TimeOff no cambian qué incidencias pertenecen al rango. | Madrid y Tokio incluyen sus incidencias del `2026-10-15` local; la incidencia madrileña del día local siguiente queda fuera de `to=2026-10-16`. |
| SPEC-003-AC-008 | Los fixtures sintéticos representan distintos tipos y estados de forma determinista, sin establecer un catálogo exhaustivo ni reglas universales de duración o país. | Vacaciones, cumpleaños, calamidad y maternidad pueden aparecer con estados/valores fuente; ningún cálculo local deriva días hábiles o festivos. |
| SPEC-003-AC-009 | La vertical es de solo lectura y no crea, cambia ni aprueba solicitudes ni implementa reglas de TimeOff. | Revisión de la API y dependencias confirma solo consulta; no hay operaciones de escritura ni un motor de políticas. |
| SPEC-003-AC-010 | Entradas inválidas y fallos de fuente mantienen errores controlados, sin stack traces, secretos ni detalles internos. | Rango inválido y fuente sintética indisponible producen las respuestas controladas de la API. |

Los tipos de ausencia sugeridos para los fixtures —vacaciones, cumpleaños, calamidad y maternidad— son ilustrativos, no un catálogo obligatorio ni exhaustivo. Los estados, fechas, duraciones y unidades son datos devueltos por cada fixture, no reglas generales.

## 7. Escenarios sintéticos propuestos

1. **Vacaciones aprobadas:** la fuente declara la relación y devuelve tipo, estado, referencia, fechas y duración con unidad. La vista conserva esos datos, sin concluir que justifican la incidencia ni recalcular días.
2. **Día de cumpleaños pendiente:** la fuente devuelve estado pendiente y las fechas disponibles. La vista lo presenta como solicitud pendiente, nunca como ausencia aprobada.
3. **Calamidad rechazada o cancelada:** se conserva el estado original y no se presenta como ausencia aprobada, aunque la fuente la relacione con un caso de TimeCheck.
4. **Licencia de maternidad aprobada:** fixture de otro tipo, con estado y fechas según la fuente. La duración solo se muestra si la fuente la entrega con unidad; no se calcula.
5. **Coincidencia sin vínculo:** otro TimeOff tiene fechas coincidentes, pero la fuente no lo asocia a la incidencia; Talent Assistant no crea la relación.
6. **Referencia compartida:** si la fuente declara una referencia estable y única en el ámbito acordado, dos incidencias se agrupan sin perder sus detalles individuales; de lo contrario, se muestran por separado.
7. **Contexto incompleto o estado desconocido:** falta tipo, estado o unidad, o llega un estado no conocido por la aplicación; se conserva lo disponible y se identifica la limitación sin reclasificarlo.

Las fechas de estos fixtures deben ser deterministas. No se utilizarán para probar una política de calendario, duración, aprobación o solapamiento que no forme parte del contrato HCM aprobado.

## 8. Cambios técnicos mínimos aprobados

Estos cambios se limitan al contrato sintético de la aplicación y deben preservar el monolito modular, Clean Architecture, el puerto de lectura de Application, CQRS ligero y los fixtures sintéticos deterministas definidos por ADR-0001, ADR-0002 y ADR-0005. No implican un contrato productivo de un HCM real ni modifican esas decisiones arquitectónicas.

- **Application:** añadir al contexto sintético campos opcionales para tipo, estado, duración y unidad, conservando los valores de la fuente. Mantener la consulta de lectura y no incorporar motor de reglas ni inferencia de ausencias.
- **Adaptador sintético:** ampliar fixtures deterministas para vacaciones aprobadas, cumpleaños pendientes, calamidad rechazada, maternidad cancelada y una incidencia sin contexto relacionado. Estos valores son ejemplos de la fuente, no políticas.
- **API:** conservar la ruta GET existente; serializar los campos opcionales añadidos al modelo. No crear operaciones de escritura ni definir una integración HCM real.
- **Web:** presentar tipo, estado, referencia, valores de fechas, duración/unidad y procedencia disponibles; distinguir datos ausentes y solicitudes no aprobadas. No agrupar por referencia mientras no se confirme su estabilidad y unicidad.
- **Pruebas:** se añadieron pruebas deterministas para valores originales, estados, contexto nulo y filtro local. No se ejecutaron; no se cambió el runner ni se ejecutó `dotnet test`.

El contrato base `HcmTimeOffContext` solo contiene referencia, fechas y resumen. La aprobación permite añadir los campos opcionales indicados para los datos sintéticos y su respuesta API. Los valores de un HCM real, su mapeo, la semántica de fechas y las garantías de referencia siguen sin definirse.

## 9. Decisiones pendientes

1. ¿Qué referencia, cardinalidad y ámbito de unicidad proporcionará un HCM real? Hasta confirmarlo, mostrar cada relación por incidencia y no agrupar.
2. ¿Qué tipos y estados exactos expondrá el contrato de un HCM real? El contrato sintético usa strings abiertos y conserva valores desconocidos.
3. ¿Qué semántica, zona horaria y límites tendrán las fechas TimeOff reales? La implementación sintética solo mostrará los valores; no los comparará ni recalculará.
4. ¿Duración se recibirá como valor y unidad separados o como texto? El fixture aprobado muestra valores fuente y no calcula duración.
5. La vista conserva todos los hallazgos y la marca `RequiresReview` existente; cualquier filtro que oculte hallazgos requiere decisión posterior.

Estas preguntas sobre la integración real no bloquean el alcance sintético aprobado. No se asumirá que un HCM real ofrece esos campos o semánticas.

Hasta resolverlas, no se calculará duración, solapamiento, cobertura ni equivalencia de estados, y no se asumirán reglas comunes entre países.

## 10. Verificación previa de SPEC-002 y límites

SPEC-002 permanece implementada con verificación parcial, según `specs/002-attendance-insight/spec.md`, sección 8. El build compiló los proyectos de pruebas, pero no ejecutó xUnit. La tabla por criterio en esa especificación distingue evidencia observada, compilación sin ejecución y criterios sin evidencia suficiente. La evidencia actual debe conservar estas distinciones:

| Evidencia | Estado documentado | Qué no demuestra |
|---|---|---|
| `dotnet build` | PASS, 0 warnings y 0 errors; los proyectos de pruebas compilaron. | No ejecuta ni valida los tests. |
| Smoke HTTP | `/health` y `/attendance/insights` respondieron 200; rango inválido respondió 400. | No cubre todos los criterios ni estados TimeOff. |
| Smoke interactivo Web | Se observó oficina, evidencia HCM y un contexto TimeOff; se observó error controlado para rango inválido. | No demuestra los escenarios pendientes, rechazados, cancelados, datos faltantes ni agrupación. |
| xUnit | No ejecutado por la restricción conocida del runner; no reintentar ni investigar ese bloqueo en esta tarea. | No se puede afirmar PASS ni cobertura ejecutada. |

Para el cierre de SPEC-002, se recomienda una matriz por `SPEC-002-AC-001..010` con estado, evidencia concreta y prueba existente/no ejecutada. Los tests escritos deben identificarse como no ejecutados; build y smoke se registran por separado. No marcar todos los criterios como aceptados basándose solo en la compilación o los smoke checks.

Este borrador no cambia la verificación documentada de SPEC-002, no cambia SPEC-001 y no declara aceptado el MVP.

## 11. Aprobación y límite de implementación

El usuario aprobó esta propuesta con «Si continua» el 2026-10-15. La autorización cubre el contrato sintético opcional, los fixtures y la presentación de solo lectura descritos arriba. No autoriza integrar un HCM real, inventar estados/políticas, agrupar sin referencia estable confirmada, cambiar SPEC-001, modificar políticas/runner, ejecutar xUnit, ni hacer commits o push.

## 12. Verificación de SPEC-003

- El entorno solo tenía SDK `10.0.401`, mientras `global.json` fija `10.0.402` con `"rollForward": "disable"`. El primer intento de build desde el repositorio no llegó a compilar por esa selección.
- El usuario autorizó instalar el SDK fijado. Se instaló `Microsoft.DotNet.SDK.10` versión `10.0.402` vía winget (hash verificado) en `C:\Program Files\dotnet`, junto al `10.0.401`. `global.json` no se modificó.
- `dotnet --version` desde la raíz del repositorio devuelve ahora `10.0.402`.
- `dotnet build TalentAssistant.sln --configuration Debug --no-restore --verbosity minimal` ejecutado desde la raíz con el SDK fijado: PASS, 0 advertencias y 0 errores; compiló Domain, Application, Web, Api, Infrastructure y ambos proyectos de pruebas.
- `dotnet test` y xUnit no se ejecutaron, conforme a la restricción del usuario. Las pruebas nuevas están compiladas, pero su comportamiento no está verificado por ejecución.
- La aceptación de SPEC-003 sigue pendiente de verificación funcional y automatizada: la compilación con el SDK fijado está verificada, no el comportamiento de los criterios de aceptación.
