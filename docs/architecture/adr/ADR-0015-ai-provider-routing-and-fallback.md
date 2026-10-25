# ADR-0015 — Enrutamiento y continuidad entre proveedores de IA

- **Estado:** Propuesto (2026-10-25); pendiente de aprobación humana.
- **Fecha:** 2026-10-25.
- **Especificación relacionada:** [SPEC-022 — Balanceo y continuidad entre proveedores de IA](../../../specs/022-ai-provider-balancing/spec.md).
- **Decisiones relacionadas:** amplía ADR-0011 sin sustituirlo; conserva ADR-0003 (frontera del servicio de IA) y NFR-021 (claves y degradación).

## Contexto

El asistente usa un único proveedor, Groq, a través de `OpenAiCompatibleLanguageModel`. Su capa gratuita es limitada y ya se agotó en una jornada: cuando el proveedor devuelve 429, `POST /chat` responde 429 y el chat queda sin alternativa hasta que la cuota se recupere. El usuario pidió añadir Gemini 2.5 Flash y repartir las peticiones entre ambos proveedores, con reintento en el otro cuando uno falla.

Gemini 2.5 Flash expone un endpoint compatible con OpenAI (`https://generativelanguage.googleapis.com/v1beta/openai/`) con autenticación Bearer y soporte de `tools`, por lo que no requiere SDK ni un segundo formato de request. Su capa gratuita también es finita y su cuota solo se consulta en AI Studio. La diferencia de comportamiento entre modelos es un riesgo de experiencia: si se alternan por turno, una misma conversación cambia de estilo y de criterio.

## Decisión propuesta

Adoptar un enrutador en Infrastructure que implemente `IAssistantLanguageModel` sobre dos instancias del cliente compatible con OpenAI (Groq y Gemini). El enrutador:

1. Reparte por **conversación**, no por llamada. Un `conversationId` opcional generado por la Web se transforma en un hash determinista y elige proveedor según pesos configurables. La misma conversación conserva proveedor mientras esté disponible; conversaciones distintas se reparten.
2. Hace **failover por completion** ante cualquier fallo del proveedor preferido, reutilizando el transcript ya construido (incluidos los resultados de herramientas). No repite herramientas ni la pregunta y mantiene el proveedor alternativo durante el resto del turno.
3. Limita los intentos a **uno por proveedor y completion** en modo enrutador, para no gastar cuota dos veces en el mismo proveedor. En modo proveedor único se conserva el reintento actual de 5xx/`tool_use_failed`.
4. Mantiene un **cortacircuitos en memoria**: tras dos fallos consecutivos, el proveedor queda fuera del reparto durante un enfriamiento configurable (120 s) y una sonda decide su vuelta. No hay estado distribuido ni persistido.
5. Comparte **prompt, catálogo, idioma, formato y parámetros** entre proveedores. Gemini usa `reasoning_effort=low` para acercar latencia y verbosidad; temperatura 0.1 y `max_tokens` son comunes.
6. Conserva la **configuración actual de Groq** (`AI:BaseUrl`, `AI:ApiKey`, `AI:Model`) y añade `AI:Gemini:*` y `AI:Routing:*` con valores por defecto. `AI:Provider` acepta `router` (por defecto), `groq` o `gemini`.

El dominio no conoce proveedores. `ChatOrchestrator` sigue dependiendo del puerto; el enrutador y los clientes viven en Infrastructure.

## Invariantes

1. **Sin escrituras nuevas:** el failover solo reutiliza lecturas y resultados ya calculados; no existen herramientas de escritura.
2. **Transparencia para el usuario:** una sola respuesta por turno; los mensajes localizados y los códigos HTTP actuales se conservan cuando ambos proveedores fallan.
3. **Compatibilidad:** un despliegue con solo la clave de Groq sigue funcionando; sin ninguna clave se mantiene `NotConfigured`.
4. **Cuotas:** la suite y el CI no hacen llamadas reales; los scripts en vivo son opt-in, con tope de llamadas y ritmo.
5. **Secretos y contenido:** claves solo en configuración externa; los logs registran proveedor, modelo, resultado, latencia y failover, nunca prompts, respuestas, claves ni datos de personas.
6. **Consistencia:** mismo prompt, catálogo y reglas de formato de SPEC-021; la respuesta no menciona el proveedor.
7. **Datos:** Gemini recibe solo datos sintéticos hasta que exista una decisión de política para datos reales; la capa gratuita puede usar los prompts para mejorar sus productos.

