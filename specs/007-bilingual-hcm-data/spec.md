# SPEC-007 — Datos sintéticos del HCM bilingües

- **Estado:** Propuesto para aprobación de planificación (2026-10-17). Nota de herencia (2026-10-21): la fuente relacional del dataset corre sobre PostgreSQL ([ADR-0013](../../docs/architecture/adr/ADR-0013-postgresql-persistence.md)).
- **Implementación:** T1 y T2 implementadas y verificadas; recorrido de navegador validado en español e inglés; sin comprobaciones pendientes.
- **Requisitos relacionados (propuestos):** FR-025 y NFR-020. Relacionados existentes: FR-006, FR-019, FR-024; NFR-005, NFR-017.
- **Criterios relacionados (propuestos):** AC-023 y AC-024 (amendan AC-021 de SPEC-006, ver §7).
- **Decisión arquitectónica:** ADR-0009 propuesto.
- **Decisión de producto registrada:** el usuario eligió “Datos bilingües según el usuario (recomendado)” el 2026-10-17.
- **Versión:** 0.1.

## 1. Objetivo

Que el contenido del asistente coincida con el idioma elegido de punta a punta: hoy la interfaz ya está localizada pero los datos del HCM (reglas, evidencias, tipos y estados de TimeOff, nombres) siguen llegando en inglés. Este cambio añade variantes español/inglés al dataset sintético y hace que la API sirva la variante del idioma del usuario.

## 2. Alcance propuesto

### Incluido

- Catálogo de fixtures con variantes español/inglés para todos los textos de presentación: nombre visible del empleado, país, descripción de fichajes, resumen de la regla, evidencias, elementos de revisión, resumen de relación de TimeOff, tipo y estado de TimeOff, unidad de duración.
- Payload de dataset en versión 2 con ambas variantes, servidas por la API según el idioma preferido del usuario autenticado.
- Selección del idioma en la API a partir del perfil del usuario (siempre actualizada tras cambiar el idioma en el perfil), sin exponer un contrato nuevo de idioma a clientes externos.
- Mantener los identificadores y valores compartidos entre idiomas: `IncidentId`, ids de empleado/oficina, referencias de TimeOff, códigos de regla, ciudades, zonas horarias, fechas, horas y duraciones numéricas.
- Pruebas y documentación actualizados; la guía explica el paso de re-sembrado tras actualizar.

### Fuera de alcance

- Traducir a más de dos idiomas; traducción automática o de terceros.
- Cambiar el contrato JSON de los endpoints de asistencia (campos y tipos idénticos; solo cambian valores de texto).
- Datos reales, HCM real o conectores externos.
- Localizar los problem details de la API (siguen en inglés; la interfaz traduce lo que muestra).

## 3. Requisitos y límites arquitectónicos

- Application conserva sus contratos: `HcmAttendanceInsight` y sus tipos anidados no cambian de forma; el criterio de selección del idioma viaja por la consulta (parámetro aditivo con valor por defecto) sin exponer Infrastructure a la interfaz.
- La API sigue siendo la dueña de la identidad: el idioma se resuelve desde el perfil del usuario autenticado, por lo que un cambio de idioma en `/profile` se refleja en la siguiente consulta sin volver a iniciar sesión.
- La semilla sigue siendo idempotente: el upsert del dataset completo reemplaza el payload v1 por el v2 sin duplicados ni pérdida de datos, y `talentassistant_tests` no recibe la semilla.
- Los identificadores compartidos deben ser byte-idénticos entre variantes; solo cambian los textos de presentación.
- Un `PayloadVersion` desconocido sigue fallando de forma controlada (NFR-006): no se reinterpreta el payload.

## 4. Flujo local esperado

