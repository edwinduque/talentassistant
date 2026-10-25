# SPEC-022 — Balanceo y continuidad entre proveedores de IA

- **Estado:** Aprobada e implementada (2026-10-25) en `feat/ai-provider-balancing`; commits de implementación `a4be5db` y `78c4de2`, cierre documental en este commit; sin push ni PR.
- **Fecha:** 2026-10-25.
- **Requisitos relacionados:** FR-029, FR-030, NFR-021, NFR-024.
- **Criterios relacionados:** AC-062, AC-063, AC-064, AC-065.
- **Decisión arquitectónica:** ADR-0015 aceptado.
- **Decisión de producto registrada:** el usuario pidió añadir Gemini 2.5 Flash y distribuir las peticiones entre proveedores con reintento en el otro, evitando consumir la cuota diaria en pruebas.
- **Versión:** 0.1.

## 1. Objetivo

Añadir un segundo proveedor de IA (Google Gemini, modelo `gemini-2.5-flash`) y un enrutador que reparta las conversaciones entre Groq y Gemini, continúe con el otro proveedor cuando el preferido falle y mantenga una experiencia de respuesta consistente entre ambos.

La cuota gratuita de Groq es limitada y ya se agotó en una jornada. El sistema debe seguir respondiendo cuando un proveedor esté limitado, agotado o no disponible, sin duplicar escrituras (no existen) y sin cambiar el contrato funcional del chat.

## 2. Problema y contexto actual

`IAssistantLanguageModel` tiene una única implementación, `OpenAiCompatibleLanguageModel`, registrada con un `HttpClient` y una configuración únicos (`AI:BaseUrl`, `AI:ApiKey`, `AI:Model`). El orquestador no conoce proveedores: recibe el puerto y ejecuta el bucle de herramientas. Cuando Groq devuelve 429, `POST /chat` responde 429 y la interfaz muestra «El servicio está ocupado»; no hay alternativa.

Gemini 2.5 Flash ofrece un endpoint compatible con OpenAI (`https://generativelanguage.googleapis.com/v1beta/openai/`), autenticación Bearer y soporte de `tools`, por lo que el mismo cliente HTTP puede usarse con otra base, clave y modelo. La cuota gratuita de Gemini también es limitada y se consulta en AI Studio; no se hardcodean cifras.

El riesgo principal no es técnico sino de experiencia: si cada turno elige un modelo distinto, la conversación cambia de estilo, de estructura y de criterio a mitad de hilo. El diseño debe repartir carga sin que el usuario lo note.

### Solicitud literal

```text
Nuevo spec, debido a que la capa gratuita de groq es bastante limitada quiero agregar una nueva api de gemini con el modelo 2.5 flash. quiero que implementemos un sistema en el cual podamos tener como una specie de balanceador en el cual algunas peticiones se vayan por uno o por otro modelo o si es el caso que intente con un modelo y si falla lo haga con el otro. Cuando hagas pruebas no acabes con los tokens del día porque precisamente eso me pasó con groq. elabora la mejor estrategia y tambien quiero que las respuestas no sean tan diferentes entre modelos
```

## 3. Estrategia aprobada

### 3.1 Dos proveedores tras el mismo puerto

Groq y Gemini se configuran como dos instancias del mismo cliente compatible con OpenAI. `ChatOrchestrator` sigue dependiendo solo de `IAssistantLanguageModel`; un enrutador implementa ese puerto y decide qué instancia atiende cada completion. No hay SDK nuevo ni acoplamiento del dominio a un proveedor.

### 3.2 Reparto pegajoso por conversación, no por llamada

La unidad de balanceo es la **conversación**, no la llamada HTTP. Un identificador opcional `conversationId` viaja en `POST /chat`; la Web lo genera al abrir `/assistant` y lo renueva con «Nuevo chat». El servidor lo usa para elegir proveedor de forma determinista y ponderada:

- Misma conversación → mismo proveedor durante todos sus turnos, incluidas las rondas de herramientas.
- Conversaciones distintas → reparto según pesos configurables (50/50 por defecto).
- Sin `conversationId` (clientes antiguos) → reparto por turno; el comportamiento nuevo de la Web siempre lo envía.
- No se persiste la asignación: el hash determinista del identificador y los pesos la reconstruyen.

Así «algunas peticiones se van por uno o por otro» sin que una misma conversación cambie de modelo entre preguntas.

### 3.3 Failover por completion sobre el mismo transcript

