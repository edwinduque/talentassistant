# Seguimiento ODD: perfil de usuario y localización de la interfaz

Objetivo: selector de idioma (español/inglés) en el login, página de perfil (nombre visible, idioma preferido y cambio de contraseña) y toda la interfaz localizada según el idioma elegido, con la API como dueña de la identidad.
Rama: `feat/profile-localization`; entrega: `feature-branch-chain` (misma estrategia de las features anteriores); runner: SDK `10.0.402`, `dotnet build`, `dotnet test`, smoke Compose y navegador en ambos idiomas; revisión nativa desactivada globalmente (`disabled/unmanaged`).

## Specs

### S1 — Selector de idioma anónimo

Alcance: dropdown ES/EN en la página de login que fija la cookie de cultura mediante un endpoint pequeño y recarga la página; funciona sin sesión y persiste tras recargar.

Texto exacto del usuario: “en la pagina de login haya un dropdown para seleccionar el idioma entre español e ingles”.

Aceptación: cambiar el idioma en el login cambia los textos de esa página de inmediato; la elección persiste tras recargar y tras cerrar sesión; no requiere autenticación; el idioma por defecto es español.

### S2 — Perfil de usuario

Alcance: usuario de Identity extendido con nombre visible e idioma preferido (migración aditiva), endpoints de la API para leer/actualizar perfil y cambiar contraseña, login ampliado con nombre e idioma, y página `/profile` en Web.

Texto exacto del usuario: “el usuario logueado deberia tener una pagina de perfil donde pueda agregar nombre, idioma y permita cambiar la contraseña”.

Aceptación: el nombre visible aparece en el layout; guardar nombre e idioma los refleja de inmediato en la interfaz; el cambio de contraseña exige la actual, aplica la política de Identity y la nueva contraseña sustituye a la anterior; los valores de idioma fuera de `es`/`en` se rechazan con error controlado.

### S3 — Localización de la interfaz

Alcance: infraestructura de localización estándar (middleware de cultura, `IStringLocalizer` con recursos por página y layout, español neutral e inglés satélite) y extracción de todas las cadenas visibles de login, layout, home, asistencia, error, no encontrado y reconexión.

Texto exacto del usuario: “que segun el idioma se muestre el contenido”.

Aceptación: no quedan cadenas visibles hardcodeadas en los componentes; home, asistencia, navegación, errores y mensajes de la interfaz se muestran por completo en el idioma activo; cambiar de idioma con sesión refleja el contenido nuevo tras la recarga.

### S4 — Límites arquitectónicos

Alcance: la identidad y el perfil viven en la API; Web sigue sin base de datos; los datos sintéticos del HCM y los problem details de la API no se localizan; la migración es aditiva.

Aceptación: Domain/Application sin referencias a Identity ni a localización; Web consume el perfil por contrato HTTP; el dataset de asistencia y las respuestas de la API son idénticas en cualquier idioma; `talentassistant_tests` sigue separada.

### S5 — Documentación y verificación

Alcance: catálogos de requisitos y criterios, registro de decisiones, guía local y README; verificación con suite, pruebas de API y recorrido de navegador en ambos idiomas.

Texto exacto del usuario: “Ayudame con un nuevo requerimiento” (gestionado con SDD, como el usuario pidió).

Aceptación: FR-023, FR-024 y NFR-019 añadidos con trazabilidad; AC-019..AC-021 en los criterios; guía con selector, perfil y cambio de contraseña; suite completa verde; recorrido de navegador verificado en español e inglés; verificador independiente sin bloqueos por el riesgo de seguridad.

Risk: item 2 (datos de usuario y cambio de credenciales). T1 y T2 requieren verificación independiente tras sus checks propios.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S2, S4 | Inline | Planificada | Usuario extendido + migración aditiva + endpoints de perfil/contraseña + login ampliado + pruebas |
| T2 | S1, S3, S4 | Inline | Planificada | Depende de T1 |
| T3 | S2, S5 | Inline | Planificada | Depende de T1/T2 |

Forecast inicial: aproximadamente 700–1000 líneas authored entre implementación, recursos de cadenas, pruebas y documentación; archivos generados excluidos. Estrategia `feature-branch-chain`; el umbral de ~400 líneas es heurístico, no un límite de tamaño.

## Log

### L1 — Solicitud original (2026-10-17)

