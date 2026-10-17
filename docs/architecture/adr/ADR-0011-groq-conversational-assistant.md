# ADR-0011 — Asistente conversacional con proveedor Groq (API compatible OpenAI)

- **Estado:** Propuesto (2026-10-17).
- **Fecha:** 2026-10-17.
- **Aprobación de arquitectura:** pendiente de aprobación del usuario junto con SPEC-009.
- **Especificación relacionada:** SPEC-009 — Asistente conversacional con herramientas del backend.

## Contexto

SPEC-008 construyó la superficie de consultas que el asistente necesita (directorio, presencia, TimeOff, tareas e insights, todos autenticados y por idioma). La visión siempre previó preguntas en lenguaje natural; la política del proyecto exige una especificación aprobada antes de incorporar cualquier proveedor LLM.

El usuario propuso usar un modelo gratuito (Groq, Hugging Face o un modelo local) y que "el modelo consulte las fuentes de datos". Se corrigió el planteamiento: el modelo **no consulta los datos**; pide herramientas, la API consulta con la sesión del usuario y el modelo redacta la respuesta. Tras presentar las opciones (local/Ollama, Groq, Hugging Face), el usuario eligió **“Groq (API en la nube)”** por su cero instalación y su mayor calidad sin hardware.

## Decisión propuesta

- Groq es el proveedor primario, integrado con un cliente HTTP genérico **compatible con OpenAI** (`/chat/completions` con `tools`): sin SDK ni paquetes NuGet nuevos, y con la posibilidad de cambiar a un Ollama local más adelante solo cambiando `AI__BaseUrl`/`AI__ApiKey` (el modelo de Groq se confirma al implementar con la lista vigente de modelos con soporte de herramientas; la verificación documental no estuvo disponible en esta sesión).
- El bucle de herramientas vive en la API: catálogo fijo y cerrado (las capacidades de SPEC-008/002), ejecución en proceso con la sesión e idioma del usuario, máximo de rondas y longitud de respuesta; el modelo nunca accede a red ni a datos por su cuenta.
- La clave vive solo en `.env`/User Secrets; sin clave o sin red la aplicación degrada con un mensaje localizado controlado (503), sin reintentos ciegos y con errores 429 traducidos.
- La respuesta incluye la traza de herramientas para que la interfaz muestre las fuentes y para que el banco de SPEC-008 sirva de evaluación.

## Impacto en decisiones vigentes

- ADR-0003 (límite del servicio de IA): se materializa — la IA invoca contratos del backend y no toca datos.
- ADR-0007/0008: la autenticación existente da la identidad y el idioma a cada herramienta; nada cambia.
- ADR-0010: los endpoints construidos pasan a ser las herramientas del catálogo; no se duplican casos de uso.
- NFR-013/NFR-014 y FR-015 quedan satisfechos por diseño (timeouts, catálogo fijo, sin escrituras).
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

La decisión se considerará implementada cuando AC-029..AC-031 tengan evidencia observada: banco de las seis preguntas respondidas por el chat, catálogo cerrado y sin escrituras, degradación sin clave, secretos solo en `.env`, E2E en ES y EN con la clave presente y suite completa en verde. La aprobación de ADR-0011 autoriza planificar; la implementación se ejecuta por tareas.
