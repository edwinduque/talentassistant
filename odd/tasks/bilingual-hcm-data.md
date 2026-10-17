# Seguimiento ODD: datos sintéticos del HCM bilingües

Objetivo: que el contenido de las tarjetas (reglas, evidencias, fichajes, TimeOff, nombres) se sirva en el idioma del usuario autenticado, manteniendo identificadores y contrato estables.
Rama: `feat/bilingual-hcm-data`; entrega: `feature-branch-chain`; runner: SDK `10.0.402`, `dotnet build`, `dotnet test`, smoke Compose y navegador en ambos idiomas; revisión nativa desactivada globalmente (`disabled/unmanaged`).

## Specs

### S1 — Catálogo y payload bilingües

Alcance: el catálogo de fixtures define textos por idioma y materializa las variantes `es` y `en` sobre el mismo esqueleto; el serializer sube a `PayloadVersion = 2` con forma `{ "es": [...], "en": [...] }` en la misma fila de dataset; versiones desconocidas siguen fallando de forma controlada.

Texto exacto del usuario: “en el asistente veo mucha informacion en ingles cuando en realidad tengo idioma en español”.

Aceptación: se traducen nombre de empleado, país, descripción de fichaje, resumen de regla, evidencias, revisiones, resumen de relación, tipo y estado de TimeOff y unidad; ids, referencias, códigos de regla, ciudades, zonas horarias, fechas y duraciones son byte-idénticos entre variantes; la semilla v2 es idempotente por upsert y una versión desconocida responde el 503 controlado existente.

### S2 — Selección por idioma del usuario

Alcance: parámetro aditivo de idioma en la consulta de asistencia (por defecto `es`) y resolución en la API desde el perfil del usuario autenticado en cada petición.

Aceptación: un usuario `es` recibe la variante española y `en` la inglesa; cambiar el idioma en `/profile` se refleja en la siguiente consulta sin re-login; el contrato JSON de los endpoints no cambia y la Web no necesita modificaciones.

### S3 — Límites arquitectónicos

Alcance: contratos de Application sin cambios de forma; Infrastructure no conoce la interfaz; la API sigue siendo la dueña de la identidad; datos reales y servicios externos siguen prohibidos.

Aceptación: los registros `HcmAttendanceInsight` y anidados no cambian de forma; `talentassistant_tests` no recibe la semilla; sin secretos ni credenciales nuevas en el repositorio.

### S4 — Documentación y verificación

Alcance: catálogos (FR-025, NFR-020, AC-023/AC-024 y enmienda de AC-021), registro de decisiones, guía local con el paso de re-sembrado y notas de SPEC-006/ADR-0008 sobre datos sin traducir.

Texto exacto del usuario: “todo manejado con sdd” ( criterio heredado del flujo del proyecto).

Aceptación: guía reproducible con el paso de re-sembrado; trazabilidad completa; suite verde; recorrido de navegador en ES y EN con contenido en el idioma correcto; verificador independiente sin bloqueos.

Risk: item 1 (cambio del formato de payload persistido con bump de versión, reemplazado por siembra idempotente). T1 requiere verificación independiente tras sus checks propios.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1, S2, S3 | Inline | Implementada; suite 30/30, resemilla v2 y smoke ES/EN por perfil en verde | Commit pendiente en `feature-branch-chain` |
| T2 | S4 | Inline | Planificada | Depende de T1 |

Forecast inicial: aproximadamente 500–800 líneas authored entre catálogo, serializer, selección, pruebas y documentación; archivos generados excluidos. Estrategia `feature-branch-chain`; el umbral de ~400 líneas es heurístico, no un límite de tamaño.

## Log

### L1 — Solicitud original (2026-10-17)

en el asistente veo mucha informacion en ingles cuando en realidad tengo idioma en español

### L2 — Decisión de alcance de datos (2026-10-17)

Texto exacto del usuario: “Datos bilingües según el usuario (recomendado)”. El dataset lleva variantes es/en y la API sirve la del usuario autenticado; se descartaron payload único en español y mantener el estado actual.

### L3 — Aprobación para planificación (2026-10-17)

Texto exacto del usuario: “Aprobar SPEC-007 y ADR-0009 (recomendado)”. Se aprueban `specs/007-bilingual-hcm-data/spec.md` y `docs/architecture/adr/ADR-0009-bilingual-hcm-data.md` para planificar; esto no autoriza cambios de código hasta autorizar tareas.

### L4 — Implementación de T1 (2026-10-17)

Texto exacto del usuario: “Autorizar T1 (recomendado)”.

Implementado: catálogo reconstruido con diccionario de textos por clave e idioma (un solo sitio de construcción; identificadores, fechas, ciudades y zonas compartidos) y variantes materializadas por idioma con caché estático; serializer con `PayloadVersion = 2` y forma `{ "es": [...], "en": [...] }` con selección `ForLanguage` (fallback a `es` ante idioma desconocido) y control de versión inalterado; `AttendanceInsightQuery` gana `Language` aditivo con valor por defecto `es`; la fuente de memoria sirve la variante y la MySQL selecciona tras deserializar; la API resuelve el idioma desde el perfil del usuario autenticado en cada consulta; el inicializador serializa ambas variantes.

Pruebas: reescritura del test de serializer con round-trip bilingüe, identificadores idénticos entre variantes, fallback de idioma y control de versión; tests de fuente ahora explícitos en inglés más uno nuevo que documenta el español por defecto. Evidencia: build 0 warnings/0 errores; suite completa 30/30 (5 unit + 25 integración); resemilla de `talentassistant_dev` con `PayloadVersion = 2` y payload `{es,en}`; smoke contra la API con la misma sesión: perfil `es` → `Vacación`/`Aprobado`, cambio de perfil a `en` → `Vacation`/`Approved` sin re-login, ids idénticos, perfil restaurado a `es`.

Risk: item 1 (formato de payload persistido con bump de versión, reemplazado por upsert idempotente).
