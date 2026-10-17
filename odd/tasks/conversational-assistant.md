# Seguimiento ODD: asistente conversacional

Objetivo: chat en lenguaje natural donde el modelo decide qué herramienta llamar y la API consulta los datos con la sesión e idioma del usuario, con respuestas verificables y degradación controlada sin proveedor.
Rama: `feat/conversational-assistant`; entrega: `feature-branch-chain`; runner: SDK `10.0.402`, `dotnet build`, `dotnet test`, smoke Compose y navegador ES/EN (la pasada en vivo con Groq requiere `AI__ApiKey` en `.env`); revisión nativa desactivada globalmente (`disabled/unmanaged`).

## Specs

### S1 — Bucle de herramientas en la API

Alcance: endpoint `POST /chat` autenticado con historial limitado del cliente, orquestador con catálogo fijo de herramientas (`search_employees`, `presence`, `timeoff`, `tasks`, `attendance_insights`), máximo de rondas y respuesta con `answer` + traza de herramientas; degradación 503 localizada sin clave.

Texto exacto del usuario: “el modelo consulta fuentes de datos y da la respuesta” (corregido: el modelo pide herramientas, la API consulta).

Aceptación: las invocaciones delegan en los casos de uso existentes con la sesión e idioma del usuario; el modelo no amplía el catálogo; sin `AI__ApiKey` → 503 localizado; AC-030 verificable con pruebas del orquestador.

### S2 — Cliente de proveedor

Alcance: cliente HTTP compatible OpenAI con `tools` sin SDK, configuración `AI__*` (`Provider`, `BaseUrl`, `ApiKey`, `Model`, `TimeoutSeconds`, `MaxTokens`, `MaxToolRounds`), Groq por defecto, secretos solo en `.env`.

Aceptación: sin clave o sin red no hay excepciones fuera de control (mensaje localizado); errores 429/timeout traducidos; el ID del modelo se confirma al implementar contra la lista vigente del proveedor; NFR-004 intacta.

### S3 — Interfaz de chat

Alcance: página `/assistant` localizada ES/EN, protegida por sesión, con historial en cliente, estado de carga, respuesta y fuentes/herramientas mostradas; reutiliza el cliente autenticado existente.

Aceptación: E2E de navegador en ambos idiomas: preguntar, ver respuesta y herramientas, cerrar sesión; sin acceso a datos desde Web; consola sin errores.

### S4 — Límites y evaluación

Alcance: traducción del banco de SPEC-008 al chat, pruebas deterministas del bucle con transporte LLM falso, pasada en vivo documentada con la clave presente, límites de rondas/tiempo/longitud.

Texto exacto del usuario: “que pueda preguntarle” (las seis preguntas de referencia son el criterio).

Aceptación: AC-029 (las seis preguntas respondidas con los valores del fixture y la herramienta citada), AC-030 y AC-031; la suite no depende de red ni de clave.

### S5 — Documentación y verificación

Alcance: guía (clave/modelo en `.env`, flujo, coste y límites del tier gratuito, degradación), README, FR-029/FR-030, NFR-021, AC-029..AC-031, registro de decisiones y actualización del estado de la visión.

Aceptación: trazabilidad completa; guía reproducible sin secretos; suite verde; recorrido de navegador; verificador independiente sin bloqueos.

Risk: item 2 (nuevo endpoint autenticado y manejo de clave de proveedor). T1 requiere verificación independiente tras sus checks propios.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1, S2 | Inline | Implementada; build 0/0, suite 63/63 con16 pruebas del bucle/cliente sin red | Commit pendiente en `feature-branch-chain` |
| T2 | S3, S4 | Inline | Planificada | Página de chat ES/EN + banco traducido + E2E en vivo con clave |
| T3 | S5 | Inline | Planificada | Depende de T1/T2 |

Forecast inicial: aproximadamente 900–1300 líneas authored entre orquestador, cliente, interfaz, pruebas y documentación; archivos generados excluidos. Estrategia `feature-branch-chain`; el umbral de ~400 líneas es heurístico, no un límite de tamaño.

## Log

### L1 — Solicitud original (2026-10-17)

Si quiero que implementemos el asistente, he pensado de la siguiente manera y corrigeme si no debería ser así. un modelo llm gratuito, inicialmente puede ser uno de groq, no se si opencode go de esa opcion o si es el caso un modelo de hugging face o en su defecto un modelo local, que pueda servir de mi asistente, que pueda preguntarle, el modelo consulta fuentes de datos y da la respuesta.

### L2 — Decisión de proveedor (2026-10-17)

Texto exacto del usuario: “Groq (API en la nube)”. Corrección aceptada por el usuario: el modelo no consulta los datos directamente; pide herramientas y la API consulta con la sesión del usuario. Se conserva la abstracción compatible OpenAI para poder cambiar a modelo local por configuración.

### L3 — Aprobación para planificación (2026-10-17)

Texto exacto del usuario: “Aprobar SPEC-009 y ADR-0011 (recomendado)”. Se aprueban `specs/009-conversational-assistant/spec.md` y `docs/architecture/adr/ADR-0011-groq-conversational-assistant.md` para planificar; esto no autoriza cambios de código hasta autorizar tareas. Pendiente de confirmar con el proveedor el ID del modelo con soporte de herramientas y los límites vigentes del nivel gratuito.

### L4 — Implementación y verificación de T1 (2026-10-17)

Texto exacto del usuario: “Autorizar T1 (recomendado)”. Implementado por worker acotado (context backstop) dentro de las superficies acordadas; sin llamadas reales a Groq en esta tarea.

Aplicado: puerto `IAssistantLanguageModel` con contratos de chat/herramientas y excepción de proveedor con razón cerrada (NotConfigured/Unreachable/Timeout/RateLimited/InvalidResponse); `ChatOrchestrator` en Application con prompt de idioma, historial recortado, máximo de rondas y catálogo fijo de5 herramientas que delegan siempre en los casos de uso existentes (verificado por el padre: cero acceso directo a datos en la capa asistente); cliente HTTP compatible OpenAI en Infrastructure (forma de `tools`, clave solo en header, 429→RateLimited, timeout, respuesta inválida, clave vacía → NotConfigured sin llamar a la red); endpoint `POST /chat` con sesión, idioma del perfil y degradación 429/503 con razón legible por máquina; sección `AI` en appsettings (Model vacío a confirmar con el proveedor), `.env.example` y cableado Compose sin guardas `:?` (la API debe arrancar sin clave).

Evidencia: build sin incremental 0 warnings/0 errores (los10 avisos xUnit1031 previos de mi banco se eliminaron pasándolo a async/await); suite completa 63/63 (5 unit + 58 integración) con las16 pruebas nuevas: bucle con LLM falso sobre fixtures reales (datos ES/EN fluyen a la herramienta), agotamiento de rondas exacto, recorte de historial, catálogo exacto de5 con JSON Schema válido, y cliente con HttpMessageHandler falso (petición+auth header, parsing de contenido y tool_calls, 429, timeout, clave vacía sin invocar el handler, JSON malformado). `docker compose config --quiet` en verde.

Risk: item 2 (nuevo endpoint autenticado y manejo de clave de proveedor).
