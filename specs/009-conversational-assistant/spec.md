# SPEC-009 — Asistente conversacional con herramientas del backend

- **Estado:** Aprobada e implementada (2026-10-17); enmiendas de nombres y consulta flexible aprobadas (2026-10-18, 2026-10-25); SPEC-020/ADR-0014 implementan la consulta dinámica. El nuevo chat y la presentación de respuestas se rigen por SPEC-021.
- **Implementación:** T1 (orquestador y cliente de proveedor) y T2 (interfaz de chat) en `feat/conversational-assistant`; T3 (documentación) implementada.
- **Requisitos relacionados:** FR-029, FR-030, FR-031, FR-040 y FR-041; NFR-021/022.
- **Criterios relacionados:** AC-029..AC-032 y AC-051 (enmiendas previas); AC-055..AC-059 (SPEC-020).
- **Decisión arquitectónica:** ADR-0011 aceptado (proveedor Groq vía API compatible OpenAI).
- **Decisión de producto registrada:** el usuario eligió “Groq (API en la nube)” el 2026-10-17, tras la corrección de que el modelo no consulta los datos directamente sino que pide herramientas.
- **Versión:** 0.1.

## 1. Objetivo

Añadir una página de chat donde el responsable de RR. HH. pregunte en lenguaje natural y reciba respuestas fundamentadas: el modelo decide qué herramienta llamar, **tu API consulta los datos con la sesión e idioma del usuario** y el modelo redacta la respuesta a partir de esos datos. Es el cierre del arco abierto en la visión del producto.

## 2. Alcance propuesto

### Incluido

- Endpoint `POST /chat` autenticado en la API con bucle de herramientas: el modelo recibe un catálogo cerrado que incluye la capacidad genérica `query_hr_data` (SPEC-020) y las herramientas de dominio; la API valida y ejecuta con el contexto del usuario y devuelve la respuesta junto a la traza de herramientas usadas.
- Cliente LLM genérico sobre HTTP compatible con OpenAI (`/chat/completions` con `tools`) sin SDK ni paquetes nuevos, configurado para Groq en `.env`; intercambiable por un Ollama local cambiando configuración.
- Página `/assistant` en la Web, localizada ES/EN, protegida por sesión, con historial de la conversación, estado de carga y las fuentes/ferramientas empleadas.
- Borrador de oferta laboral en el chat a partir de requisitos escritos por RR. HH.; es una respuesta efímera, no una escritura ni publicación al HCM.
- Límites operativos: tiempo de espera, número de rondas de herramientas y longitud de respuesta; errores del proveedor (sin clave, sin red, límite de tasa) traducidos a mensajes localizados controlados.
- Evaluación: las diez preguntas de SPEC-008 como criterios de aceptación del chat; la lógica de bucle se prueba con un transporte LLM falso y determinista; la pasada en vivo queda documentada y solo se ejecuta con la clave presente.
- Documentación: guía (clave y modelo en `.env`, flujo, coste/límites del nivel gratuito), README, catálogos y registro de decisiones.

### Fuera de alcance

- Que el modelo escriba, modifique o elimine datos (FR-015 sigue vigente: el MVP no tiene escrituras autónomas).
- El modelo elija herramientas fuera del catálogo cerrado, envíe SQL/código o acceda a red/base de datos por su cuenta.
- RAG sobre documentos (FR-014), agentes con roles (FR-017) y streaming de respuestas (v1 no streaming; la interfaz muestra estado de espera).
- Registro de usuarios, roles y permisos nuevos; el acceso es «usuario autenticado» como hoy.
- Recuperación de claves, proveedores alternativos como implementación (solo como configuración) y despliegues productivos.

## 3. Requisitos y límites arquitectónicos

- La IA no accede directamente a los datos: los hechos del HCM pasan por los casos de uso existentes de Application, con la sesión e idioma del usuario autenticado. El borrador de oferta es la excepción textual: usa solo los requisitos proporcionados por RR. HH. y no consulta ni escribe datos (coherente con ADR-0003, FR-013 y NFR-014).
- Resolución de nombres y desambigüación: la búsqueda de personas es insensible a acentos (SPEC-008 enmendada) y, cuando una pregunta de una persona concreta coincide con varios empleados, el asistente lista los candidatos y pregunta cuál antes de responder; nunca elige en silencio. *(Enmienda 2026-10-18.)*
- El catálogo de herramientas es cerrado en el backend e incluye una capacidad genérica de consulta; el modelo no lo amplía ni decide permisos.
- Clave del proveedor y configuración solo en `.env`/User Secrets; nunca en el repositorio ni en imágenes (NFR-004).
- La respuesta debe distinguir los datos devueltos por las herramientas de cualquier texto del modelo: si las herramientas no traen el dato, el asistente lo dice (sin inventar).
- El modelo recibe únicamente los campos solicitados del contrato HCM. La consulta genérica permite cualquier campo no sensible disponible en las vistas, sin aprobación individual; sueldo, números de cuenta y exclusiones previas explícitas (incluido el año de nacimiento crudo) permanecen bloqueados. Un salario introducido por el usuario como requisito de una oferta puede aparecer solo en el borrador de esa oferta.
- No hay escrituras ni publicación: el borrador solo se devuelve como texto en el chat y no se guarda en el HCM.

