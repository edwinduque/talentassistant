# Feature: balanceo y continuidad entre proveedores de IA

Objetivo: añadir Gemini 3.8 Flash como segundo proveedor (sustituto obligado de 2.5 Flash, retirado para usuarios nuevos) y enrutar las conversaciones entre Groq y Gemini con asignación pegajosa, failover y cortacircuitos, sin cambiar la experiencia ni agotar cuotas en pruebas.
Rama: `feat/ai-provider-balancing`.
Entrega: un solo PR con commits por work unit (preferencia del usuario, 2026-10-25); push y PR los decide el usuario.
Runner: `dotnet test` (UnitTests e IntegrationTests); la integración usa PostgreSQL local (`docker compose`). Sin llamadas reales a Groq o Gemini.

## Specs

S1. Dos proveedores configurables (Groq existente y Gemini 3.8 Flash) detrás de `IAssistantLanguageModel`; el dominio y `ChatOrchestrator` no conocen proveedores.

S2. La unidad de reparto es la conversación: un `conversationId` opcional en `POST /chat` elige proveedor de forma determinista y ponderada; la misma conversación conserva proveedor mientras esté disponible y no cambia entre turnos.

S3. La Web genera el `conversationId` al abrir `/assistant` y lo renueva con «Nuevo chat»; los clientes sin el campo siguen funcionando.

S4. Failover por completion ante cualquier fallo del proveedor preferido, reutilizando el transcript y sin repetir herramientas; el resto del turno permanece en el proveedor alternativo.

S5. En modo enrutador se hace como máximo un intento HTTP por proveedor y completion; en modo proveedor único se conserva el reintento actual de 5xx/`tool_use_failed`.

S6. Cortacircuitos en memoria por proveedor: tras dos fallos consecutivos queda fuera del reparto durante 120 s (configurables); una sonda decide su vuelta. Si ambos están bloqueados, se devuelve el error controlado sin llamadas.

S7. Si ambos proveedores fallan, se conservan los mensajes localizados y los códigos HTTP actuales; `RateLimited` si ambos fueron limitados.

S8. Consistencia: mismo system prompt, catálogo, idioma, formato de SPEC-021 y `max_tokens`; Groq con temperatura 0.1, Gemini 3.x sin `temperature` (deprecado) y `reasoning_effort=low` configurable. La respuesta no menciona el proveedor.

S9. Configuración compatible: Groq conserva `AI:BaseUrl/ApiKey/Model`; se añaden `AI:Gemini:*` y `AI:Routing:*`; `AI:Provider` acepta `router` (por defecto), `groq` o `gemini`. Sin clave de Gemini el sistema opera con Groq; sin ninguna clave mantiene `NotConfigured`.

S10. Adaptación de esquema de herramientas por proveedor cuando sea necesaria (uniones `["string","null"]` para Gemini), sin cambiar el esquema canónico de Groq.

S11. Seguridad y cuotas: claves solo en configuración externa; logs con proveedor, modelo, resultado, latencia y failover, nunca prompt/respuesta/claves; suite y CI sin llamadas reales; scripts en vivo opt-in con `-MaxCalls 4` y `-PaceSeconds 15`.

S12. Verificación: pruebas deterministas de configuración, selección, failover, cortacircuitos, payloads y contrato; build sin errores ni avisos; suite completa; documentación (SPEC-022, ADR-0015, NFR-024, AC-062..AC-065) actualizada.

## Tasks

- T1 — Contratos, configuración de proveedores, enrutador, salud/cortacircuitos, cliente (intentos, `reasoning_effort`, normalizador) y pruebas deterministas (S1, S2, S4–S10). Ruta: inline. Estado: completo; commit `a4be5db`.
- T2 — Integración API/Web: `conversationId`, renovación con «Nuevo chat», payload y pruebas (S2, S3, S9). Ruta: inline. Estado: completo; commit `78c4de2`.
- T3 — Documentación y verificación final: SPEC-022, ADR-0015 aceptado, NFR-024, AC-062..AC-065, README, visión, guía local, entorno y suite (S11, S12). Ruta: inline. Estado: completo; cierre documental.

## Log

L1 (2026-10-25, verbatim): «Nuevo spec, debido a que la capa gratuita de groq es bastante limitada quiero agregar una nueva api de gemini con el modelo 2.5 flash. quiero que implementemos un sistema en el cual podamos tener como una specie de balanceador en el cual algunas peticiones se vayan por uno o por otro modelo o si es el caso que intente con un modelo y si falla lo haga con el otro. Cuando hagas pruebas no acabes con los tokens del día porque precisamente eso me pasó con groq. elabora la mejor estrategia y tambien quiero que las respuestas no sean tan diferentes entre modelos»

L2 (2026-10-25): el usuario aprobó SPEC-022 y ADR-0015 con los pesos 50/50, failover total y presupuesto de pruebas de 4 llamadas.

L3 (2026-10-25, propuesta): SPEC-022 y ADR-0015 quedaron en `spec/022-ai-provider-balancing`, commit `4bf7be3`; no se hicieron llamadas reales. La rama de implementación continúa desde ese commit.

