# SPEC-018 — Identidad visual unificada de la interfaz

- **Estado:** Implementación completada; suite completa de integración parcial por configuración local de bases de prueba (2026-10-23).
- **Requisito:** NFR-023.
- **Solicitud del usuario (verbatim):** “Tengo un nuevo requerimiento. Con sdd tratemos en tener un diseño unificado, en el login y en las pantallas se ve otro diseño. podemos adaptar esas pantallas a un diseño similar a la landing page”

## 1. Objetivo

Aplicar el lenguaje visual de la landing de SPEC-017 al login y a las pantallas operativas para que Talent Assistant se perciba como una sola aplicación, sin cambiar la información ni los flujos de cada pantalla.

## 2. Alcance

- Unificar marca, paleta, tipografía, encabezados, superficies, tarjetas, controles y acciones en `/`, `/login`, `/overview`, `/attendance-insights`, `/assistant`, `/profile` y `/admin/users`.
- Alinear también las páginas de error, no encontrado y el diálogo de reconexión cuando usan la carcasa Web.
- Compartir las utilidades `.sr-only` y `.visually-hidden`; el texto auxiliar conserva su presencia en el árbol accesible sin duplicarse visualmente.
- Tomar como referencia la paleta existente de la landing: teal profundo `#17383b`, acento teal `#086b63`, ámbar `#e5ad48`, canvas `#f2f5f4`, superficie blanca y tipografía sans-serif; mantener las variantes de modo oscuro y alto contraste.
- Conservar la estructura y propósito de cada pantalla, los recursos ES/EN, labels, validaciones, mensajes, rutas, autorización, formularios y contratos HTTP existentes.
- Mantener adaptación a escritorio y móvil, landmarks, navegación por teclado, contraste, foco visible y modos claro/oscuro y de alto contraste según NFR-012.

## 3. Fuera de alcance

- Cambios en comportamiento, contenido funcional, endpoints, datos, autenticación, autorización o localización.
- Dependencias externas, tipografías remotas, video o reproductor.
- Reorganizar la navegación o sustituir los flujos de cada pantalla.

## 4. Criterio de aceptación

- **AC-044:** La landing, el login, las pantallas operativas, las páginas de error y el diálogo de reconexión mencionados en §2 comparten paleta, tipografía, superficies, controles y acciones. Se conserva el contenido y comportamiento existente en español e inglés, con presentación usable en escritorio/móvil, navegación por teclado y modos claro/oscuro y de alto contraste.

## 5. Verificación

- RED observado: la prueba nueva encontró `#f97316` en el estilo inline del login antes de unificar los tokens.
- GREEN: `dotnet test tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --filter FullyQualifiedName~PublicLandingRouteTests` — 4/4.
- Unitarias: `dotnet test tests/TalentAssistant.UnitTests/TalentAssistant.UnitTests.csproj` — 11/11.
- Integración completa: 79/141 pasaron y 62 fallaron en pruebas dependientes de las bases locales; faltan `HcmPostgres__ConnectionString` y `ConnectionStrings__TalentAssistantTests`.
- Navegador: `/` y `/login` inspeccionados a 1200×900 y 390 px, en modo claro y oscuro; sin overflow horizontal. Las páginas autenticadas no pudieron abrirse en el navegador sin sesión/servicios locales; sus selectores, tokens compartidos y marcado se revisaron estáticamente.
- El CSS del diálogo de reconexión incluye `prefers-reduced-motion`; no se abrió ese diálogo durante la inspección visual. Recursos ES/EN y contratos funcionales no se modificaron.
- `.sr-only` y `.visually-hidden` ahora tienen una definición global compartida; las leyendas conservan su semántica accesible y dejan de aparecer como texto visual redundante.

## 6. Riesgo

Risk: item 6 (los cambios visuales requieren inspección renderizada; las pruebas automatizadas disponibles no son pruebas de regresión visual).