## 4. Flujo local esperado

1. El usuario abre `/assistant`, autenticado, y escribe «Dime cuantos dias de vacaciones tiene Juan Perez».
2. La API envía la pregunta y el historial al proveedor con el catálogo de herramientas.
3. Para una pregunta sobre datos de una persona concreta, el modelo puede llamar primero a `search_employees` para desambiguar; con varios candidatos pregunta cuál antes de consultar detalles. Una consulta de conteo por fragmento de nombre usa `query_hr_data` e incluye todas las coincidencias.
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

El catálogo cerrado incluye `search_employees`, `presence`, `timeoff`, `tasks`, `attendance_insights`, `upcoming_birthday` y `query_hr_data`. La última acepta un plan declarativo sobre los datasets y campos no sensibles del contrato HCM; no acepta SQL ni código. Ninguna herramienta expone sueldo, números de cuenta ni el año de nacimiento crudo.

## 7. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Evidencia futura |
|---|---|---|
| AC-029 | Las diez preguntas de SPEC-008, formuladas en el chat, producen respuestas que contienen los valores esperados de los fixtures y citan la herramienta usada. | Pruebas del bucle con transporte falso y pasada en vivo documentada con la clave presente. |
| AC-030 | El modelo solo invoca el catálogo cerrado, incluida la herramienta genérica validada; las invocaciones usan la sesión e idioma del usuario, no hay escrituras y una pregunta factual sin datos recibe una respuesta de «sin información» del propio asistente. | Pruebas unitarias del orquestador y revisión del catálogo. |
| AC-031 | Sin clave o sin red la aplicación degrada con mensajes localizados controlados; secretos solo en `.env`; límites de tiempo/rondas activos; suite completa en verde y recorrido de navegador del chat en ES y EN. | Pruebas de degradación, inspección y E2E de navegador. |
| AC-032 | Una pregunta por una persona con nombre compartido («Juan») recibe una respuesta del asistente que lista los candidatos y pregunta cuál antes de responder; tras la elección responde con el dato del fixture, y un nombre sin acento («Juan Perez») resuelve al empleado correcto. *(Enmienda 2026-10-18.)* | Pasada en vivo con la pregunta ambigua y su turno de elección, más pruebas deterministas del matcher sin acentos. |
| AC-051 | La solicitud de borrador de oferta de §13 responde en el chat sin herramientas, se basa solo en los términos proporcionados, pregunta de forma breve solo cuando falta un dato esencial y no guarda ni publica. | Prueba determinista del prompt y pasada en vivo opcional autorizada. |

## 8. Diseño propuesto para planificar

1. `POST /chat` con historial limitado del cliente; orquestador en API con catálogo cerrado y máximo de rondas. `query_hr_data` pasa por el validador de Application y una fuente configurada de solo lectura.
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

Cambios aprobados el 2026-10-24:

- El system prompt se orienta por capacidades: resuelve seguimientos con el historial y conserva la edad derivada `AgeOnNextBirthday`; el año de nacimiento crudo sigue excluido.
- FR-040 aprobó agregados (conteo, agrupación, orden, ranking y extremos) sobre datos HCM; SPEC-020 amplía su ejecución a la fuente mediante un plan genérico.
- El dato de sexo/gender no existe en el contrato HCM actual; no se inventa y se declara no disponible.

### Enmienda aprobada por el usuario: consultas dinámicas (2026-10-25)

El usuario indicó: «No modificacion de datos, consultas dinamicas pero que no contengan datos sensibles como el salario o numero de cuenta. lo demas si».

- `query_hr_data` acepta un plan declarativo para seleccionar campos no sensibles y combinar filtros, agregados, agrupaciones y ordenamientos sobre los datasets de empleados, presencia, TimeOff, Tasks y attendance insights. No se crea una herramienta por pregunta.
- Application valida el plan y la sesión; Infrastructure compila SQL dinámico parametrizado desde metadatos confiables y ejecuta solo lectura. El modelo no puede enviar SQL/código ni modificar datos.
- Salario, número de cuenta y exclusiones previas como el año de nacimiento crudo siguen bloqueados en toda posición del plan. Otros campos no sensibles de las vistas HCM no necesitan aprobación individual.
- Los agregados se calculan en la fuente; errores, campos ausentes y datos inexistentes se explican sin inventar. SPEC-010 conserva el rechazo de solicitudes ajenas como generar una función en Python.
- La aprobación técnica y sus límites están en ADR-0014; SPEC-020 define AC-055..AC-059.
