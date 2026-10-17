# ADR-0008 — Localización de la interfaz y perfil de usuario

- **Estado:** Propuesto (2026-10-17).
- **Fecha:** 2026-10-17.
- **Aprobación de arquitectura:** pendiente de aprobación del usuario junto con SPEC-006.
- **Especificación relacionada:** SPEC-006 — Perfil de usuario y localización de la interfaz.

## Contexto

La interfaz presenta un desajuste de idiomas: la página de login y algunos textos están en español, mientras que el contenido interno (home, asistencia, navegación) está en inglés. El usuario quiere un selector de idioma en el login (español/inglés), una página de perfil para nombre, idioma y cambio de contraseña, y que el contenido se muestre según el idioma elegido.

SPEC-005 ya puso a la API como dueña de la identidad con Identity en MySQL y una cookie de sesión en Web, así que el perfil debe integrarse en ese modelo sin dar acceso a base de datos a la interfaz.

## Decisión propuesta

- El usuario de Identity se extiende con `FullName` y `PreferredLanguage` (una subclase de `IdentityUser`), persistidos por una migración aditiva sobre `AspNetUsers`; no se crea una tabla paralela de perfiles.
- La localización usa el mecanismo estándar de ASP.NET Core en Blazor: `RequestLocalizationMiddleware` con cookie de cultura, `IStringLocalizer` con recursos de cadenas por página y layout, español como recurso neutral e inglés como satélite. Cada componente localizado lee sus cadenas del recurso; no hay cadenas visibles hardcodeadas.
- El login incluye un selector de idioma que fija la cookie de cultura mediante un endpoint pequeño y recarga la página; funciona sin sesión y sirve de base para la sesión autenticada.
- El idioma preferido se guarda en el perfil del usuario. Al iniciar sesión, la API devuelve nombre e idioma junto al token y Web fija la cookie de cultura; al guardar el perfil se actualiza la base, la cookie y la sesión se recargan.
- El cambio de contraseña se expone en la API exigiendo la contraseña actual, con la política de Identity y respuestas controladas.
- El alcance inicial de la localización fue la interfaz; la localización de los datos sintéticos del HCM se resolvió después mediante ADR-0009. Los problem details de la API no se localizan; la interfaz traduce los mensajes que muestra.

## Impacto en decisiones vigentes

- ADR-0003, ADR-0005 y ADR-0007 no cambian: la identidad sigue en la API y la autorización en el backend; el perfil amplía el mismo modelo de usuario.
- ADR-0004 y ADR-0006 se mantienen: la migración es aditiva sobre el mismo servidor MySQL y no toca el dataset de asistencia de SPEC-004.
- La política de contraseñas y el manejo de secretos de SPEC-005/NFR-018 siguen vigentes para el cambio de contraseña.

## Consecuencias esperadas

### Positivas

- Una sola experiencia de idioma coherente en toda la aplicación y una ubicación predecible para cada cadena.
- El perfil usa el mecanismo estándar de Identity y el mismo patrón cliente-API ya existente; sin dependencias nuevas.
- El nombre visible mejora la claridad del layout frente al nombre de usuario técnico.

### Compromisos

- Cada nueva página o cadena visible debe añadirse a los recursos de los dos idiomas; el coste de mantener la localización crece con la superficie de UI.
- Cambiar de idioma recarga la página (no es una conmutación en vivo dentro del circuito).
- Los problem details de la API permanecen en su idioma original; es una decisión explícita de alcance (los datos del HCM se localizaron después mediante ADR-0009).
- La subclase de Identity añade dos columnas y una migración que convive con la de SPEC-005 en el mismo historial.

## Alternativas consideradas

- **Diccionario propio de traducciones en JSON:** menos ceremonia, pero reinventa lo que la plataforma ya ofrece (recursos, cultura, neutral language) y se vuelve frágil al crecer.
- **Tabla separada de perfiles:** separa datos de usuario de Identity, pero añade uniones y código propio para datos que encajan naturalmente en el usuario; se descarta por complejidad sin beneficio.
- **Localizar también la API (Accept-Language):** más completo, pero amplía contrato y mantenimiento; el usuario eligió UI completa sin cambiar la API para esta versión.
- **Detección automática del idioma del navegador:** conveniente, pero añade comportamiento implícito; se deja fuera y el idioma por defecto es español.

## Verificación de la decisión

La decisión se considerará implementada cuando AC-019..AC-021 de SPEC-006 tengan evidencia observada: selector anónimo persistente, perfil y cambio de contraseña probados, recorrido de navegador en ambos idiomas, migración aditiva y suite completa en verde. La aprobación de ADR-0008 autoriza planificar; la implementación se ejecuta por tareas del plan ODD.
