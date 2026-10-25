# ADR-0011 — Asistente conversacional con proveedor Groq (API compatible OpenAI)

- **Estado:** Aceptado (2026-10-17); enmendado por ADR-0014 (2026-10-25).
- **Fecha:** 2026-10-17.
- **Aprobación de arquitectura:** aprobado por el usuario junto con SPEC-009 (2026-10-17).
- **Especificación relacionada:** SPEC-009 — Asistente conversacional con herramientas del backend.

## Contexto

SPEC-008 construyó la superficie de consultas que el asistente necesita (directorio, presencia, TimeOff, tareas e insights, todos autenticados y por idioma). La visión siempre previó preguntas en lenguaje natural; la política del proyecto exige una especificación aprobada antes de incorporar cualquier proveedor LLM.

El usuario propuso usar un modelo gratuito (Groq, Hugging Face o un modelo local) y que "el modelo consulte las fuentes de datos". Se corrigió el planteamiento: el modelo **no consulta los datos**; pide herramientas, la API consulta con la sesión del usuario y el modelo redacta la respuesta. Tras presentar las opciones (local/Ollama, Groq, Hugging Face), el usuario eligió **“Groq (API en la nube)”** por su cero instalación y su mayor calidad sin hardware.

## Decisión aceptada (enmendada por ADR-0014)

- Groq es el proveedor primario, integrado con un cliente HTTP genérico **compatible con OpenAI** (`/chat/completions` con `tools`): sin SDK ni paquetes NuGet nuevos, y con la posibilidad de cambiar a un Ollama local más adelante solo cambiando `AI__BaseUrl`/`AI__ApiKey` (el modelo de Groq se confirma al implementar con la lista vigente de modelos con soporte de herramientas; la verificación documental no estuvo disponible en esta sesión).
- El bucle de herramientas vive en la API: catálogo cerrado que incluye `query_hr_data` y herramientas de dominio, ejecución en proceso con la sesión e idioma del usuario, máximo de rondas y longitud de respuesta; el modelo nunca accede a red ni a datos por su cuenta. ADR-0014 define la excepción de consulta dinámica validada.
- La clave vive solo en `.env`/User Secrets; sin clave o sin red la aplicación degrada con un mensaje localizado controlado (503), sin reintentos ciegos y con errores 429 traducidos.
- La respuesta incluye la traza de herramientas para que la interfaz muestre las fuentes y para que el banco de SPEC-008 sirva de evaluación.

## Impacto en decisiones vigentes

- ADR-0003 (límite del servicio de IA): se materializa — la IA invoca contratos del backend y no toca datos.
- ADR-0007/0008: la autenticación existente da la identidad y el idioma a cada herramienta; nada cambia.
- ADR-0010: los endpoints construidos pasan a ser las herramientas del catálogo; no se duplican casos de uso.
- NFR-013/NFR-014 y FR-015 quedan satisfechos por diseño (timeouts, catálogo cerrado y sin escrituras).
- La visión/documentación de estado que indicaba "el chat espera especificación y proveedor" queda actualizada al aceptarse este ADR.

## Consecuencias esperadas

### Positivas

- El usuario conversa en lenguaje natural y obtiene respuestas verificables sobre datos reales de su aplicación, reutilizando todo lo construido.
- Sin instalación ni hardware; la elección de proveedor queda reversiblemente encapsulada en configuración.

### Compromisos

- **Los datos de la conversación salen de tu equipo** hacia el proveedor. Hoy son consultas sobre datos sintéticos; con un HCM real esta salida requerirá una decisión de política explícita (el ADR del proveedor real la tendrá que reabrir).
- Dependencia de internet y del nivel gratuito: límites de tasa y disponibilidad del proveedor afectan al chat (degradación controlada prevista); verificar los límites vigentes el día de elegir modelo, no se pudo confirmar en esta sesión.
- Clave externa que gestionar como secreto; su fuga permitiría consumir la cuota en tu nombre.
- Respuesta no streaming en v1: el usuario espera con indicador de carga.

## Alternativas consideradas

- **Modelo local (Ollama):** gratis, sin salida de datos y sin límites, alineado al 100% con las reglas del proyecto; descartado por elección explícita del usuario (prioridad a calidad sin hardware), pero viable después por la misma abstracción.
- **Hugging Face Inference:** también en la nube, con free tier menos predecible por modelo; sin ventaja frente a Groq para este caso.
- **Modelo local primero con Groq después:** la opción recomendada técnicamente; se descarta por preferencia del usuario, y la abstracción conserva esa ruta.
- **SDK oficial del proveedor:** añade paquetes y acopla el código; se descarta por el cliente HTTP compatible OpenAI.

## Verificación de la decisión

La decisión original se consideró implementada al verificar AC-029..AC-031. La capacidad adicional de consultas dinámicas, validación de planes y exclusión sensible se verifica mediante AC-055..AC-059 de SPEC-020 y ADR-0014.

## Actualizaciones (2026-10-18)

- El modelo se resolvió a `openai/gpt-oss-120b`, confirmado contra la API de Groq con soporte de herramientas; los modelos Llama no estaban disponibles en el plan del usuario (HTTP 404 `model_not_found`).
- El gateway del proveedor valida los argumentos de herramienta contra el JSON Schema; los cinco esquemas del catálogo aceptan `["string","null"]` y el ejecutor normaliza `null` a argumento ausente.
- Enmienda de resolución de nombres: la búsqueda de personas dobla acentos («Juan Perez» localiza a «Juan Pérez») y las preguntas de persona exigen `search_employees` primero; con varios candidatos el asistente los lista y pregunta cuál antes de responder, nunca elige en silencio (AC-032).
- Evidencia de implementación: pasada en vivo de las siete preguntas con salida 0 (`scripts/verify-assistant-questions.ps1 -PaceSeconds 9`), recorrido de navegador ES/EN con turno de elección, verificación independiente con los cinco claims MET, suite completa 70/70 y build 0 errores/0 avisos; commits `4de7672`, `d1ce2e4` y `fb71938`.
