# SPEC-020 — Consulta flexible y controlada de datos de RR. HH.

- **Estado:** Aprobada para implementación (2026-10-25); ADR-0014 aceptado; implementación en curso.
- **Fecha:** 2026-10-25.
- **Requisitos relacionados:** FR-041; amplía FR-036 y FR-040.
- **Criterios relacionados:** AC-055..AC-059.
- **Decisiones arquitectónicas:** ADR-0014 aceptado; actualiza las restricciones de catálogo de ADR-0011 y SQL de ADR-0012.
- **Versión:** 0.4.

## 1. Objetivo

Permitir que responsables de RR. HH. formulen preguntas variadas en lenguaje natural sobre los datos disponibles en la fuente HCM, sin necesitar una herramienta, plantilla o consulta específica por cada forma de preguntar. Se podrán consultar dinámicamente los campos no sensibles expuestos por la fuente, sin aprobar cada campo de forma individual. El asistente debe mantener los límites de ámbito, autorización, solo lectura, exclusión de datos sensibles y respuesta basada en evidencia.

La consulta dinámica no permite modificar datos ni entregar SQL libre o código ejecutable al modelo. El backend valida y ejecuta consultas de solo lectura. Si una pregunta no puede resolverse con los datos disponibles, el asistente debe reconocer la limitación y no inventar una respuesta.

## 2. Problema y contexto actual

El usuario reporta que «¿Quién es la próxima persona que cumple años?» puede responderse mediante la operación existente `upcoming_birthday`, mientras que «cuantos empleados tiene la compañia» termina con «No se pudo obtener una respuesta válida.». También pide que una pregunta futura como «cuantos mauricios hay en la compañia» no requiera crear otra plantilla.

### Solicitud literal

```text
Ayudame con un nuevo requerimiento. La idea inicial del proyecto es que una persona de recursos humanos tenga una herramienta tipo chatbot para consultar toda la información de empleados marcajes, ausencias, tareas, informacion de empleados. Inicialmente el proyecto se fue llevando a unas consultas predeterminadas pero eso no es lo que yo quiero. Quiero que sea mas libre de responder, pero terminamos teniend unas plantillas predeterminadas y si se pregunta algo diferente no sabe responder

Te doy ejemplos:
si pregunto cual es la  proxima persona que cumple años: Probablemente lo sepa porque tiene una plantilla upcoming_birthday

pero si le pregunto cuantos empleados tiene la compañia: No se pudo obtener una respuesta válida.

Ojo, no quiero que ahora cree una plantilla para resolver eso. quiero que el chatbot sea mas libre en obtener y consultar esos datos.

si mañana pregunto cuantos mauricios hay en la compañia lo pueda resolver o No se pudo obtener una respuesta válida tambien lo sepa

Por eso quiero cambiar la estrategia, si es el caso que pueda consultar de bd de una manera diferente o tener una herramienta que haga las consultas con las restricciones pues necesarias y que no se dañen los guarda rieles que ya se han creado como por ejemplo que pregunten: creame una funcion en python para retornar el numero mayor.

Hagamos esto como una especificacion nueva
```

### Enmienda acordada por el usuario (2026-10-25)

> «No modificacion de datos, consultas dinamicas pero que no contengan datos sensibles como el salario o numero de cuenta. lo demas si»

Esta enmienda confirma las consultas dinámicas de solo lectura y que todos los campos no sensibles disponibles en la fuente HCM pueden consultarse sin una aprobación individual por campo. El salario y el número de cuenta quedan excluidos tanto de los resultados como de cualquier filtro, agrupación, ordenación o agregación. Se conservan las exclusiones explícitas de guardarraíles previos —por ejemplo, el año de nacimiento crudo de SPEC-008— y los datos que la fuente no exponga no se inventan.

La implementación actual expone seis herramientas de catálogo cerrado en `ChatOrchestrator.ToolCatalog`; `IHcmWorkforceSource` ofrece operaciones tipadas para empleados, cumpleaños, presencia, TimeOff y tareas. SPEC-009 §14 ya contempla derivar conteos, agrupaciones, ordenaciones y extremos sobre los registros recibidos, pero no define una consulta general que permita expresar y ejecutar combinaciones nuevas de filtros y agregaciones desde el backend. La causa exacta de la respuesta reportada no se considera reproducida ni demostrada por esta especificación.

## 3. Alcance aprobado

### Incluido

- Consultas flexibles en lenguaje natural sobre los datos que expongan los contratos/vistas HCM para el directorio de empleados, marcajes/insights, presencia, ausencias (`TimeOff`) y tareas.
- Consultas dinámicas sobre cualquier campo no sensible disponible en esas fuentes, sin una aprobación individual por campo, y combinaciones de filtros, selección de campos y operaciones —conteo, agrupación, ordenación, ranking y extremos— sin una receta por pregunta.
- Una capacidad genérica del backend que traduzca la intención a una consulta estructurada, validada y de solo lectura. El backend, no el LLM, controla qué se ejecuta.
- Respuestas con resultados provenientes de la fuente HCM. Las agregaciones deben devolver solo los valores necesarios para responder; no se deben enviar al modelo filas personales cuando basta un agregado.
- Tratamiento explícito de preguntas ambiguas, campos no disponibles, datos inexistentes y consultas que no puedan representarse de forma segura.
- Conservación de los guardarraíles aprobados en SPEC-009 y SPEC-010, y de la identidad, idioma y autorización de la sesión vigente.

