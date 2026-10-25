# SPEC-009 — Asistente conversacional con herramientas del backend

- **Estado:** Aprobada e implementada (2026-10-17); enmienda de resolución de nombres aprobada e implementada (2026-10-18).
- **Implementación:** T1 (orquestador y cliente de proveedor) y T2 (interfaz de chat) en `feat/conversational-assistant`; T3 (documentación) implementada.
- **Requisitos relacionados (propuestos):** FR-029, FR-030 y NFR-021. Relacionados existentes: FR-012, FR-013, FR-016, FR-020..FR-027; NFR-004, NFR-006, NFR-013, NFR-014.
- **Criterios relacionados (propuestos):** AC-029, AC-030, AC-031 y AC-032 (enmienda 2026-10-18).
- **Decisión arquitectónica:** ADR-0011 aceptado (proveedor Groq vía API compatible OpenAI).
- **Decisión de producto registrada:** el usuario eligió “Groq (API en la nube)” el 2026-10-17, tras la corrección de que el modelo no consulta los datos directamente sino que pide herramientas.
- **Versión:** 0.1.

## 1. Objetivo

Añadir una página de chat donde el responsable de RR. HH. pregunte en lenguaje natural y reciba respuestas fundamentadas: el modelo decide qué herramienta llamar, **tu API consulta los datos con la sesión e idioma del usuario** y el modelo redacta la respuesta a partir de esos datos. Es el cierre del arco abierto en la visión del producto.

## 2. Alcance propuesto

### Incluido

- Endpoint `POST /chat` autenticado en la API con bucle de herramientas: el modelo recibe un catálogo fijo de herramientas (los endpoints de SPEC-008, `upcoming_birthday` y los insights de SPEC-002), la API los ejecuta en proceso con el contexto del usuario y devuelve la respuesta junto a la traza de herramientas usadas.
- Cliente LLM genérico sobre HTTP compatible con OpenAI (`/chat/completions` con `tools`) sin SDK ni paquetes nuevos, configurado para Groq en `.env`; intercambiable por un Ollama local cambiando configuración.
- Página `/assistant` en la Web, localizada ES/EN, protegida por sesión, con historial de la conversación, estado de carga y las fuentes/ferramientas empleadas.
- Borrador de oferta laboral en el chat a partir de requisitos escritos por RR. HH.; es una respuesta efímera, no una escritura ni publicación al HCM.
- Límites operativos: tiempo de espera, número de rondas de herramientas y longitud de respuesta; errores del proveedor (sin clave, sin red, límite de tasa) traducidos a mensajes localizados controlados.
- Evaluación: las diez preguntas de SPEC-008 como criterios de aceptación del chat; la lógica de bucle se prueba con un transporte LLM falso y determinista; la pasada en vivo queda documentada y solo se ejecuta con la clave presente.
- Documentación: guía (clave y modelo en `.env`, flujo, coste/límites del nivel gratuito), README, catálogos y registro de decisiones.

### Fuera de alcance

- Que el modelo escriba, modifique o elimine datos (FR-015 sigue vigente: el MVP no tiene escrituras autónomas).
- El modelo elija herramientas fuera del catálogo fijo o acceda a red/base de datos por su cuenta.
- RAG sobre documentos (FR-014), agentes con roles (FR-017) y streaming de respuestas (v1 no streaming; la interfaz muestra estado de espera).
- Registro de usuarios, roles y permisos nuevos; el acceso es «usuario autenticado» como hoy.
- Recuperación de claves, proveedores alternativos como implementación (solo como configuración) y despliegues productivos.

## 3. Requisitos y límites arquitectónicos

- La IA no accede directamente a los datos: los hechos del HCM pasan por los casos de uso existentes de Application, con la sesión e idioma del usuario autenticado. El borrador de oferta es la excepción textual: usa solo los requisitos proporcionados por RR. HH. y no consulta ni escribe datos (coherente con ADR-0003, FR-013 y NFR-014).
- Resolución de nombres y desambigüación: la búsqueda de personas es insensible a acentos (SPEC-008 enmendada) y, cuando una pregunta de una persona concreta coincide con varios empleados, el asistente lista los candidatos y pregunta cuál antes de responder; nunca elige en silencio. *(Enmienda 2026-10-18.)*
- El catálogo de herramientas es fijo y cerrado en el backend; el modelo no lo amplía ni decide permisos.
- Clave del proveedor y configuración solo en `.env`/User Secrets; nunca en el repositorio ni en imágenes (NFR-004).
- La respuesta debe distinguir los datos devueltos por las herramientas de cualquier texto del modelo: si las herramientas no traen el dato, el asistente lo dice (sin inventar).
- El modelo solo recibe atributos proyectados explícitamente por los DTOs de las herramientas; el sueldo de empleados y los datos bancarios están restringidos, y campos no aprobados no se añaden al payload. Un salario introducido por el usuario como requisito de una oferta puede aparecer solo en el borrador de esa oferta.
- No hay escrituras ni publicación: el borrador solo se devuelve como texto en el chat y no se guarda en el HCM.

