# SPEC-006 — Perfil de usuario y localización de la interfaz

- **Estado:** Propuesto para aprobación de planificación (2026-10-17).
- **Implementación:** T1, T2 y T3 implementadas; recorrido de navegador verificado en ambos idiomas; pendiente verificación final del feature.
- **Requisitos relacionados (propuestos):** FR-023, FR-024 y NFR-019. Relacionados existentes: FR-007, FR-009, FR-010, FR-021, FR-022; NFR-004, NFR-006, NFR-012, NFR-018.
- **Criterios relacionados (propuestos):** AC-019, AC-020, AC-021.
- **Decisión arquitectónica:** ADR-0008 propuesto.
- **Decisión de producto registrada:** el usuario eligió “UI completa, API sin cambiar (recomendado)” el 2026-10-17.
- **Versión:** 0.1.

## 1. Objetivo

Cerrar el desajuste de idiomas actual (página de login y algunos textos en español; contenido interno en inglés) y dar al usuario local un perfil donde gestionar su nombre, su idioma y su contraseña. Todo dentro de la aplicación local, sin servicios externos.

## 2. Alcance propuesto

### Incluido

- Selector de idioma (español/inglés) en la página de login, efectivo de inmediato y persistente mediante cookie de cultura, incluso sin sesión. Aprobado como píldora compacta en la esquina superior derecha con autoenvío.
- Rediseño de la página de login al estilo del mockup del usuario (tarjeta de bienvenida, inputs redondeados con fondo suave, mostrador de contraseña y botón de acento) y opción «Permanecer conectado» que extiende la cookie de sesión a 30 días; sin marcar, la cookie sigue siendo de sesión del navegador.
- Página de perfil para usuarios autenticados: nombre visible, idioma preferido y cambio de contraseña (con la contraseña actual).
- Localización de TODA la interfaz: login, layout (navegación, usuario, cierre de sesión), home, asistencia, páginas de error y reconexión. Dos idiomas: español (por defecto) e inglés.
- Migración aditiva en las tablas de Identity para el nombre visible y el idioma preferido del usuario.
- Endpoint de la API para leer y actualizar el perfil y para cambiar la contraseña, validando el idioma contra el catálogo cerrido `es`/`en`.
- Documentación local actualizada (guía, README, catálogos de requisitos y criterios).

### Fuera de alcance

- Localizar los problem details de la API: son contrato, no interfaz; la interfaz traduce lo que muestra. La localización de la DATA sintética del HCM (nombres de oficinas, tipos de ausencia, evidencias) pasó a SPEC-007/ADR-0009.
- Más de dos idiomas, traducciones automáticas, detección automática del idioma del navegador y soporte RTL.
- Registro de usuarios, recuperación de contraseñas, MFA y cualquier cambio en los roles o en la autorización.
- Cambios en el contrato JSON de los endpoints de asistencia.

## 3. Requisitos y límites arquitectónicos

- La API sigue siendo la dueña de la identidad: el perfil se expone como endpoints de la API y Web vuelve a consumirlos por contrato HTTP, sin acceso a base de datos (FR-007, NFR-001/NFR-002).
- El nombre visible y el idioma son atributos del usuario local; no introducen entidades de negocio ni datos reales.
- El idioma elegido se aplica en la interfaz; la localización del contenido del HCM se resolvió después en SPEC-007/ADR-0009 (datos bilingües servidos por el idioma del usuario).
- Los secretos y la política de contraseñas de SPEC-005 se conservan: la contraseña nueva cumple la política de Identity y solo se almacena como hash (NFR-018).
- La interfaz accesible mantiene sus propiedades: etiquetas asociadas, navegación por teclado, contraste (NFR-012), y los textos localizados no rompen el marcado (NFR-010).

## 4. Flujo local esperado

