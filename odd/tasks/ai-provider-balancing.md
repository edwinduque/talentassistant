# Feature: balanceo y continuidad entre proveedores de IA

Objetivo: añadir Gemini 2.5 Flash como segundo proveedor y enrutar las conversaciones entre Groq y Gemini con asignación pegajosa, failover y cortacircuitos, sin cambiar la experiencia ni agotar cuotas en pruebas.
Rama: `feat/ai-provider-balancing`.
Entrega: un solo PR con commits por work unit (preferencia del usuario, 2026-10-25); push y PR los decide el usuario.
Runner: `dotnet test` (UnitTests e IntegrationTests); la integración usa PostgreSQL local (`docker compose`). Sin llamadas reales a Groq o Gemini.

## Specs

S1. Dos proveedores configurables (Groq existente y Gemini 2.5 Flash) detrás de `IAssistantLanguageModel`; el dominio y `ChatOrchestrator` no conocen proveedores.

S2. La unidad de reparto es la conversación: un `conversationId` opcional en `POST /chat` elige proveedor de forma determinista y ponderada; la misma conversación conserva proveedor mientras esté disponible y no cambia entre turnos.

S3. La Web genera el `conversationId` al abrir `/assistant` y lo renueva con «Nuevo chat»; los clientes sin el campo siguen funcionando.

S4. Failover por completion ante cualquier fallo del proveedor preferido, reutilizando el transcript y sin repetir herramientas; el resto del turno permanece en el proveedor alternativo.

S5. En modo enrutador se hace como máximo un intento HTTP por proveedor y completion; en modo proveedor único se conserva el reintento actual de 5xx/`tool_use_failed`.

S6. Cortacircuitos en memoria por proveedor: tras dos fallos consecutivos queda fuera del reparto durante 120 s (configurables); una sonda decide su vuelta. Si ambos están bloqueados, se devuelve el error controlado sin llamadas.

S7. Si ambos proveedores fallan, se conservan los mensajes localizados y los códigos HTTP actuales; `RateLimited` si ambos fueron limitados.

S8. Consistencia: mismo system prompt, catálogo, idioma, formato de SPEC-021, temperatura 0.1 y `max_tokens`; Gemini con `reasoning_effort=low` configurable. La respuesta no menciona el proveedor.

S9. Configuración compatible: Groq conserva `AI:BaseUrl/ApiKey/Model`; se añaden `AI:Gemini:*` y `AI:Routing:*`; `AI:Provider` acepta `router` (por defecto), `groq` o `gemini`. Sin clave de Gemini el sistema opera con Groq; sin ninguna clave mantiene `NotConfigured`.

S10. Adaptación de esquema de herramientas por proveedor cuando sea necesaria (uniones `["string","null"]` para Gemini), sin cambiar el esquema canónico de Groq.

S11. Seguridad y cuotas: claves solo en configuración externa; logs con proveedor, modelo, resultado, latencia y failover, nunca prompt/respuesta/claves; suite y CI sin llamadas reales; scripts en vivo opt-in con `-MaxCalls 4` y `-PaceSeconds 15`.

S12. Verificación: pruebas deterministas de configuración, selección, failover, cortacircuitos, payloads y contrato; build sin errores ni avisos; suite completa; documentación (SPEC-022, ADR-0015, NFR-024, AC-062..AC-065) actualizada.

## Tasks

- T1 — Contratos, configuración de proveedores, enrutador, salud/cortacircuitos, cliente (intentos, `reasoning_effort`, normalizador) y pruebas deterministas (S1, S2, S4–S10). Ruta: inline.
- T2 — Integración API/Web: `conversationId`, renovación con «Nuevo chat», payload y pruebas (S2, S3, S9). Ruta: inline.
- T3 — Documentación y verificación final: SPEC-022, ADR-0015 aceptado, NFR-024, AC-062..AC-065, README, visión, guía local, entorno y suite (S11, S12). Ruta: inline.

## Log

L1 (2026-10-25, verbatim): «Nuevo spec, debido a que la capa gratuita de groq es bastante limitada quiero agregar una nueva api de gemini con el modelo 2.5 flash. quiero que implementemos un sistema en el cual podamos tener como una specie de balanceador en el cual algunas peticiones se vayan por uno o por otro modelo o si es el caso que intente con un modelo y si falla lo haga con el otro. Cuando hagas pruebas no acabes con los tokens del día porque precisamente eso me pasó con groq. elabora la mejor estrategia y tambien quiero que las respuestas no sean tan diferentes entre modelos»

L2 (2026-10-25): el usuario aprobó SPEC-022 y ADR-0015 con los pesos 50/50, failover total y presupuesto de pruebas de 4 llamadas.

L3 (2026-10-25, propuesta): SPEC-022 y ADR-0015 quedaron en `spec/022-ai-provider-balancing`, commit `4bf7be3`; no se hicieron llamadas reales. La rama de implementación continúa desde ese commit.

L4 (2026-10-25, fuentes verificadas): Gemini 2.5 Flash expone `https://generativelanguage.googleapis.com/v1beta/openai/` con Bearer y `tools`; `reasoning_effort=low` equivale a 1.024 de pensamiento y `none` desactiva el pensamiento en 2.5; la cuota gratuita es por proyecto y solo se consulta en AI Studio; la capa gratuita puede usar prompts para mejorar sus productos, así que solo se envían datos sintéticos.