1. Tras actualizar, se re-aplica la inicialización (`docker compose run --rm db-init` o el flujo Compose normal, que ya lo ejecuta) y el dataset pasa a versión 2 con ambas variantes.
2. Un usuario con idioma `es` en su perfil consulta asistencia y las tarjetas muestran reglas, evidencias, tipos y estados en español.
3. El mismo usuario cambia su idioma a `en` en el perfil y la siguiente consulta devuelve la variante inglesa, sin re-login.
4. Los identificadores (`incident-demo-*`, `HCM-SYN-RULE-*`, fechas, zonas horarias) son idénticos en ambos idiomas.
5. Si el payload es de una versión desconocida, la API responde el 503 controlado de asistencia ya existente.

## 5. Componentes candidatos

| Componente | Responsabilidad propuesta | Línea base que debe conservar |
|---|---|---|
| Infrastructure — catálogo | Definir cada fixture con textos por idioma y materializar la lista para `es` y para `en`. | Mismo esqueleto de datos; identificadores compartidos. |
| Infrastructure — serializer | Payload v2 `{ es: [...], en: [...] }`; lectura por idioma; control de versión. | Fallo controlado ante versión desconocida. |
| Infrastructure — fuentes | Memoria y MySQL sirven la variante solicitada por la consulta. | Mismo contrato de salida que hoy. |
| API — selección de idioma | Resolver el idioma preferido del usuario autenticado y pasarlo a la consulta. | Sin cambio de contrato HTTP; sin cabeceras nuevas obligatorias. |
| Application — consulta | Parámetro aditivo de idioma con valor por defecto. | Sin romper consumidores existentes. |
| Pruebas | Round-trip bilingüe, selección por usuario, idempotencia y control de versión. | Suite en verde; base de pruebas separada. |
| Documentación | Guía, README, catálogos y enmiendas de SPEC-006. | Comandos verificados. |

## 6. Matriz de traducción

Se traducen (ejemplos es/en): `Spain` → `España`, `Japan` → `Japón`; `Synthetic HCM punch event` → `Evento de fichaje sintético del HCM`; `The synthetic HCM reports an attendance finding for review.` → `El HCM sintético informa un hallazgo de asistencia para revisión.`; evidencias y revisiones; `Vacation/Approved/workday` → `Vacación/Aprobado/día laborable`; `Birthday/Pending`, `Calamity/Rejected`, `Maternity leave/Cancelled` y sus resúmenes de relación.

Se comparten (idénticos en ambos idiomas): `incident-demo-*`, `employee-demo-*`, `office-demo-*`, `timeoff-demo-*`, `HCM-SYN-RULE-00N`, ciudades (`Madrid`, `Tokyo`), zonas IANA, fechas, horas y duración numérica.

Enmienda 2026-10-25: las etiquetas visibles `Empleado sintético 001..005` se reemplazan por
nombres ficticios plausibles (`Aitana Vidal`, `Mireia Pardo`, `Bruno Castaño`, `Nadia Soler`,
`Gael Domínguez`). Como nombres propios, permanecen idénticos en español e inglés; los alias
estables `employee-demo-*` no cambian.

## 7. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Evidencia futura |
|---|---|---|
| AC-023 | Con el usuario en español, la API devuelve reglas, evidencias, tipos y estados de TimeOff en español; en inglés, la variante inglesa; los identificadores y fechas son idénticos entre ambas respuestas. | Pruebas de API por idioma y recorrido de navegador en ES y EN. |
| AC-024 | La semilla es idempotente en la versión 2, un payload de versión desconocida sigue fallando de forma controlada, la suite permanece verde y `talentassistant_tests` no recibe la semilla. | Suite completa, doble ejecución del inicializador y comprobación de bases. |

Enmienda de SPEC-006: AC-021 afirmaba que “el dataset y los endpoints responden igual en cualquier idioma”; con SPEC-007 esa igualdad se mantiene para identificadores y contrato, y cambia para los textos de presentación, que pasan a servirse por idioma. Se reescribe AC-021 para reflejarlo y se marca la relación con SPEC-007.

## 8. Diseño propuesto para planificar

