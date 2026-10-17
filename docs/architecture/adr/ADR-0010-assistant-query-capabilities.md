# ADR-0010 — Capacidades de consulta del asistente (previas a la IA)

- **Estado:** Propuesto (2026-10-17).
- **Fecha:** 2026-10-17.
- **Aprobación de arquitectura:** pendiente de aprobación del usuario junto con SPEC-008.
- **Especificación relacionada:** SPEC-008 — Capacidades de consulta del asistente y su corpus de preguntas.

## Contexto

El análisis de realineamiento detectó que el proyecto conserva una arquitectura correcta pero ha invertido sobre plataforma (contenedores, autenticación, localización) mientras el producto se alejaba de su norte: un asistente que responda preguntas en lenguaje natural a responsables de RR. HH. Las seis preguntas literales del usuario no pueden responderse hoy: el contrato expone únicamente incidencias de asistencia por rango de fechas, sin directorio de empleados, presencia, TimeOff consultable por empleado ni tareas.

La política del proyecto prohíbe incorporar un LLM sin especificación aprobada y exige datos verificables antes que respuestas generativas; además, un modelo sin herramientas solo podría inventar respuestas. Por eso, antes del chat, el proyecto necesita la superficie de consultas que el asistente llamará y un banco de preguntas que mida si se responde bien.

## Decisión propuesta

- Crear un segundo puerto en Application, `IHcmWorkforceSource`, para las capacidades de consulta (directorio con búsqueda por nombre, presencia por fecha, TimeOff por empleado y tareas por empleado), sin tocar el puerto de asistencia existente.
- Mantener la regla de propiedad del HCM: los estados de presencia, los tipos y estados de TimeOff y los estados de tareas son valores informados por la fuente; Talent Assistant no los deriva, no calcula duraciones ni calendarios.
- Exponer las capacidades como endpoints de API protegidos por sesión; serán las herramientas autorizadas que el asistente invocará, con la autorización siempre en el backend (ADR-0003, FR-013).
- Ampliar el catálogo sintético bilingüe con los datos de las nuevas capacidades, incluidos nombres de demostración ficticios que hacen literales las preguntas del usuario.
- Convertir las seis preguntas del usuario en escenarios automatizados de API: banco de evaluación determinista que funcionará igual antes y después de incorporar IA.
- Podar la interfaz de revisión: retirar el campo manual de id de empleado y añadir un panel de «Presencia de hoy» en el home.
- Diferir el chat, el proveedor de IA y los agentes a una especificación posterior con su propio ADR.

## Impacto en decisiones vigentes

- ADR-0001/0005: la modularidad y los casos de uso explícitos se mantienen; las capacidades son casos de uso más detrás de contratos.
- ADR-0002/0006: los fixtures crecen en el mismo dataset sintético bilingüe, sin datos reales ni nuevos servicios.
- ADR-0003: se refuerza — la futura IA solo podrá invocar estas capacidades del backend.
- ADR-0007/0008/0009: sin cambios; la sesión y la localización sirven a las capacidades nuevas.
- FR-004 pasa de diferido a activo dentro de este alcance.

## Consecuencias esperadas

### Positivas

- El producto vuelve a responder a las preguntas que su dueño hace en lenguaje natural, aunque todavía por API.
- El banco de escenarios mide la capacidad real y servirá de evaluación del asistente cuando llegue el IA, evitando demostraciones sin red.
- La superficie de herramientas queda definida y autorizada antes de elegir modelo, lo que simplifica el ADR de IA futuro.

### Compromisos

- Cada capacidad añade contrato, fixtures y pruebas; el dataset crece y con él el mantenimiento de textos en dos idiomas.
- La búsqueda por nombre es una coincidencia simple, suficiente para el dataset sintético pequeño; deberá replantearse con un HCM real.
- Los estados de presencia son valores cerrados de la fuente sintética; un HCM real podrá exponer estados distintos y el contrato deberá absorberlos.
- La interfaz de revisión queda deliberadamente simple mientras no exista la superficie conversacional.

## Alternativas consideradas

- **Ampliar `IHcmAttendanceSource`:** menos piezas, pero mezcla la revisión de incidencias con consultas de fuerza laboral y complica sustituir cada contrato por separado.
- **Un único endpoint de «respuestas»:** parecido al chat, pero sin tipado; obligaría a la IA a interpretar texto libre en vez de llamar herramientas.
- **Construir el chat primero con un LLM y mock:** viola los principios de verificabilidad y la regla de no incorporar IA sin especificación aprobada.
- **Empezar por la UI de búsqueda:** muestras bonitas sin contrato sólido que luego habría que rehacer.

## Verificación de la decisión

La decisión se considerará implementada cuando AC-025..AC-028 tengan evidencia observada: escenarios 1–6 automatizados y en verde, presencia en el home, poda verificada, contenido bilingüe y suite completa. La aprobación de ADR-0010 autoriza planificar; la implementación se ejecuta por tareas.
