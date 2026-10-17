# ADR-0009 — Datos sintéticos bilingües servidos por el idioma del usuario

- **Estado:** Propuesto (2026-10-17).
- **Fecha:** 2026-10-17.
- **Aprobación de arquitectura:** pendiente de aprobación del usuario junto con SPEC-007.
- **Especificación relacionada:** SPEC-007 — Datos sintéticos del HCM bilingües.

## Contexto

SPEC-006 localizó toda la interfaz (es/en), pero el payload del dataset sintético sigue en inglés: reglas, evidencias, tipos y estados de TimeOff, nombres de empleado y descripciones de fichaje. El usuario vio “mucha información en inglés” con el idioma en español y eligió “Datos bilingües según el usuario (recomendado)”.

Hoy el catálogo vive como lista estática con textos incrustados, el serializer espera `PayloadVersion = 1` con una lista plana y las dos fuentes (memoria y MySQL) devuelven esa misma lista sin considerar idioma.

## Decisión propuesta

- El catálogo define cada fixture con textos por idioma y materializa dos variantes sobre un único esqueleto de datos: los identificadores, fechas, horas, zonas horarias, ciudades y duraciones son byte-idénticos entre variantes; solo cambian los textos de presentación (incluidos tipo y estado de TimeOff, que son valores de muestra, no contratos de código).
- El payload sube a `PayloadVersion = 2` con forma `{ "es": [ ... ], "en": [ ... ] }` en la misma fila de dataset, conservando el modelo “una fila por dataset” de SPEC-004. Cualquier versión distinta de 2 sigue fallando con `InvalidDataException` (503 controlado).
- La consulta de asistencia recibe el idioma como parámetro aditivo con valor por defecto; las fuentes seleccionan la variante pedida sin cambiar el contrato JSON de la API.
- La API resuelve el idioma desde el perfil del usuario autenticado en cada consulta (búsqueda por clave primaria): siempre refleja el último cambio hecho en `/profile`, no depende de cabeceras del cliente y funciona también para llamadas directas a la API con token.
- El inicializador existente reemplaza v1 por v2 por upsert en el siguiente arranque; la guía documenta el paso (Compose ya lo ejecuta automáticamente con `db-init`).

## Impacto en decisiones vigentes

- ADR-0002 (datos sintéticos): se mantiene; las dos variantes siguen siendo fixtures locales, sin datos reales ni servicios externos.
- ADR-0004/0006: misma tabla y misma fila de dataset; solo cambia el contenido y la versión del payload. No hay migración de esquema: el bump de versión vive en la siembra.
- ADR-0007/0008: la interfaz localizada ya existe; este cambio la completa por dentro. La resolución de idioma se queda en la API, coherente con “autorización y datos en el backend”.
- SPEC-004: se conservan idempotencia y fallo controlado por versión; el catálogo único sigue siendo la única fuente de verdad.

## Consecuencias esperadas

### Positivas

- Experiencia coherente en los dos idiomas: lo que se ve en la interfaz coincide con el contenido que la llena.
- Sin cambios de contrato HTTP ni de esquema; la Web no necesita modificaciones.
- Cambiar de idioma en el perfil se refleja en la siguiente consulta, sin re-login.

### Compromisos

- El dataset duplica sus textos de presentación; cada texto nuevo debe añadirse en los dos idiomas (mismo coste de mantenimiento que los recursos de interfaz).
- Cada consulta de asistencia resuelve el idioma con una consulta por clave primaria al perfil: coste despreciable en local, a revisar si el dataset crece.
- Cualquier prueba o documentación que compare textos en inglés concretos debe actualizarse a la matriz por idioma.
- Tras desplegar este cambio es obligatorio re-aplicar la semilla; sin ella, la API lee v1 y responde el 503 controlado de asistencia.

## Alternativas consideradas

- **Claim de idioma en el JWT:** una consulta menos por petición, pero el token caduca con el idioma antiguo hasta re-login tras cambiar el perfil; se descarta por el desfase.
- **Cabecera `Accept-Language` del cliente:** simple, pero pone el idioma de los datos en manos del cliente y exige que la Web lo propague siempre; la fuente de verdad ya es el perfil.
- **Payload único en español:** cambio menor, pero rompe la coherencia del modo inglés (mezcla inversa) y el usuario eligió la opción bilingüe.
- **Dos filas de dataset por idioma:** viable, pero rompe el modelo “una fila por dataset” de SPEC-004 sin ganancia.

## Verificación de la decisión

La decisión se considerará implementada cuando AC-023 y AC-024 tengan evidencia observada (variantes por idioma con identificadores idénticos, semilla v2 idempotente, control de versión, suite verde y recorrido de navegador en ES y EN). La aprobación de ADR-0009 autoriza planificar; la implementación se ejecuta por tareas.
