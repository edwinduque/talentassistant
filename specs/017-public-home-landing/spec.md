# SPEC-017 — Landing pública de Talent Assistant

- **Estado:** Implementación completada; suite completa de integración pendiente por configuración local de bases de prueba (2026-10-23).
- **Requisito:** FR-034.
- **Solicitud del usuario (verbatim):** “Ayudame con un nuevo requerimiento, actualmente la pagina inicial es la de login, quiero crear un home para allí explicar un poco que es el proyecto. En una proxima iteración quiero agregar un video, es basicamente una landing con un menú superior y entre los menús está el de login”

## 1. Objetivo

Presentar Talent Assistant en una landing pública como puerta de entrada de la aplicación y conservar el login y el área operativa autenticada.

## 2. Alcance y límites

- La ruta `/` muestra la landing sin requerir sesión.
- La landing explica el propósito del proyecto con información fiel a la visión aprobada: apoyar la revisión de asistencia con datos y evidencia del HCM; el HCM conserva sus datos y reglas. El MVP utiliza datos sintéticos y no requiere sistemas corporativos ni datos personales reales.
- Un menú superior enlaza a las secciones informativas de la landing y a `/login`.
- Los textos nuevos siguen la localización existente de español e inglés.
- El resumen operativo existente pasa a `/overview` y sigue requiriendo autenticación. Un login sin destino solicitado abre `/overview`.
- El video pertenece a una iteración futura: no se agrega video, reproductor ni placeholder en esta iteración.

## 3. Criterios de aceptación

- **AC-042:** Una persona anónima puede abrir `/`, comprender el propósito de Talent Assistant y navegar desde el menú superior a las secciones y a `/login`; los textos aparecen en español o inglés según la cultura configurada. No hay video ni reproductor de placeholder.
- **AC-043:** `/overview` conserva el resumen operativo y requiere autenticación; `/login` sigue disponible sin sesión y, si no recibe `ReturnUrl`, su formulario envía a `/overview` tras autenticar.

## 4. Verificación

- RED observado: la ruta `/` redirigía al login y el formulario de login usaba `/` como destino por defecto; 2 de las 3 pruebas nuevas fallaban antes del cambio.
- GREEN: `dotnet test tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --filter FullyQualifiedName~PublicLandingRouteTests` — 3/3.
- Integración completa: 78/140 pasaron y 62 fallaron al no estar configuradas `HcmPostgres__ConnectionString` y `ConnectionStrings__TalentAssistantTests`; la suite completa queda pendiente del entorno local de bases de prueba.
- Unitarias: `dotnet test tests/TalentAssistant.UnitTests/TalentAssistant.UnitTests.csproj` — 11/11.
- Navegador: inspección renderizada en escritorio y ancho móvil de 390 px; menú y secciones presentes, sin overflow horizontal ni errores de consola. Recursos ES/EN: 47 claves en cada idioma, sin diferencias.
- El video y el reproductor no se agregaron.

## 5. Riesgo

Risk: item 2 (se hace pública únicamente la landing; el resumen y las demás páginas funcionales conservan la autorización).
