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

### S6 — Resolución de nombres y desambigüación (enmienda 2026-10-18)

Alcance: matcher del source insensible a acentos (SPEC-008 S1 enmendado: «Juan Perez» localiza a «Juan Pérez»), fixture «Juan Herrera» como segundo «Juan» (SPEC-008 catálogo enmendado), regla de desambigüación en el system prompt del asistente (SPEC-009 §3/§8 enmendados: varios candidatos → listar y preguntar cuál, nunca elegir en silencio), y ampliación de la pasada en vivo con la séptima pregunta ambigua.

Texto exacto del usuario: “El modelo deberia tener la capacidad de que si le escribo Juan busque los juanes y si hay mas de uno antes que nada te pregunte cual Juan”.

Aceptación: AC-026 (acentos + doble «Juan») y AC-032 (el asistente pregunta entre candidatos y responde tras la elección) con evidencia de banco determinista y pasada en vivo.

Risk: item 2 (nuevo endpoint autenticado y manejo de clave de proveedor). T1 requiere verificación independiente tras sus checks propios.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1, S2 | Inline | Implementada; build 0/0, suite 63/63 con16 pruebas del bucle/cliente sin red | Commit pendiente en `feature-branch-chain` |
| T2 | S3, S4 | Inline | Implementada; pasada en vivo observada5/6 (Q4 bloqueada por acentos → S6/T4) y UI ES/EN + degradación verificadas en navegador | Commits `ab6d2bc`, `902e25a`; evidencia final tras T4 |
| T3 | S5 | Inline | Planificada | Depende de T1/T2/T4 |
| T4 | S6 (enmienda SPEC-008 + SPEC-009) | Inline | Planificada | Matcher sin acentos, fixture Juan Herrera, regla de desambigüación y séptima pregunta en vivo; Risk item 3 (helper compartido con la asistencia) |

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

Evidencia: build sin incremental 0 warnings/0 errores (los10 avisos xUnit1031 previos de mi banco se eliminaron pasándolo a async/await); suite completa 63/63 (5 unit + 58 integración) con las15 pruebas nuevas: bucle con LLM falso sobre fixtures reales (datos ES/EN fluyen a la herramienta), agotamiento de rondas exacto, recorte de historial, catálogo exacto de5 con JSON Schema válido, y cliente con HttpMessageHandler falso (petición+auth header, parsing de contenido y tool_calls, 429, timeout, clave vacía sin invocar el handler, JSON malformado). `docker compose config --quiet` en verde.

Risk: item 2 (nuevo endpoint autenticado y manejo de clave de proveedor).

### L5 — Verificación independiente de T1 (2026-10-17)

El verificador validó los cinco claims sin bloqueos: alcance del diff exacto (18 rutas, cero PackageReference nuevos, Web/Domain intactos, sin claves reales); invariancia arquitectónica (cero acceso directo a datos en la capa asistente; catálogo exacto de5 herramientas delegando en casos de uso); endpoint sin red de proveedor observado en vivo con host propio en puerto libre: sin token → 401 (GET → 405), mensaje vacío → 400 con cuerpo exacto, sin clave → 503 {"reason":"NotConfigured"} en348 ms con la guardia comprobada ANTES de cualquier envío y /health → 200; tests sin red (solo handler falso, 0 llamadas a api.groq) y compose config en verde; suite 63/63 desde el workspace. Cero secretos emitidos; `.env` no escrito; scratch y proceso host eliminados; contenedores nunca tocados.

Advisories: (1) `AI__Provider` en appsettings es informativo hoy — el conmutador real es BaseUrl/ApiKey según ADR-0011; (2) GET /chat devuelve 405, el gate real es POST (relevante para el E2E de T2); (3) se propone un test HTTP del endpoint (TestServer) que queda diferido como los tests 401 anteriores.

Risk: item 2 (endpoint autenticado y clave de proveedor) — verificado sin bloqueos.

### L6 — T2: pasada en vivo, defecto de esquemas nulos y enmienda de nombres (2026-10-18)

Texto exacto del usuario (aprobó T2): “Autorizar T2 con lo anterior (recomendado)”. Texto exacto del usuario sobre el bloqueo de Q4: “El modelo deberia tener la capacidad de que si le escribo Juan busque los juanes y si hay mas de uno antes que nada te pregunte cual Juan”.

Evidencia de T2: página `/assistant` con enlace de navegación, estado vacío, historial y mensajes de fallo localizados (verificados en navegador ES con consola a0); degradación 503 NotConfigured localizada observada antes de la clave; script `scripts/verify-assistant-questions.ps1` con login desde .env, aserciones de hechos del fixture (Q1..Q6) y salida ASCII-proof; modelo `openai/gpt-oss-120b` confirmado con la API de Groq (los Llama no disponibles en el plan del usuario); build0/0 y suite65/65 con los dos tests nuevos.