Ayudame con un nuevo requerimiento. el login está en español y el contenido interno en ingles, quiero que en la pagina de login haya un dropdown para seleccionar el idioma entre español e ingles. de igual manera el usuario logueado deberia tener una pagina de perfil donde pueda agregar nombre, idioma y permita cambiar la contraseña. y que segun el idioma se muestre el contenido

### L2 — Decisión de alcance de localización (2026-10-17)

Texto exacto del usuario: “UI completa, API sin cambiar (recomendado)”. Toda la interfaz en español/inglés; datos sintéticos del HCM y problem details de la API sin localizar.

### L3 — Aprobación para planificación (2026-10-17)

Texto exacto del usuario: “Aprobar SPEC-006 y ADR-0008 (recomendado)”. Se aprueban `specs/006-user-profile-localization/spec.md` y `docs/architecture/adr/ADR-0008-ui-localization-and-user-profile.md` para planificar; esto no autoriza cambios de código hasta autorizar tareas.


### L4 — Implementación de T1 (2026-10-17)

Texto exacto del usuario: “Autorizar T1 (recomendado)”.

Implementado: `ApplicationUser` (subclase de `IdentityUser` con `FullName` maxLength 100 y `PreferredLanguage` maxLength 5, por defecto `es`); `TalentAssistantIdentityDbContext` configura ambos con `HasDefaultValue` de base de datos; DI, inicializador y login migrados a `ApplicationUser`; login ampliado con nombre e idioma; `/auth/me` devuelve el perfil completo; nuevos endpoints `POST /auth/profile` (valida el catálogo cerrado `es`/`en`, trim del nombre) y `POST /auth/change-password` (exige la contraseña actual); catálogo `SupportedLanguages` en la API; pruebas nuevas de perfil, contraseña y catálogo.

Hallazgo y corrección: la primera versión de la migración añadía `PreferredLanguage NOT NULL` sin default de base de datos, lo que falla en MySQL sobre tablas con filas (y el DDL no transaccional dejó un estado parcial con solo `FullName` aplicado). Se repararon las bases dev y de pruebas (drop de columnas parciales y fila de historial), se regeneró la migración como `20261017002940_AddUserProfile` con longitudes y `defaultValue: "es"`, y se verificó sobre el usuario existente.

Evidencia: build 0 warnings/0 errores; prueba enfocada 16/16; integración 23/23; UnitTests 4/5 con el bloqueo ambiental conocido (Application Control 0x800711C7 sobre `TalentAssistant.Domain.dll` en `UnitTests\bin`; mismo archivo carga en IntegrationTests) y grep del padre confirma cero referencias a Identity en Domain/Application. Smoke completo: login devuelve `fullName`/`preferredLanguage`, `/auth/me` el perfil, guardado con `en` OK, idioma `fr` → 400, contraseña actual errónea → 400, cambio y restauración de contraseña OK, asistencia con token sigue devolviendo 5 fixtures. La contraseña del usuario local se restauró al valor documentado en `.env`.

Risk: item 2 (datos de usuario y cambio de credenciales).
### L5 — Verificación independiente de T1 (2026-10-17)

El verificador independiente validó los cinco claims sin bloqueos en scratch propio (MySQL desechable en puerto 3307 y API en 5115; el stack del usuario y la base dev no se tocaron): alcance del diff acotado con cero referencias a Identity en Domain/Application; migración aditiva ejecutada en vivo con prueba de reaplicación sobre tabla poblada (backfill de `es`, identidad y hash intactos); comportamiento completo de perfil/contraseña/idioma (trim, catálogo cerrado incluido el caso sensible `ES` → 400, política de Identity, contraseña vieja → 401 tras el cambio); integración 23/23 desde el workspace y también desde rebuild scratch; 0 secretos en el diff.

Advisories: bloqueo ambiental App Control 0x800711C7 en UnitTests (reproducido incluso fuera del workspace; firma idéntica a la conocida) y codificación UTF-8 inválida en este logbook causada por `Add-Content`/`Set-Content` de PowerShell 5.1 en ANSI; reparada con reescritura UTF-8 y verificación estricta de todos los Markdown del repo. El verificador propuso un test de regresión de reaplicación de migración; se difiere porque muta el esquema de la base compartida de pruebas y podría ejecutarse en paralelo con las pruebas de autenticación.