1. El catálogo pasa a definir cada fixture con textos por idioma y materializar dos listas (`es` y `en`) sobre el mismo esqueleto de datos.
2. El serializer sube a `PayloadVersion = 2` con forma `{ "es": [ ... ], "en": [ ... ] }`; se elimina la escritura de v1 y la lectura de versiones distintas de 2 sigue fallando con `InvalidDataException`.
3. La consulta de asistencia gana un parámetro aditivo de idioma (por defecto `es`); las dos fuentes seleccionan la variante pedida.
4. La API resuelve el idioma desde el perfil del usuario autenticado en cada consulta (consulta por clave primaria, sin cabeceras nuevas y siempre actualizada tras cambiar el perfil).
5. El inicializador existente reemplaza el dataset por el payload v2 en el siguiente arranque; la guía documenta ese paso.

Supuestos explícitos para aprobación: la variante por defecto ante un idioma desconocido es `es`; los estados y tipos de TimeOff se traducen como valores de presentación (la interfaz los muestra en crudo) y ningún código compara esos textos; la resolución por usuario añade una consulta por petición de asistencia, aceptable en el perfil local.

## 9. Plan SDD propuesto

1. Añadir FR-025 y NFR-020, AC-023/AC-024 y la enmienda de AC-021 con trazabilidad.
2. Aceptación de ADR-0009 y actualización del registro de decisiones y de las notas de SPEC-006/ADR-0008 sobre datos sin traducir.
3. Rama `feat/bilingual-hcm-data` desde `main` y plan ODD: T1 datos/serializer/selección y pruebas; T2 documentación y recorrido de navegador en ambos idiomas.
4. Verificación: suite completa, pruebas de round-trip bilingüe, doble semilla idempotente y verificador independiente.
5. Cierre con matriz y commits por work unit.

## 10. Estado y aprobación

SPEC-007 y ADR-0009 quedan propuestos a la espera de aprobación para planificación; la aprobación no autoriza cambios de código hasta autorizar tareas.

## 11. Verificación de T2

T2 documenta y enmienda los artefactos siguientes:

- `docs/requirements/functional-requirements.md`: FR-025 con nota de aprobación (SPEC-007 y ADR-0009).
- `docs/requirements/non-functional-requirements.md`: NFR-020 (Localización de datos).
- `docs/acceptance/mvp-acceptance-criteria.md`: AC-023 y AC-024, enmienda y nota de AC-021, y filas de trazabilidad AC-023 → FR-025/NFR-020 y AC-024 → FR-025/NFR-006/NFR-017.
- `docs/decisions/decision-register.md`: fila ADR-0009 (Aceptado; implementado con limitaciones documentadas).
- `docs/architecture/adr/ADR-0008-ui-localization-and-user-profile.md`: «Compromisos» actualizado; solo los problem details permanecen en su idioma original y se remite a ADR-0009.
- `specs/006-user-profile-localization/spec.md`: «Fuera de alcance» y fila AC-021 enmendadas hacia SPEC-007/ADR-0009.
- `docs/development/local-setup.md`: sección «Idioma y perfil» con el comportamiento por idioma y el paso de re-sembrado (`docker compose run --rm db-init`; Compose lo hace en `up --build`).
- `README.md`: «Estado actual» con T1 implementada y verificada (suite 30/30).

El recorrido de navegador end-to-end se ejecutó contra el stack Compose reconstruido y validó AC-023: con el perfil en español las tarjetas muestran `Vacación`, `Aprobado`, `España · Madrid`, `Empleado sintético 001` y reglas/evidencias en español, sin un solo `Vacation`; al guardar `en` en el perfil, la misma consulta devuelve `Vacation`, `Approved`, `Spain · Madrid` y las reglas inglesas sin re-login, con los identificadores idénticos entre variantes; la consola del navegador quedó sin errores y el perfil se restauró a español. Commits de T2: `0e01e18994f967ab2e36413155fae2bb1bfb65b3` (documentación y enmiendas) y el de esta evidencia.