Si el proveedor preferido falla en cualquier punto del bucle, el enrutador reintenta **esa misma completion** con el otro proveedor usando el transcript ya construido (incluidos los resultados de herramientas). No se reejecutan herramientas ni se repite la pregunta. Tras el failover, el resto del turno permanece en el proveedor alternativo para no alternar dentro de una misma respuesta.

Se considera fallo con failover cualquier `AssistantProviderUnavailableException` del proveedor (429, 5xx, timeout, red, respuesta inválida, error transitorio de herramienta); `NotConfigured` solo aplica cuando no hay ningún proveedor configurado y mantiene la degradación actual.

Riesgo controlado: el proveedor alternativo debe aceptar un transcript con `tool_calls` emitidos por el otro proveedor. El protocolo OpenAI lo permite, pero la compatibilidad de Gemini se verifica con un transporte falso y una llamada en vivo opt-in; si el transcript no es aceptado, el turno devuelve el error controlado en lugar de reintentar sin límite.

### 3.4 Presupuesto de intentos y cortacircuitos

- En modo enrutador: **un intento HTTP por proveedor y completion** (máximo dos llamadas). Se evita el reintento interno actual de 5xx/`tool_use_failed` para no gastar cuota dos veces en el mismo proveedor.
- En modo proveedor único: se conserva el comportamiento actual (dos intentos ante 5xx o `tool_use_failed`).
- Cortacircuitos en memoria por proveedor: tras N fallos consecutivos (2 por defecto) el proveedor queda no disponible durante un enfriamiento (120 s por defecto) y el tráfico va al otro; al expirar, una sonda decide si vuelve. El estado es por proceso y no se persiste.
- Si ambos fallan: se devuelve el error localizado existente. Precedencia: `RateLimited` si ambos fueron limitados; en otro caso, el motivo del último fallo observado.

### 3.5 Consistencia entre modelos

- Un único system prompt, un único catálogo de herramientas, el mismo idioma del perfil y las mismas reglas de formato de SPEC-021. Sin instrucciones específicas por proveedor que cambien el estilo.
- Misma temperatura (0.1) y mismo `max_tokens` en ambos. En Gemini se fija `reasoning_effort=low` (presupuesto de pensamiento 1.024) para acercar latencia y verbosidad a Groq; es configurable y puede desactivarse con `none`.
- Adaptador de esquema por proveedor: para Gemini se reducen las uniones anulables y multi-tipo al primer tipo no nulo, se añade un `items` genérico a los arrays sin elementos y se elimina `additionalProperties`; el esquema canónico de Groq no cambia y el backend sigue validando los argumentos. Se prueba con un proveedor falso estricto.
- Verificación de paridad con tres preguntas de referencia (un conteo, una persona y una lista): se comparan hechos y estructura, no texto literal. Una respuesta no debe mencionar el modelo ni el proveedor.
- La asignación pegajosa por conversación es la principal defensa contra la diferencia de estilo; la paridad se mide, no se asume.

### 3.6 Configuración compatible con el despliegue actual

- Groq conserva `AI:BaseUrl`, `AI:ApiKey` y `AI:Model`, de modo que el despliegue actual sigue funcionando sin cambios.
- Gemini añade `AI:Gemini:BaseUrl` (por defecto el endpoint compatible), `AI:Gemini:ApiKey`, `AI:Gemini:Model` (`gemini-2.5-flash`) y `AI:Gemini:ReasoningEffort` (`low`).
- Enrutamiento: `AI:Routing:GroqWeight` (50), `AI:Routing:GeminiWeight` (50), `AI:Routing:FailureThreshold` (2) y `AI:Routing:CooldownSeconds` (120).
- `AI:Provider` pasa a aceptar `router` (por defecto), `groq` o `gemini`; los dos últimos fuerzan un proveedor único para diagnóstico.
- Si solo hay una clave configurada, el sistema opera en modo proveedor único sin error. Si no hay ninguna, se mantiene `NotConfigured`.

### 3.7 Seguridad, cuotas y verificación

- Claves solo en `.env`/User Secrets; nunca en el repositorio, imágenes, logs ni mensajes de error.
- Logs estructurados por completion: proveedor, modelo, resultado, latencia y motivo de failover. Nunca prompt, respuesta, claves ni datos de personas.
- **Cero llamadas reales en CI y en la suite por defecto.** Las pruebas deterministas usan transportes falsos que simulan 200, 429, 5xx, timeout, `tool_use_failed` y respuestas inválidas.
- La verificación en vivo es un script opcional con presupuesto duro: `-MaxCalls` (4 por defecto), `-PaceSeconds` (15 por defecto) y tope por proveedor; no se ejecuta sin autorización explícita del usuario.
- Gemini en capa gratuita puede usar prompts y respuestas para mejorar sus productos. El MVP solo envía datos sintéticos; con HCM real se requiere una decisión de política (ADR-0011/ADR-0015).
- Una conversación puede quedar asignada a un proveedor limitado; el cortacircuitos y el failover protegen el turno, y el siguiente turno puede cambiar de proveedor si el preferido sigue no disponible. La asignación no se persiste.