## 4. Flujo local esperado

1. El usuario abre `/assistant`, autenticado, y escribe «Dime cuantos dias de vacaciones tiene Juan Perez».
2. La API envía la pregunta y el historial al proveedor con el catálogo de herramientas.
3. Para una pregunta de persona, el modelo llama primero a `search_employees(name="Juan")`: con un único candidato continúa con `timeoff`, y con varios (p. ej. Juan Pérez y Juan Herrera) devuelve una pregunta con los candidatos antes de consultar datos. *(Enmienda 2026-10-18.)*
4. El modelo redacta la respuesta a partir de los datos reales; la traza de herramientas viaja con la respuesta.
5. Si el proveedor falla o agota el tiempo, la interfaz muestra un mensaje localizado controlado; la aplicación sigue funcionando sin IA.
6. Las diez preguntas de SPEC-008 producen respuestas que contienen los valores de los fixtures (criterio de aceptación automatizado donde sea posible).
7. Para «Quien es la proxima persona que cumple años», el modelo llama a `upcoming_birthday` y redacta a partir del resultado, sin calcular desde fechas de nacimiento.
8. Para «Que cargo tiene mauricio», el modelo busca a la persona y responde con el `JobTitle` devuelto por `search_employees`.
9. Para «Por ejemplo: quiero que crees una oferta de vigilante para la ciudad de Bogotá, horarios rotativos, salario 2.500.000 pesos colombianos y la ia me de un texto de aproximadamente unos 500 caracteres con ese requerimiento», el asistente genera un borrador en el idioma del usuario, usa solo esos términos, no invoca herramientas y no lo guarda ni publica.

## 5. Componentes candidatos

| Componente | Responsabilidad propuesta | Línea base que debe conservar |
|---|---|---|
| API — endpoint de chat | Recibir pregunta/historial, orquestar el bucle de herramientas, devolver respuesta y traza. | Sesión obligatoria; errores controlados. |
| API — herramientas | Declaraciones de las capacidades existentes y su ejecución en proceso. | Sin nuevo acceso a datos; mismos casos de uso. |
| API — cliente LLM | HTTP compatible OpenAI con `tools`; Groq por configuración. | Sin SDK; sin secretos en código. |
| Web — página de chat | Conversación localizada ES/EN con estado de carga y fuentes. | Sin acceso a datos; token del cliente existente. |
| Configuración | `AI__*` en `.env.example` y Compose. | Claves fuera de Git; degradación sin proveedor. |
| Evaluación | Traducción del banco de SPEC-008 al chat + pruebas del bucle con transporte falso. | Determinista sin red; pasada en vivo documentada. |
| Documentación | Guía, README, catálogos, registro. | Comandos verificados; sin secretos. |

## 6. Herramientas del catálogo (v1)

`search_employees` (nombre/oficina/apellido y campos de directorio aprobados), `presence` (fecha), `timeoff` (empleado), `tasks` (empleado), `attendance_insights` (rango), `upcoming_birthday` (sin argumentos; resultado de la fuente HCM). El catálogo permanece cerrado; ninguna herramienta devuelve sueldo, datos bancarios ni atributos no aprobados.