## Cambios a decisiones vigentes

- **ADR-0011:** se mantiene el proveedor Groq y el catálogo cerrado; se añade Gemini como segundo proveedor intercambiable y un enrutador. No se cambia la política de egreso de datos, que sigue pendiente para HCM real.
- **NFR-021:** se amplía con continuidad entre proveedores y verificación sin llamadas reales (propuesto como NFR-024).
- **SPEC-009/SPEC-021:** el contrato de `POST /chat` gana un campo opcional `conversationId`; el formato de respuesta y la UI no cambian.

## Consecuencias

### Positivas

- El chat sigue operativo cuando un proveedor agota cuota, falla o está mal configurado.
- La carga se reparte entre dos capas gratuitas sin que el usuario cambie de modelo a mitad de conversación.
- El mismo puerto y el mismo formato de request evitan SDKs y permiten pruebas deterministas con transportes falsos.

### Riesgos y costos

- Dos proveedores implican dos procesadores de datos y dos superficies de configuración; la política de egreso debe revisarse antes de datos reales.
- Los esquemas de herramientas pueden no ser idénticos; hace falta un adaptador probado y una verificación de paridad.
- El cortacircuitos es por proceso; en un despliegue multi-instancia cada instancia decide por su cuenta.
- El failover añade una llamada adicional en el peor caso; el presupuesto de intentos lo acota.
- La asignación por hash cambia si cambian los pesos; es aceptable porque no hay persistencia.
- El proveedor alternativo podría rechazar un transcript con `tool_calls` generados por el otro; se verifica con transporte falso y una llamada en vivo opt-in, y un rechazo termina en el error controlado sin reintentos ilimitados.

## Alternativas consideradas

- **Sustituir Groq por Gemini:** simple, pero elimina la redundancia y mantiene un único punto de fallo y una única cuota.
- **Round-robin por llamada HTTP:** reparte mejor la cuota, pero mezcla modelos dentro de una misma respuesta y puede invalidar el estado de herramientas; además cambia el estilo entre turnos.
- **Groq primario y Gemini solo como respaldo:** protege la disponibilidad pero no reparte la cuota; el primario seguiría agotándose y el respaldo cargaría con todo.
- **Gateway/proxy externo con estado distribuido:** añade infraestructura y operación para un monolito local; no se justifica.
- **Enrutador en la Web o decisión del cliente:** expondría la lógica y las claves al cliente y rompería la frontera de ADR-0003.

## Verificación requerida para declarar implementada la decisión

1. Pruebas deterministas del selector: misma conversación → mismo proveedor; pesos → reparto; sin estado persistido.
2. Pruebas de failover: 429, 5xx, timeout, red y respuesta inválida; transcript reutilizado; sin repetición de herramientas; doble fallo con mensaje localizado.
3. Pruebas de cortacircuitos y recuperación tras el enfriamiento.
4. Pruebas de payload por proveedor: prompt, catálogo, idioma, temperatura, `max_tokens`, `reasoning_effort` y autenticación Bearer; adaptador de esquema.
5. Pruebas de configuración: solo Groq, solo Gemini, ambos, ninguno, pesos inválidos.
6. Suite completa sin llamadas reales y build sin avisos.
7. Pasada de paridad opcional con tres preguntas y presupuesto de llamadas, solo con autorización explícita; el resultado se documenta con hechos y estructura, no con texto literal.
8. Actualización de NFR-024/AC-062..AC-065 y de la documentación operativa al aprobarse.

## Estado

Propuesto el 2026-10-25. No se ha modificado código, configuración ni catálogos, y no se ha realizado ninguna llamada a Groq o Gemini. La implementación requiere la aprobación explícita del usuario.