1. Una persona sin sesión abre Web y ve el login en español (idioma por defecto); puede cambiar a inglés con el selector y la página cambia de inmediato; la elección persiste tras recargar.
2. Se autentica: la sesión hereda el idioma preferido guardado en su perfil (o el elegido en el login la primera vez).
3. Navega por home y asistencia en su idioma; el layout muestra su nombre visible y un enlace a su perfil.
4. En el perfil actualiza su nombre y su idioma; al guardar, la interfaz refleja ambos cambios de inmediato.
5. En el perfil cambia su contraseña proporcionando la actual; con la nueva puede volver a entrar; con la actual ya no puede.
6. Un cambio de idioma no altera los datos mostrados: los cinco hallazgos y sus estados siguen siendo los mismos.

## 5. Componentes candidatos

| Componente | Responsabilidad propuesta | Línea base que debe conservar |
|---|---|---|
| Infrastructure — usuario | Usuario de Identity extendido con nombre visible e idioma; migración aditiva. | Mismo servidor MySQL; sin tocar datos existentes. |
| API — perfil | Leer/actualizar perfil y cambiar contraseña; validar idioma contra `es`/`en`. | Respuestas controladas; hash solo; sin secretos en logs. |
| API — login ampliado | El login devuelve también nombre e idioma para que Web configure la cultura sin llamadas extra. | Contrato existente conservado (campos nuevos, no rotos). |
| Web — localización | Cultura por cookie, recursos de cadenas por página y layout, y selector en el login. | Sin dependencias nuevas; patrón estándar de ASP.NET Core. |
| Web — perfil | Página `/profile` con nombre, idioma y cambio de contraseña. | Protegida por sesión; accesible; errores controlados. |
| Pruebas | Pruebas de API de perfil/contraseña y de localización básica. | Suite existente en verde; base de pruebas separada. |
| Documentación | Guía local, README, catálogos y estado de la especificación. | Comandos verificados; sin secretos. |

## 6. Modelo de perfil y de idioma

- El usuario de Identity se extiende con `FullName` (nombre visible, opcional) y `PreferredLanguage` (`es` por defecto).
- La migración es aditiva: solo añade columnas a `AspNetUsers`; no modifica ni elimina datos existentes y no toca el dataset de asistencia.
- El idioma se valida contra el catálogo cerrido `es`/`en`; valores desconocidos se rechazan con error controlado.
- El cambio de contraseña exige la contraseña actual y aplica la política de Identity; el fallo devuelve un error controlado sin exponer detalles internos.
- La interfaz muestra `FullName` si existe y, si no, el nombre de usuario.

## 7. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Evidencia futura |
|---|---|---|
| AC-019 | La persona sin sesión puede cambiar el idioma del login entre español e inglés; la elección se aplica de inmediato y persiste tras recargar. | Prueba de navegador en ambos idiomas y sondeo de la cookie de cultura. |
| AC-020 | El usuario autenticado gestiona su perfil: el nombre visible aparece en el layout, el idioma elegido localiza home, asistencia y textos de navegación/error, y el cambio de contraseña exige la actual y respeta la política. | Pruebas de API de perfil/contraseña y recorrido de navegador en ambos idiomas. |
| AC-021 | La localización no altera los identificadores ni el contrato: el dataset sintético y los endpoints de asistencia conservan ids, fechas, zonas horarias y campos idénticos en cualquier idioma, mientras que los textos de presentación se sirven en el idioma del usuario (enmienda de SPEC-007), la migración es aditiva, la suite permanece verde y no se introducen servicios externos ni datos reales. | Suite completa, comparación de respuestas de la API y revisión de la migración. |
| AC-022 | El login se presenta como el mockup aprobado: píldora de idioma autoenviante, tarjeta de bienvenida, inputs suaves, mostrador de contraseña y botón de acento. «Permanecer conectado» extiende la cookie a 30 días y sin él la cookie es de sesión; el enlace de recuperación no existe (fuera de alcance). | Comprobación de cabeceras Set-Cookie con y sin la opción, recorrido de navegador y suite. |

AC-019..AC-022 son propuestos y no se declaran aceptados hasta aprobar SPEC-006 y ADR-0008.

## 8. Diseño propuesto para planificar