### Fuera de alcance

- Crear una plantilla, endpoint o instrucción específica por cada pregunta o redacción.
- Permitir que el LLM se conecte directamente a la base de datos, a la red o a herramientas fuera del backend.
- Aceptar o ejecutar SQL libre, scripts, Python, expresiones ejecutables o código generado por el modelo.
- Consultar o devolver salario de empleados o números de cuenta; los guardarraíles explícitos preexistentes, como no exponer el año de nacimiento crudo, también se conservan.
- Inventar campos o datos que no existan en las fuentes HCM. Por ejemplo, el dato de género no está disponible en el contrato HCM actual.
- Escribir, actualizar, borrar, aprobar o gestionar información del HCM.
- Añadir un proveedor LLM o cambiar la decisión de egreso de datos de ADR-0011.

## 4. Ejemplos de comportamiento esperado

| Pregunta o solicitud | Resultado esperado |
|---|---|
| «si pregunto cual es la  proxima persona que cumple años: Probablemente lo sepa porque tiene una plantilla upcoming_birthday» | Mantener la respuesta correcta existente, obtenida de la fuente HCM. El comportamiento no debe degradarse al cambiar la estrategia. |
| «pero si le pregunto cuantos empleados tiene la compañia: No se pudo obtener una respuesta válida.» | La consulta debe poder contar los empleados dentro del alcance autorizado y responder con el resultado de la fuente; no requiere una plantilla para esta frase. El texto citado describe el resultado reportado por el usuario, no un resultado reproducido en esta tarea. |
| «si mañana pregunto cuantos mauricios hay en la compañia lo pueda resolver o No se pudo obtener una respuesta válida tambien lo sepa» | Buscar y contar todas las coincidencias según las reglas de búsqueda aprobadas; no elegir un empleado en silencio ni inventar un total. Si la consulta no se puede ejecutar con seguridad, indicar esa limitación en vez de presentar un resultado ficticio. |
| «creame una funcion en python para retornar el numero mayor.» | Rechazar brevemente y redirigir a una pregunta de RR. HH./HCM; no ejecutar herramientas ni generar el código solicitado. |

## 5. Requisitos funcionales y límites

### FR-041 — Consulta flexible de datos del HCM

El asistente podrá traducir preguntas de RR. HH./HCM a consultas dinámicas y estructuradas sobre cualquier campo no sensible disponible en la fuente HCM, combinando selección, filtros y operaciones sin requerir una herramienta o plantilla por pregunta ni una aprobación individual por campo. El salario, los números de cuenta y los atributos protegidos explícitamente por guardarraíles previos no podrán aparecer ni usarse en ninguna parte de la consulta. La API validará y ejecutará cada consulta con la sesión del usuario contra la fuente configurada, en modo de solo lectura. El modelo no recibirá SQL libre ni acceso directo a la fuente. Cuando un dato no esté disponible o una consulta no sea segura, el asistente declarará la limitación sin inventar valores.

### Requisitos heredados que permanecen

- **FR-029 / SPEC-009:** el modelo solicita herramientas del backend; el backend consulta usando la sesión y el idioma del usuario. No hay escrituras.
- **FR-031 / SPEC-010:** solo se responde dentro del ámbito RR. HH./HCM; para hechos se usan datos de las herramientas; las peticiones ajenas se rechazan; no se inventan resultados.
- **FR-036 / SPEC-008..010:** la enmienda amplía la selección de campos no sensibles a todos los disponibles en las fuentes HCM, sin aprobación por campo. Sueldo, datos bancarios y exclusiones explícitas previas siguen restringidos.
- **FR-040 / SPEC-009 §14:** se conserva el objetivo de preguntas de seguimiento y agregaciones; el backend ejecuta consultas dinámicas y los agregados en la fuente.
- **FR-032..FR-033 / SPEC-011:** la fuente HCM real sigue siendo de solo lectura, sin mezcla silenciosa de fuentes y con degradación honesta si no está disponible.

### Límites de seguridad y calidad