L4 (2026-10-25, fuentes verificadas): Gemini 2.5 Flash expone `https://generativelanguage.googleapis.com/v1beta/openai/` con Bearer y `tools`; `reasoning_effort=low` equivale a 1.024 de pensamiento y `none` desactiva el pensamiento en 2.5; la cuota gratuita es por proyecto y solo se consulta en AI Studio; la capa gratuita puede usar prompts para mejorar sus productos, así que solo se envían datos sintéticos.

L5 (2026-10-25, implementación): el enrutador scoped selecciona por hash SHA-256 del `conversationId` y pesos, conserva el proveedor durante el turno, reintenta la completion fallida en el otro con el mismo transcript y registra salud por proveedor con enfriamiento. El cliente compatible OpenAI acepta un presupuesto de intentos por proveedor, `reasoning_effort` opcional y normalización de esquema; Gemini reduce uniones al primer tipo no nulo, añade `items` a arrays sin elementos y elimina `additionalProperties`. La configuración conserva las claves de Groq y añade `AI:Gemini:*`/`AI:Routing:*`; `AI:Provider` acepta `router|groq|gemini`. La Web renueva el `conversationId` con «Nuevo chat».

L6 (2026-10-25, verificación): build 0 errores/0 avisos; 261/261 pruebas de integración en verde, incluidas configuración, selección, failover, cortacircuitos, payloads, normalizador y el identificador de extremo a extremo; UnitTests 23/24 por el bloqueo ambiental de App Control ya conocido (`0x800711C7` al cargar `TalentAssistant.Domain.dll`). El script `verify-ai-providers.ps1` se validó con `-DryRun`; no se ejecutaron llamadas reales a Groq ni Gemini.

L7 (2026-10-25, entrega): la rama `feat/ai-provider-balancing` está apilada sobre `feat/assistant-chat-refresh` porque depende de SPEC-021 y del botón «Nuevo chat»; el PR del chat debe integrarse primero. El PR de esta feature incluye el commit de propuesta y cuatro commits de implementación/cierre (T1, T2, documentación y observabilidad), sin push ni PR todavía.

L8 (2026-10-25, pendiente): la pasada en vivo con presupuesto de cuatro llamadas confirmará que Gemini acepta el transcript con `tool_calls` de Groq y el esquema normalizado; hasta entonces esa compatibilidad es un riesgo documentado y el fallo se degrada con el error controlado.

L9 (2026-10-25, observabilidad): cada completion registra proveedor, modelo y latencia; los fallos añaden el motivo y el failover. Los logs no incluyen prompt, respuesta, claves ni datos de personas.

L10 (2026-10-26, producción): Gemini devolvía 404 `NOT_FOUND` en todas las llamadas: `gemini-2.5-flash` ya no está disponible para usuarios nuevos y el proveedor recomienda `gemini-3.8-flash`. El failover a Groq funcionó, pero al estar Groq limitado la API devolvía 429. Se actualiza el modelo por defecto a 3.8 Flash y se omite `temperature` en Gemini 3.x; Groq conserva 0.1. SPEC-022 §11 y ADR-0015 recogen la enmienda.

L11 (2026-10-26, verificación del arreglo): build 0/0 y pruebas focalizadas 34/34; se añadió la prueba de que Gemini no envía `temperature` y Groq sí. La suite completa y el despliegue quedan como cierre de esta corrección.

L12 (2026-10-26, timeout de la Web): con Gemini 3.8 la Web abortaba a los 5 s y mostraba «No se pudo contactar con el servicio de IA» mientras la API seguía procesando el turno. El cliente `BackendApiClient` pasa a 120 s para el chat; `AuthApiClient` conserva 5 s. Se añadió `BackendApiClientTimeoutTests` para impedir que vuelva a bajarse por debajo de 100 s.

L13 (2026-10-26, transcript entre proveedores): producción mostró Gemini 503, failover a Groq correcto y, tras una llamada de herramienta de Groq, Groq 429 seguido de Gemini 400. El cuerpo del 400 reveló que Gemini 3 exige `tool_calls[].extra_content.google.thought_signature`; el cliente ahora la conserva al parsear y la reenvía al serializar, incluidas llamadas paralelas y secuenciales. El enrutador reintenta una vez con transcript limpio cuando el alternativo recibe tool_calls de otro proveedor sin firma. Pruebas nuevas: persistencia de la firma, reenvío en el payload, reintento limpio y no reintento sin herramientas.

L14 (2026-10-26, verificación): build 0/0; focalizadas 31/31 (router, cliente y orquestador); integración local 264/265 por el volumen HCM caducado, CI con contenedor fresco en verde. No se ejecutaron llamadas reales.

L15 (2026-10-26, timeout de Gemini): la primera llamada a Gemini 3.8 agotó los 30 s. El tiempo por intento sube a 60 s (configurable) y la Web mantiene 120 s para el turno completo; el firmado de pensamiento evita el 400 en las rondas siguientes.