## 4. Alcance

### Incluido

- Segundo proveedor Gemini 2.5 Flash mediante el endpoint compatible con OpenAI.
- Enrutador ponderado y pegajoso por conversación, con failover por completion y cortacircuitos.
- Identificador opcional `conversationId` en `POST /chat` y su generación en la Web (incluido «Nuevo chat»).
- Configuración compatible, validación al arranque, observabilidad sin secretos.
- Pruebas deterministas, script de paridad y de humo opcionales con presupuesto de llamadas.
- ADR-0015, actualización de requisitos/criterios al aprobarse y documentación operativa.

### Fuera de alcance

- Sustituir Groq o eliminar el proveedor actual.
- Un gateway/proxy externo, balanceo entre instancias o estado distribuido del cortacircuitos.
- Persistir conversaciones, asignaciones de proveedor o métricas históricas.
- Streaming, cambios al catálogo de herramientas, a los guardarraíles o a la autenticación.
- Ajuste fino de prompts por proveedor o comparación literaria de respuestas.
- Usar datos reales del HCM o cambiar la política de egreso.

## 5. Comportamiento esperado

| Situación | Resultado esperado |
|---|---|
| Ambos proveedores configurados y sanos; misma conversación | Todos los turnos usan el proveedor asignado por el hash y los pesos; no cambia a mitad de hilo. |
| Conversaciones nuevas | Se reparten según los pesos; con 50/50, ambas rutas reciben tráfico. |
| El proveedor preferido devuelve 429 o 5xx | El turno continúa en el otro proveedor con el mismo transcript; el usuario recibe una sola respuesta. |
| El proveedor preferido agota su tiempo de espera | Se reintenta en el otro; si ambos fallan, se muestra el mensaje controlado existente. |
| Solo una clave configurada | Modo proveedor único; el chat funciona como hoy. |
| Ninguna clave | `NotConfigured` y la degradación actual. |
| Se pulsa «Nuevo chat» | La conversación recibe un identificador nuevo y puede asignarse al otro proveedor. |
| El proveedor cae y se recupera | Tras el enfriamiento, una sonda lo devuelve al reparto. |

## 6. Requisitos y límites

1. El dominio y `ChatOrchestrator` no conocen proveedores; solo el puerto `IAssistantLanguageModel`.
2. La selección es determinista para un mismo `conversationId` y pesos; no usa aleatoriedad ni estado persistido.
3. Una misma conversación no cambia de proveedor por turno mientras el asignado esté disponible.
4. El failover reutiliza el transcript y no repite herramientas ya ejecutadas; las herramientas son de solo lectura.
5. Los intentos por completion están acotados (dos en modo enrutador) para proteger las cuotas.
6. Si ambos proveedores fallan, se conservan los mensajes localizados y los códigos HTTP actuales.
7. Las claves y el contenido de prompts/respuestas no se registran; los logs solo llevan metadatos.
8. La configuración actual de Groq sigue siendo válida; los despliegues sin Gemini no se rompen.
9. La suite y el CI no hacen llamadas reales; los scripts en vivo son opt-in y con presupuesto.
10. Las respuestas mantienen el formato de SPEC-021 y no mencionan el proveedor.
11. Gemini recibe solo datos sintéticos hasta que exista una decisión de política para datos reales.
12. Cualquier cambio de pesos, proveedor o contrato requiere aprobación humana.

## 7. Diseño implementado

| Componente | Responsabilidad | Límite |
|---|---|---|
| `RoutingLanguageModel` | Implementa `IAssistantLanguageModel`; elige proveedor por hash/pesos, hace failover y mantiene el proveedor del turno. | Sin lógica de negocio ni acceso a datos. |
| `AssistantProviderHealth` | Estado en memoria de fallos/ enfriamiento por proveedor. | Por proceso; no distribuido. |
| Cliente compatible OpenAI | Dos instancias con base, clave, modelo y tiempo de espera propios. | Sin SDK; mismo formato de request. |
| Opciones de configuración | Valida claves, pesos y modelo; conserva las claves actuales de Groq. | Secretos solo en configuración. |
| Contrato `/chat` | Campo opcional `conversationId` (GUID, longitud acotada) y mismo response. | Aditivo; clientes antiguos siguen funcionando. |
| Web | Genera y renueva el identificador de conversación; lo envía en cada turno. | Sin persistencia. |
| Adaptador de esquema | Ajusta el catálogo al subconjunto aceptado por cada proveedor si hace falta. | Esquema canónico intacto para Groq. |
| Observabilidad | Log por completion con proveedor, resultado, latencia y failover. | Sin contenido sensible. |