1. La API permite consultas dinámicas sobre los campos no sensibles expuestos por las fuentes HCM y valida conjuntos de datos, campos y operaciones. El salario, los números de cuenta y las exclusiones explícitas previas se bloquean antes de planificar o ejecutar la consulta. El texto del usuario o del modelo nunca se concatena como SQL ejecutable.
2. La consulta conserva la autorización del usuario autenticado y no puede ampliar su alcance mediante instrucciones conversacionales, historial o inyección de prompt.
3. El plan y los resultados no pueden seleccionar, filtrar, agrupar, ordenar, agregar ni inferir salario, número de cuenta o datos que guardarraíles previos excluyen.
4. El backend realiza consultas de solo lectura; ninguna solicitud ni plan del modelo puede modificar datos.
5. La fuente HCM determina los hechos y reglas propios de su negocio. Talent Assistant no deduce estados de presencia, reglas de ausencias u otros valores que pertenecen al HCM.
6. Las operaciones tienen límites de tiempo y volumen configurables y verificables. Una consulta inválida, excedida, ambigua o no admitida produce una respuesta controlada; no se presenta como un resultado válido ni como un conteo de cero.
7. Se conservan las reglas de SPEC-010: una petición ajena al HCM, como generar una función de Python, no puede convertirse en consulta de datos ni invocar herramientas.

## 6. Diseño aprobado

El usuario aprobó [ADR-0014](../../docs/architecture/adr/ADR-0014-dynamic-read-only-hcm-queries.md) el 2026-10-25. La implementación añadirá una herramienta genérica (`query_hr_data`, nombre tentativo) que acepta un plan declarativo, no SQL ni código. Application valida el plan y los atributos restringidos; Infrastructure compone y ejecuta SQL dinámico con identificadores resueltos desde metadatos confiables y valores parametrizados, en modo de solo lectura y con límites.

La aprobación no habilita SQL libre, acceso directo del LLM a la base ni operaciones de escritura. Se conservan los atributos sensibles y las exclusiones previas de §5.

## 7. Criterios de aceptación aprobados

| ID | Criterio verificable aprobado | Evidencia requerida |
|---|---|---|
| AC-055 | «Cuantos empleados tiene la compañia» devuelve el total que informa la fuente autorizada, sin crear una herramienta o plantilla exclusiva para esa frase. El total coincide con una consulta independiente al fixture/base de prueba. | Prueba determinista de la consulta estructurada y del resultado agregado en cada fuente configurada. |
| AC-056 | «Cuantos mauricios hay en la compañia» cuenta todas las coincidencias que cumplen el criterio de nombre, con las reglas aprobadas de normalización; no selecciona una persona ni inventa un total. Una consulta sin coincidencias se distingue de una consulta inválida o no admitida. | Casos con cero, una y varias coincidencias en datos sintéticos; verificar la respuesta y el resultado fuente. |
| AC-057 | Las consultas dinámicas permiten seleccionar y combinar cualquier campo no sensible expuesto por los contratos HCM para directorio, presencia, TimeOff, tareas y marcajes/insights, sin un catálogo de consultas por pregunta ni aprobación individual por campo. Las preguntas existentes de SPEC-008/009 siguen respondiéndose correctamente. | Pruebas de contrato/escenarios contra fixtures de cada fuente y regresión de preguntas existentes. |
| AC-058 | La API ejecuta únicamente consultas de solo lectura; rechaza intentos de modificación, SQL o código libre y conserva autenticación y autorización. El modelo no tiene conexión directa a la base. | Pruebas negativas del plan y permisos; verificar que los datos permanecen sin cambios tras cada rechazo. |
| AC-059 | Salario, número de cuenta y atributos excluidos por guardarraíles previos —incluido el año de nacimiento crudo— no aparecen ni se usan en ninguna consulta, payload o respuesta. La petición «creame una funcion en python para retornar el numero mayor» sigue siendo rechazada sin invocar herramientas; los campos que la fuente no ofrece se declaran no disponibles. | Pruebas de regresión de SPEC-010, inspección de planes/payloads y escenarios de dato restringido, no disponible y fuente indisponible. |

Los límites aprobados por ADR-0014 son: máximo 12 campos, 8 filtros, 3 campos de agrupación, 250 filas, 256 caracteres por valor de filtro y 64 KiB por payload de resultado; el timeout Postgres es configurable (15 segundos por defecto).

## 8. Decisiones y parámetros de implementación

1. El alcance funcional confirmado es: consultas dinámicas de solo lectura sobre los campos disponibles y no sensibles; salario, número de cuenta y exclusiones explícitas anteriores permanecen bloqueados.
2. ADR-0014 está aceptado y autoriza la composición parametrizada en el backend, no el envío de SQL desde el modelo.
3. La implementación aplica y prueba los límites de ADR-0014; solo los datos del contrato HCM y las exclusiones aprobadas podrán consultarse.

No quedan decisiones de producto pendientes para comenzar las tareas; toda desviación del alcance o de los límites de ADR-0014 requiere aprobación humana.

## 9. Plan SDD propuesto

1. T1: implementar de extremo a extremo el plan, validación y ejecución de solo lectura en ambas fuentes, integrarlo al asistente, actualizar documentación y verificar AC-055..AC-059.

## 10. Estado y aprobación

El usuario confirmó el 2026-10-25 que las consultas deben ser dinámicas y de solo lectura, que salario y número de cuenta son datos sensibles excluidos, y que los demás datos disponibles pueden consultarse. ADR-0014 fue aprobado explícitamente; la implementación de T1 está en curso según `odd/tasks/flexible-hcm-query.md`.