## 7. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Evidencia futura |
|---|---|---|
| AC-029 | Las diez preguntas de SPEC-008, formuladas en el chat, producen respuestas que contienen los valores esperados de los fixtures y citan la herramienta usada. | Pruebas del bucle con transporte falso y pasada en vivo documentada con la clave presente. |
| AC-030 | El modelo solo invoca el catálogo fijo, las invocaciones usan la sesión e idioma del usuario, no hay escrituras y una pregunta factual sin datos recibe una respuesta de «sin información» del propio asistente. | Pruebas unitarias del orquestador y revisión del catálogo. |
| AC-031 | Sin clave o sin red la aplicación degrada con mensajes localizados controlados; secretos solo en `.env`; límites de tiempo/rondas activos; suite completa en verde y recorrido de navegador del chat en ES y EN. | Pruebas de degradación, inspección y E2E de navegador. |
| AC-032 | Una pregunta por una persona con nombre compartido («Juan») recibe una respuesta del asistente que lista los candidatos y pregunta cuál antes de responder; tras la elección responde con el dato del fixture, y un nombre sin acento («Juan Perez») resuelve al empleado correcto. *(Enmienda 2026-10-18.)* | Pasada en vivo con la pregunta ambigua y su turno de elección, más pruebas deterministas del matcher sin acentos. |
| AC-051 | La solicitud de borrador de oferta de §13 responde en el chat sin herramientas, se basa solo en los términos proporcionados, pregunta de forma breve solo cuando falta un dato esencial y no guarda ni publica. | Prueba determinista del prompt y pasada en vivo opcional autorizada. |

## 8. Diseño propuesto para planificar

1. `POST /chat` con historial limitado del cliente; orquestador en API con catálogo fijo de herramientas y máximo de rondas; cada herramienta delega en su caso de uso.
2. Cliente HTTP compatible OpenAI (`Authorization: Bearer` + `tools`); configuración `AI__Provider`, `AI__BaseUrl`, `AI__ApiKey`, `AI__Model`, `AI__TimeoutSeconds`, `AI__MaxTokens`, `AI__MaxToolRounds`.
3. Groq como configuración por defecto: `BaseUrl=https://api.groq.com/openai/v1` y `AI__Model=openai/gpt-oss-120b` (id con soporte de herramientas confirmado con la API de Groq el 2026-10-18; los modelos Llama no estaban disponibles para la clave de prueba, se documenta como alternativa `openai/gpt-oss-20b`).
4. Respuesta del chat: `{ answer, toolsUsed[], sources[] }`; la interfaz muestra la respuesta y las herramientas empleadas.
5. Web envía historial (máximo N mensajes) y reutiliza el cliente autenticado existente.
6. Degradación: sin `AI__ApiKey` el endpoint responde 503 con mensaje localizado; errores 429/timeout se traducen a mensajes controlados sin reintentos ciegos.
7. Resolución de nombres (enmienda 2026-10-18): matcher del source insensible a acentos, regla de desambigüación en el system prompt (varios candidatos → preguntar, no elegir), fixture «Juan Herrera» como segundo «Juan», y el banco de SPEC-009 añade la pregunta ambigua «Dime cuantos dias de vacaciones tiene Juan.» con la séptima pregunta de la pasada en vivo.

Supuestos explícitos para aprobación: respuesta no streaming; el usuario asume el coste/límites del nivel gratuito de Groq (verificarlos el día de elegir el modelo); los datos salen hacia el proveedor (hoy solo sintéticos — con HCM real esta salida requiere decisión de política); el historial vive en el cliente y se pierde al recargar.

## 9. Plan SDD propuesto

1. Añadir FR-029, FR-030 y NFR-021 y AC-029..AC-031 con trazabilidad.
2. Aceptación de ADR-0011 y actualización del registro de decisiones y de la sección de estado de la visión (SPEC-008 añadió que el chat espera especificación y proveedor: esto la es).
3. Rama `feat/conversational-assistant` desde `main` y plan ODD: T1 (orquestador, cliente LLM y herramientas con pruebas del bucle), T2 (página de chat ES/EN y E2E en vivo con la clave), T3 (documentación, degradación y verificación final).
4. Verificación: suite completa, pruebas del bucle deterministas, pasada en vivo de las diez preguntas con la clave presente y verificador independiente.
5. Cierre con matriz y commits por work unit.

## 10. Estado y aprobación

SPEC-009 y ADR-0011 fueron aprobados para planificación el 2026-10-17; la enmienda de resolución de nombres y desambigüación (SPEC-008 y las secciones 3, 4 y 7 de esta especificación) fue aprobada el 2026-10-18. La especificación está implementada en la rama `feat/conversational-assistant` con la siguiente evidencia observada para AC-029..AC-032:

- Pasada en vivo de las siete preguntas con la clave presente: `powershell -NoProfile -File scripts\verify-assistant-questions.ps1 -PaceSeconds 9` con salida `0`.
- Recorrido de navegador E2E en español e inglés, incluido el turno de elección de la pregunta ambigua.
- Verificación independiente: cinco claims MET, sin bloqueos.
- Suite completa 70/70 (5 unit + 65 integración) y build 0 errores/0 avisos.

Commits: `8861456`, `ef9750e`, `ab6d2bc`, `902e25a`, `4de7672`, `d1ce2e4`, `fb71938`.

## 11. Enmienda aprobada por el usuario (2026-10-23)

El usuario aprobó ampliar el asistente para responder «Quien es la proxima persona que cumple años» y «Que cargo tiene mauricio». La herramienta `upcoming_birthday` devuelve la fecha y empleado(s) más próximos determinados por la fuente; `search_employees` incluye el cargo confirmado desde `"job:title"`. El resultado no incluye el año de nacimiento. Sueldo y datos bancarios quedan restringidos; todo campo nuevo requiere aprobación antes de añadirse al payload.

## 12. Enmienda: fecha de contratación y búsqueda por apellido (2026-10-23)

Se añaden `HireDate`, `LastName` y el filtro opcional `surname_contains` a la proyección aprobada de `search_employees`. La búsqueda por apellido usa el valor `last_name` del HCM y devuelve todos los candidatos; el asistente no debe inferir el apellido desde otra parte del nombre ni elegir en silencio. El corpus de SPEC-008 pasa a diez preguntas de referencia, más la pregunta ambigua de AC-032.

## 13. Enmienda: borrador de oferta laboral (2026-10-24)

El usuario aclaró que RR. HH. debe poder pedir un borrador inicial de oferta a partir de requisitos, mostrarlo en el chat y no guardarlo ni publicarlo todavía. El modelo usa solo los datos proporcionados, pregunta de forma breve cuando falta un detalle esencial, omite términos opcionales desconocidos y apunta a unos 500 caracteres cuando se solicite. El sueldo de un empleado sigue restringido; un salario provisto expresamente para la oferta puede repetirse únicamente en su borrador.

## 14. Enmienda: seguimiento conversacional y consulta fluida (2026-10-24)

Verbatim del usuario: «No se que haces pero llevas dos horas dando vueltas, ademas parece que no estas haciendo lo de hacerlo mas natural, veo que estas enfocado en el ejemplo que te di pero si la pregunta fuera, cuantos empleados tiene la sede de tokio o cuantos son se sexo masculino y de sexo femenino o quien es el empleado mas antiguo o quien es el empleado mas joven o quien lleva mas años en la compañia o ese tipo de preguntas, no quiero que simplemente te las aprendas o que tengas un select para cada una. quiero que la ia tenga mas fluides al consultar cosas y no solo las preguntas predeterminadas. pero necesito calidad».

Cambios aprobados:

- El system prompt pasa de lista de recetas por tipo de pregunta a orientarse por capacidades: resuelve seguimientos con el historial («esa persona»), deriva agregados (contar, agrupar, ordenar, ranking, min/máx) **solo** sobre los registros que devuelven las herramientas, encadena varias llamadas y, si un filtro no devuelve filas, amplía la consulta en vez de concluir que no hay datos.
- El catálogo **no crece**: sigue cerrado con las seis herramientas del §6. La fluidez no añade un `select` por pregunta; «Cuantos empleados tiene la sede de tokio?» se responde con `search_employees` + recuento sobre los registros, y «quién es el empleado más antiguo / quién lleva más años» con `search_employees` + ordenación por `HireDate`.
- `search_employees` y `upcoming_birthday` proyectan la EDAD DERIVADA `AgeOnNextBirthday` (aprobación «Edad derivada (Recomendado)», 2026-10-24): la edad que cumple en el próximo cumpleaños, anclada a la fecha de consulta de cada fuente; `null` cuando la fuente no tiene cumpleaños. El año de nacimiento crudo sigue prohibido en cualquier payload del modelo y `GET /employees` conserva exactamente `id`, `displayName`, `officeIds`.
- Banco en vivo: +3 probes (seguimiento de dos turnos con historial, recuento por oficina, ranking de antigüedad). SPEC-010 y sus guardrails no cambian.
- Dato inexistente: el shape real (`db/hcm-real/upstream.sql`) no contiene columna de sexo/gender; hasta aprobación explícita de un atributo nuevo (SPEC-010 §11) esas preguntas se responden con «información no disponible», verificado en vivo.