1. El usuario de Identity se extiende con `FullName` y `PreferredLanguage` mediante una migración aditiva sobre `AspNetUsers`.
2. La API expone lectura/actualización de perfil y cambio de contraseña; el login ampliado devuelve nombre e idioma junto al token.
3. La localización usa el mecanismo estándar de ASP.NET Core: `IStringLocalizer` con recursos de cadenas por página/layout (español como recurso neutral, inglés como satélite) y `RequestLocalizationMiddleware` con cookie de cultura; el login expone el selector que fija esa cookie.
4. El idioma preferido del usuario se persiste en el perfil y se aplica al iniciar sesión y al guardar el perfil; Web sigue sin base de datos (el flujo pasa por la API).
5. La guía local documenta el selector, el perfil y el cambio de contraseña.

Supuestos explícitos para aprobación:

- El idioma por defecto es español; no se detecta automáticamente el idioma del navegador.
- El nombre visible es un campo libre sin validación de formato más allá de longitud razonable.
- El cambio de contraseña requiere la contraseña actual y no envía correo de notificación.
- Los problem details de la API siguen en inglés y la interfaz traduce los mensajes que muestra al usuario.

## 9. Plan SDD propuesto

1. Añadir FR-023, FR-024 y NFR-019 a los catálogos y AC-019..AC-021 a los criterios, con trazabilidad.
2. Aceptación de ADR-0008 y actualización del registro de decisiones.
3. Rama de feature `feat/profile-localization` desde `main` y plan ODD con tareas acotadas (perfil e idioma en Infrastructure/API; localización e interfaz en Web; documentación y verificación final).
4. Verificación propuesta: suite completa, pruebas de API de perfil/contraseña/localización, recorrido de navegador en ambos idiomas y verificador independiente por el riesgo de seguridad (cambio de contraseña y datos de usuario).
5. Cierre con matriz requisito/criterio/evidencia y commits por work unit.

## 10. Estado y aprobación

SPEC-006 y ADR-0008 quedan propuestos a la espera de aprobación para planificación. La aprobación no autoriza por sí sola cambios de código: cada tarea se autoriza después según el plan ODD.

## 11. Verificación de T3

T3 documenta la implementación de T1/T2 en estos artefactos del repositorio:

- `docs/development/local-setup.md`: sección «Idioma y perfil» (selector anónimo en el login, perfil, cambio de contraseña con la actual y límites de la localización).
- `README.md`: estado de SPEC-006 en «Ejecución local» y «Estado actual».
- `docs/decisions/decision-register.md`: ADR-0008 registrado como aceptado e implementado con limitaciones documentadas.
- `docs/requirements/functional-requirements.md`: FR-023 y FR-024 con su nota de aprobación e implementación.
- `docs/requirements/non-functional-requirements.md`: NFR-019.
- `docs/acceptance/mvp-acceptance-criteria.md`: AC-019..AC-021 y sus filas de trazabilidad.

El recorrido de navegador end-to-end se ejecutó contra el stack Compose reconstruido y validó AC-019 y AC-020 en ambos idiomas: anónimo en `/` redirige a `/login` en español por defecto; el selector conmuta a inglés de inmediato y la elección persiste tras navegar; el login hereda el idioma del perfil guardado; `/profile` se muestra en ambos idiomas y guardar nombre/idioma se refleja de inmediato en el layout; Attendance insights en inglés muestra los 5 hallazgos con etiquetas localizadas y los datos del HCM sin traducir; el cambio de contraseña con la actual incorrecta muestra el mensaje localizado correcto, y el cambio real y su restauración pasan; el logout regresa al login en español con la preferencia anónima preservada; la consola del navegador quedó sin errores. La comparación de respuestas de la API confirma AC-021: los cinco fixtures y el dataset son idénticos en cualquier idioma y la migración sigue siendo aditiva.

Commits de T3: `8202103dba6c2de83bbf01cc69247411125c896b` (documentación y catálogos) y `93d7036efa4a1a40fc02bd994f296764b6c8ecc8` (corrección del clasificador de contraseña actual, hallazgo del smoke: la API devuelve "Incorrect password." y la Web buscaba otro texto, clasificándolo como fallo de política). Advisory cosmetico: durante la navegación dentro del circuito el elemento `<PageTitle>` puede conservar el título de la página anterior (comportamiento preexistente; los recursos de título son correctos).