No se añaden paquetes. Gemini usa el mismo cliente HTTP; solo cambian configuración y adaptación de payload si la verificación lo exige.

## 8. Criterios de aceptación aprobados

| ID | Criterio verificable aprobado | Evidencia requerida |
|---|---|---|
| AC-062 | Con ambos proveedores sanos, una conversación mantiene el mismo proveedor en todos sus turnos según el hash y los pesos; conversaciones distintas se reparten y «Nuevo chat» puede cambiar la asignación. Sin estado persistido. | Pruebas deterministas del selector con pesos y miles de identificadores; verificación de que no hay cambios por turno. |
| AC-063 | Ante 429, 5xx, timeout, error de red o respuesta inválida del proveedor preferido, el turno termina con el otro proveedor sobre el mismo transcript, sin repetir herramientas; si ambos fallan, se conserva el mensaje localizado y el código HTTP actual. | Transportes falsos por escenario, incluido failover a mitad del bucle y doble fallo. |
| AC-064 | Ambos proveedores reciben el mismo system prompt, catálogo, idioma, formato y parámetros efectivos; las tres preguntas de paridad devuelven los mismos hechos y estructura, sin mencionar el proveedor, y el formato de SPEC-021 se mantiene. | Pruebas de payload por proveedor, adaptador de esquema y pasada de paridad opcional con presupuesto. |
| AC-065 | La suite y el CI no hacen llamadas reales; los scripts en vivo exigen autorización, tienen tope de llamadas y ritmo, y no imprimen claves ni contenido. Los despliegues sin Gemini siguen funcionando. | Inspección de la suite/CI, pruebas de configuración y ejecución controlada del script con `-MaxCalls`. |

## 9. Plan SDD

1. T1: opciones y validación de proveedores, dos clientes Gemini/Groq, enrutador pegajoso, cortacircuitos y contrato `conversationId`, con pruebas deterministas.
2. T2: integración con la Web, prompt/parámetros de consistencia, adaptador de esquema y observabilidad.
3. T3: ADR-0015, catálogos de requisitos/criterios, documentación operativa y script de paridad/humo con presupuesto; verificación sin llamadas reales por defecto.

## 10. Estado y aprobación

El usuario aprobó SPEC-022 y ADR-0015 el 2026-10-25 con pesos 50/50, failover total y un presupuesto de pruebas de cuatro llamadas.

Decisiones confirmadas:

1. Adoptar ADR-0015 y el reparto pegajoso por conversación con `conversationId` opcional.
2. Pesos iniciales 50/50, configurables.
3. Failover ante cualquier fallo de proveedor y un intento HTTP por proveedor en modo enrutador.
4. `reasoning_effort=low` en Gemini 2.5 Flash y temperatura 0.1 compartida.
5. Scripts en vivo opt-in con `-MaxCalls 4` y `-PaceSeconds 15`.

Implementación: commits `a4be5db` (configuración, enrutador, cortacircuitos, adaptador y pruebas) y `78c4de2` (identificador de conversación en API/Web y pruebas), más el cierre documental.

Evidencia observada: build con 0 errores y 0 avisos; 261/261 pruebas de integración en verde, incluidas configuraciones válidas e inválidas, selección determinista, failover, ambos fallos, cortacircuitos, recuperación tras el enfriamiento, payloads por proveedor, esquema normalizado y el `conversationId` de extremo a extremo. Pruebas unitarias: 23/24; la única falla es el bloqueo ambiental de App Control (`0x800711C7`) al cargar `TalentAssistant.Domain.dll`, reproducido en sesiones anteriores y ajeno a este cambio. No se ejecutaron llamadas reales a Groq o Gemini; el script de humo se validó con `-DryRun` y queda pendiente la pasada en vivo autorizada.

Limitación: la aceptación de Gemini del transcript con `tool_calls` generados por Groq y del esquema normalizado solo puede confirmarse con la pasada en vivo opt-in; el diseño devuelve el error controlado si el proveedor alternativo no acepta el transcript.
