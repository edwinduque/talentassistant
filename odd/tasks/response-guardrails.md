# Seguimiento ODD: guardrails de respuesta del asistente

Objetivo: que el asistente solo opere en el ámbito de RR. HH./HCM, rechace con amabilidad y redirección las peticiones ajenas (ejemplo literal: «dame una funcion python de ordenar numeros») y nunca invente datos que las herramientas no devuelven, sin regresión en las siete preguntas de referencia.
Rama: `feat/response-guardrails`; entrega: `feature-branch-chain`; runner: SDK `10.0.402`, `dotnet build`, `dotnet test`, `scripts/verify-assistant-guardrails.ps1` con la clave en `.env` y E2E de navegador ES/EN; revisión nativa desactivada (`disabled/unmanaged`).

## Specs

### S1 — Regla de ámbito en el prompt

Alcance: primera instrucción del system prompt de `ChatOrchestrator` (ámbito RR. HH./HCM, solo respuestas apoyadas en herramientas, rechazo breve con redirección en el idioma del usuario, sin código ni conocimiento general) y cláusula reforzada de «sin dato de las herramientas → información no disponible». Solo texto de instrucción: catálogo y bucle intactos.

Texto exacto del usuario: “que el sistema no alucine, que no responda cosas que no tienen que ver por el hcm: por ejemplo dame una funcion python de ordenar numeros”.

Aceptación: pruebas deterministas que pinan la regla de ámbito y la cláusula anti-alucinación; las siete preguntas de referencia siguen verdes en la suite (AC-035).

### S2 — Banco adversarial y pasada en vivo

Alcance: `scripts/verify-assistant-guardrails.ps1` con los cinco casos de SPEC-010 §6, aserciones ASCII-only (`\uXXXX` para tildes), `-PaceSeconds`/`-Only` y códigos 0/1/3; pasada en vivo con clave: sin artefactos de código, marcador de rechazo/redirección, `toolsUsed` vacío fuera de ámbito, sin cifras de salario, sin empleado fabricado.

Aceptación: AC-033 y AC-034 con evidencia del script; la suite no depende de red.

### S3 — Recorrido de navegador

Alcance: turno de rechazo en español y otro en inglés en `/assistant` con la conversación continúa operativa, consola sin errores.

Aceptación: E2E de navegador observado en ambos idiomas (AC-033).

### S4 — Documentación y cierre

Alcance: FR-031, AC-033..AC-035 con trazabilidad, sección «Qué responde y qué rechaza» en la guía, nota en el registro junto a ADR-0011 (sin ADR nuevo), estado de la visión y verificación final.

Aceptación: trazabilidad completa, guía reproducible sin secretos, suite verde y cierre con Risk.

Risk: none (el cambio es de instrucción del prompt con regresión fijada por AC-035: siete preguntas en vivo + suite completa + pins deterministas; sin datos, autenticación, concurrencia ni dependencias nuevas).

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1 | Inline | Implementada; RED→GREEN del pin de ámbito, build0/0, suite77/77 | Commit en esta entrada |
| T2 | S2, S3 | Pendiente de ruta | Planificada | Depende de T1; requiere `AI_API_KEY` (ya presente en `.env`) |
| T3 | S4 | Pendiente de ruta | Planificada | Depende de T1/T2 |

Forecast inicial: aproximadamente 300–600 líneas authored (prompt, pruebas, script, documentación); archivos generados excluidos. Estrategia `feature-branch-chain`.

## Log

### L1 — Solicitud original (2026-10-18)

si consolidemos, ademas quiero comenzar a ver los guardarieles, que el sistema no alucine, que no responda cosas que no tienen que ver por el hcm: por ejemplo dame una funcion python de ordenar numeros

### L2 — Aprobación para planificación (2026-10-18)

Texto exacto del usuario: “Aprobar SPEC-010 (recomendado)”. Se aprueba `specs/010-response-guardrails/spec.md` para planificar; esto no autoriza cambios de código hasta autorizar tareas. La consolidación previa (tests HTTP de `/chat`, commit `cb4991f`, suite 76/76) quedó cerrada en este mismo mensaje del usuario («si consolidemos»).

### L3 — Implementación y verificación de T1 (2026-10-19)

Texto exacto del usuario: “Autorizar T1 (recomendado)”. Implementado en línea (unidad pequeña): `BuildSystemPrompt` reescrito con la identidad y el ámbito RR. HH./HCM en la PRIMERA línea, la regla «solo RR. HH./HCM y solo con datos de las herramientas», la cláusula de no disponibilidad intacta y la instrucción de rechazo con redirección localizada para peticiones ajenas (programación, conocimiento general, traducciones, recetas, escritura creativa); las sentencias de desambigüación y timeoff/presence se conservaron literal para no romper los pines existentes.

Evidencia: test nuevo `SystemPromptBindsTheAssistantToTheHrmScopeAndAntiHallucinationRules` con RED observado (sub-string no encontrado: «HR/HCM» ausente del prompt previo) y GREEN tras el cambio; build0/0 y suite77/77 (5 unit +72 integración) con los pines «first call search_employees» y «ask which one» verdes. Sin llamadas a Groq en esta tarea.

Risk: none (instrucción de prompt con regresión fijada por AC-035: siete preguntas verdes en vivo en T2 y suite completa).