Defecto encontrado y corregido en vivo: Groq valida los argumentos de tool contra nuestro JSON Schema y rechazaba con HTTP400 tool_use_failed cuando el modelo expresaba el valor por defecto como null (`presence {"date":null}`); fix: los cinco esquemas aceptan ["string","null"] (el ejecutor ya normalizaba null→ausente), con test RED→GREEN (`EveryParameterAcceptsNullBecauseTheProviderValidatesArgumentsAgainstTheSchema`) y pin de comportamiento (`NullPresenceDateIsNormalizedToTodayInsteadOfAFailedTool`). La verificación de red fue necesaria: hubo un404 por modelo inaccesible y un401 por una línea AI_API_KEY corrupta en .env (mi apéndice de AI_MODEL sin salto de línea previo la pegó a la clave; reparada y validada con sha y /models200).

Bloqueo de Q4: la pregunta literal “Juan Perez” sin acento no supera el matcher contains/OrdinalIgnoreCase (SPEC-008 lo especificaba así) → vacío → “No hay información disponible” (falla la aserción de hechos /\b3\b/). Decisión del usuario (más ambiciosa que la enmienda técnica propuesta): buscar devuelve todos los coincidentes y, si hay varios para una persona concreta, el asistente pregunta cuál antes de responder. Se redactó la enmienda: SPEC-008 (fixtures +Juan Herrera, matcher sin acentos, AC-026 ampliado) y SPEC-009 (principio y diseño de desambigüación, flujo §4, AC-032, encabezados aprobados/AC-032), más S6 y T4 en este plan. Pendiente de aprobación de la enmienda.

### L7 — Evidencia de T2/T4: siete preguntas verdes y turno de elección (2026-10-18)

Pasada en vivo completa (`scripts/verify-assistant-questions.ps1 -PaceSeconds 9`) con `LIVE_EXIT=0`: las seis preguntas de referencia más la séptima ambigua responden con los hechos del fixture. Q4 («Juan Perez» sin acento) → search_employees + timeoff → «3 días laborables (del 13 al15 de octubre2026)». Q5 coherente (search_employees + timeoff → «20 de octubre de2026»). Q7 → `search_employees` primero, lista «Juan Pérez – Oficina: Madrid» y «Juan Herrera – Oficina: Tokio» y pregunta «¿A cuál de ellos te refieres?» (AC-032 en vivo).

Dos defectos observados y corregidos durante la pasada, además del400 por esquemas nulos registrado en L6: (1) presencia de Pedro Rojas decía «trabajando» hoy mientras su timeoff aprobado (15→20 oct) lo cubre — Q5 cambió de respuesta entre pasadas; filas de presencia de hoy y mañana corregidas a `VacationState` y banco/tests reconciliados (Q1 y SyntheticWorkforceSourceTests ahora exigen que NO figura como trabajando). (2) Q7 no preguntaba: el modelo iba directo a `timeoff(name="Juan")` y la resolución interna de ids ocultaba la ambigüedad — el prompt ahora exige `search_employees` PRIMERO en preguntas de persona (pin de test `first call search_employees`) y guía timeoff/presence según el tipo de pregunta.

E2E de navegador: sesión con turno de elección en español («Dime cuantos dias de vacaciones tiene Juan.» → candidatos + «¿A cuál?» → «El de Pérez, por favor.» → «3 días laborables (del13 al15 de octubre2026)» con Fuentes: timeoff); perfil a inglés y pregunta en inglés → «Juan Pérez has **3 vacation days** approved.» con etiqueta Sources; perfil restaurado a español y logout; consola con0 errores en todo el recorrido. Suite final: build0/0 y68/68 (5 unit +63 integración). La ventana de tasa de Groq exige ritmo (`-PaceSeconds`) o reintentos: el429 se maneja con código3 y mensaje específico.

### L8 — Verificación independiente de T2/T4 (2026-10-18)

El verificador validó los cinco claims sin bloqueos sobre `ef9750e..d1ce2e4`: alcance exacto de20 rutas con0 PackageReference y0 secretos; riesgo item3 observado en vivo — el matcher doblado solo en los dos predicados de nombre, comportamiento previo preservado (`name=cardenas/rojas/...` y `name=zzz`=0, oficina exacta sin plegado: `office-demo-tókyo`=0), conducta nueva verificada (`name=Perez`=Juan Pérez; `/timeoff?name=Juan Perez` y `/tasks?name=Juan Perez` resuelven el empleado); invariante de fixtures (Pedro=`vacation` hoy, Juans=`working`, payload español correcto); sin llamadas a `/chat` ni Groq; prompt/fixture/script correctos y script ASCII0; suite68/68, build0/0, compose config en verde; workspace idéntico antes/después y scratch eliminado.

Advisory honestado: la página de asistencia no llamaba a `GetEmployeesAsync` ni en el baseline (filtra por fechas) — la superficie compartida real es el endpoint `/employees?name=` + la resolución interna de timeoff/tasks, que fue la sondeada; el riesgo item3 se materializa ahí y quedó verificado. Sus dos sondas se incorporaron como tests de regresión en `SyntheticWorkforceSourceTests` (`OfficeIdMatchingStaysExactAndUnfolded`, `AccentFoldingFlowsThroughTimeOffAndTasksByIdResolution`).
