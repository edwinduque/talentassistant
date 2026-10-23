# SPEC-005 — Autenticación local para Web y API

- **Estado:** Propuesto para aprobación de planificación (2026-10-16). Nota de herencia (2026-10-21): Identity persiste sobre PostgreSQL ([ADR-0013](../../docs/architecture/adr/ADR-0013-postgresql-persistence.md)); las menciones a MySQL describen la decisión en su momento.
- **Implementación:** T1, T2 y T3 implementadas; suite 21/21 y smoke de navegador end-to-end verificados; pendiente el cierre/merge del feature.
- **Requisitos relacionados (propuestos):** FR-021, FR-022 y NFR-018. Relacionados existentes: FR-007, FR-009, FR-010; NFR-001, NFR-004, NFR-006, NFR-007.
- **Criterios relacionados (propuestos):** AC-016, AC-017, AC-018.
- **Decisión arquitectónica:** ADR-0007 propuesto.
- **Decisión de producto registrada:** el usuario eligió “Identity local + MySQL (recomendado)” el 2026-10-16.
- **Versión:** 0.1.

## 1. Objetivo

Añadir un login real a la aplicación: la interfaz Web y la API quedan protegidas por una sesión iniciada por el usuario, con identidades locales almacenadas en la misma MySQL y sin depender de servicios externos ni de datos reales.

El objetivo NO es convertir Talent Assistant en un sistema de gestión de identidades: es cerrar el acceso a la demostración local con el patrón estándar de ASP.NET Core.

## 2. Alcance propuesto

### Incluido

- ASP.NET Core Identity en la API, con usuarios persistidos en MySQL (hash de contraseñas, bloqueo por intentos fallidos).
- Endpoints de autenticación en la API: login, logout y consulta de usuario autenticado.
- Endpoints de negocio protegidos: sin sesión válida responden HTTP 401 controlado. `/health` y `/health/database` permanecen públicos por ser sondas de estado del entorno.
- Páginas de la interfaz Web protegidas, con página de login y acción de cierre de sesión visibles.
- Semilla idempotente de un usuario sintético local; la contraseña proviene solo de configuración externa (`.env`/User Secrets), nunca del repositorio.
- Migración EF Core aditiva con las tablas estándar de Identity en `talentassistant_dev`, sin tocar el dataset de asistencia existente.
- Actualización de la guía local, README, registro de decisiones y frases documentales que hoy declaran que el MVP no incluye autenticación.

### Fuera de alcance

- Registro de usuarios, recuperación o cambio de contraseñas, envío de correo.
- Autenticación multifactor, refresh tokens, revocación server-side de sesiones.
- Matrices de roles y permisos granulares: la autorización de esta especificación es “usuario autenticado”.
- Proveedor externo de identidad (Entra ID, Google, etc.).
- Terminación HTTPS/TLS, rate limiting público, CAPTCHA, auditoría persistente, endurecimiento productivo.

## 3. Requisitos y límites arquitectónicos

- La API es la dueña de la identidad. Domain y Application no referencian Identity, tablas de usuarios ni tokens: se conservan NFR-001 y NFR-002, y la autorización permanece en el backend (principio 6 de la visión y ADR-0003).
- Web sigue sin acceso a base de datos: se autentica contra la API mediante su contrato HTTP, igual que hoy consume los insights (FR-007).
- Los secretos de autenticación (clave de firma del token, usuario y contraseña sembrados) se entregan solo por `.env` o User Secrets, sin estar en el repositorio ni en las imágenes (NFR-004, NFR-018).
- La semilla de usuarios es idempotente, no modifica el dataset de asistencia de SPEC-004 y no escribe en `talentassistant_tests` por defecto (límite S4 de SPEC-004).
- Los fallos de login son controlados: una única respuesta 401 idéntica para usuario inexistente y contraseña incorrecta, sin enumerar usuarios, sin stack traces y sin registrar credenciales (FR-009, NFR-006, NFR-007).

## 4. Flujo local esperado

1. El desarrollador configura en `.env` (perfil Compose) o User Secrets (perfil host) la clave de firma, el usuario y la contraseña sembrados.
2. Al abrir Web, las páginas protegidas redirigen a `/login`.
3. El usuario introduce sus credenciales; Web las envía a la API mediante una llamada servidor-a-servidor.
4. La API valida con Identity (incluido bloqueo por intentos) y, si son correctas, devuelve un token de sesión firmado.
5. Web guarda la sesión del navegador y las llamadas posteriores a la API incluyen el token; sin token, la API responde 401.
6. El cierre de sesión limpia la sesión del navegador y vuelve a exigir login.
7. Las sondas `/health` y `/health/database` siguen respondiendo sin sesión.

## 5. Componentes candidatos

| Componente | Responsabilidad propuesta | Línea base que debe conservar |
|---|---|---|
| API — endpoints de autenticación | Login, logout y usuario autenticado; emisión y validación del token. | Minimal API existente; respuestas controladas. |
| API — pipeline | Autenticación JWT y autorización de endpoints de negocio. | `/health` público; negocio protegido. |
| Infrastructure — identidad | `TalentAssistantIdentityDbContext`, migración aditiva y seed idempotente de usuarios. | Mismo servidor MySQL; sin tocar el dataset de asistencia. |
| Web — login | Página `/login`, estado de autenticación y cierre de sesión. | Blazor Web App interactive server; sin acceso a BD. |
| Web — cliente API | Adjuntar el token a las llamadas existentes de `BackendApiClient`. | Sin CORS, comunicación servidor-a-servidor. |
| Configuración | Variables nuevas de autenticación en `.env.example` y guía local. | Secretos solo fuera de Git. |
| Pruebas | Pruebas unitarias del token y pruebas de integración del flujo de login y de respuestas 401. | Suite existente en verde; base de pruebas separada. |

## 6. Usuarios y credenciales sintéticos

- Un usuario sembrado por defecto, con nombre configurable (`Authentication:SeedUserName`, por ejemplo `demo@talentassistant.local`) y contraseña provista por configuración externa.
- Si no se configura contraseña de siembra, la API no crea usuarios y lo comunica con un error controlado al iniciar el inicializador; no existe ninguna contraseña por defecto en el código.
- La siembra es idempotente: si el usuario ya existe no se crea de nuevo y no se sobreescribe su contraseña (operación documentada para actualizarla).
- Las contraseñas se almacenan solo como hash con el mecanismo de Identity; no se guardan ni se transmiten en claro por el repositorio, imágenes o logs.
- Los datos de identidad son locales y sintéticos; no hay conexión con directorios corporativos.

## 7. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Evidencia futura |
|---|---|---|
| AC-016 | Con el stack local arrancado y el usuario sembrado, el login en Web funciona: redirige a `/login`, autentica, muestra las páginas protegidas y cierra la sesión. Sin sesión, la API responde HTTP 401 en `/attendance/insights`. | Pruebas de integración de login/401 y prueba manual de navegador con logout. |
| AC-017 | Las credenciales son seguras en el flujo local: contraseñas solo como hash, secretos solo en configuración externa, login fallido con respuesta genérica idéntica para usuario inexistente o contraseña errónea, y bloqueo tras intentos repetidos. | Pruebas automatizadas de login fallido/bloqueo, inspección del diff y de la imagen, y revisión de logs sin credenciales. |
| AC-018 | La autenticación no rompe lo existente: perfil host y perfil Compose siguen operativos, la suite completa permanece verde, el dataset de asistencia y su semilla no cambian y no se introduce ningún servicio externo ni dato real. | Suite completa, smoke de insights autenticado y comparación del dataset antes/después. |

AC-016..AC-018 son propuestas y no se declaran aceptadas hasta aprobar SPEC-005 y ADR-0007.

## 8. Diseño propuesto para planificar

1. Identity se registra en la API con su propio contexto (`TalentAssistantIdentityDbContext`) y su migración aditiva; usa el mismo servidor MySQL que la aplicación.
2. El endpoint de login valida con Identity (incluido bloqueo por intentos fallidos) y emite un token JWT firmado de forma simétrica con una clave provista por configuración externa.
3. La API exige token en los endpoints de negocio; `/health` y `/health/database` permanecen públicos como sondas del entorno.
4. Web se autentica contra la API por contrato HTTP: la sesión del navegador se conserva en una cookie protegida con Data Protection y el token se adjunta a las llamadas de `BackendApiClient`.
5. La siembra de usuarios se incorpora al inicializador one-shot existente, conservando su idempotencia y su modo de ejecución.
6. La guía local documenta configuración, login, cierre de sesión y limitaciones conocidas del perfil local.

Supuestos que esta propuesta deja explícitos para aprobación:

- La autenticación aplica también al perfil host, no solo a Compose: es una propiedad de la aplicación, no del perfil.
- Hay un único usuario sembrado y la autorización es “usuario autenticado”; los roles quedan para una especificación posterior.
- Duración de sesión propuesta: 60 minutos, configurable; sin refresh token.
- Bloqueo propuesto: 5 intentos fallidos con bloqueo temporal, configurable.
- Tras recrear el contenedor Web, las cookies de sesión anteriores dejan de ser válidas por rotación de claves de Data Protection; se documenta como limitación local.

## 9. Plan SDD propuesto

1. Añadir FR-021, FR-022 y NFR-018 a los catálogos de requisitos, y AC-016..AC-018 a los criterios, con su trazabilidad.
2. Aceptación de ADR-0007 y actualización del registro de decisiones, junto con las frases de SPEC-001/ADR-0006/guía local que hoy declaran la ausencia de autenticación.
3. Rama de feature `feat/local-authentication` y plan ODD con tareas acotadas (identidad en Infrastructure; API de login/token y endpoints protegidos; login y sesión en Web; pruebas, guía y verificación).
4. Verificación propuesta: suite completa, pruebas nuevas de autenticación, smoke de navegador con login/logout y verificador independiente por el riesgo de seguridad.
5. Cierre con matriz requisito/criterio/evidencia y commits por work unit.

## 10. Estado y aprobación

SPEC-005 y ADR-0007 quedan propuestos a la espera de aprobación para planificación. La aprobación no autoriza por sí sola cambios de código: cada tarea se autoriza después según el plan ODD.

El aplazamiento vigente “Autenticación y despliegue de producción: quedan fuera de SPEC-001” del registro de decisiones sigue siendo cierto para SPEC-001; al implementar esta especificación, el MVP local sí dispondrá de autenticación y el despliegue de producción seguirá fuera de alcance.

## 11. Verificación de T3

T3 actualiza los artefactos documentales y de configuración siguientes:

- `.env.example`: variables `AUTH_SIGNING_KEY`, `AUTH_SEED_USER`, `AUTH_SEED_PASSWORD` y `AUTH_TOKEN_LIFETIME_MINUTES` con comentarios.
- `compose.yaml`: `db-init` y `api` cablean `Authentication__*` desde esas variables; `web` no necesita cambios.
- `docs/development/local-setup.md`: sección «Autenticación local» (configuración, siembra idempotente, login/logout, token de 60 minutos, bloqueo y limitaciones) y perfiles Compose/host actualizados.
- `README.md`: ejecución local y estado actual con la salvedad de suite de T2.
- `docs/decisions/decision-register.md`: fila ADR-0007 y aplazamiento de autenticación/despliegue actualizado.
- `docs/architecture/adr/ADR-0006-containerized-local-development.md`: nota de sustitución de la restricción «sin autenticación» por ADR-0007.
- `docs/requirements/functional-requirements.md` (FR-021, FR-022) y `docs/requirements/non-functional-requirements.md` (NFR-018).
- `docs/acceptance/mvp-acceptance-criteria.md`: AC-016..AC-018 y sus filas de trazabilidad.

Verificación de T3: `git diff --check` y `docker compose config --quiet` pasaron; commits `6574b37` (docs/Compose/catálogos) y `1aed991` (evidencia).

El smoke de navegador end-to-end (Playwright) contra el stack Compose validó AC-016 completo: `/` anónimo redirige a `/login` sin errores de consola; el login con el usuario sembrado entra a la home (usuario y "Cerrar sesión" visibles); Attendance insights muestra los 5 hallazgos con oficinas, reglas HCM, evidencia y contexto TimeOff (token Web→API); el logout vuelve a `/login`. La suite completa quedó en 21/21.

El smoke cazó y se corrigieron tres defectos (commit `a03c255`): el `EditForm` sin `FormName` devolvía 400 al enviar, el manejo en prerender carecía de `IJSRuntime`, y la FallbackPolicy retaba `/_blazor/negotiate` impidiendo el circuito a usuarios anónimos. La solución final usa formulario HTML nativo con POST a `/session/login` (redirect solo a rutas locales), el script de sesión en `App.razor`, y una FallbackPolicy por aserción que exime `/_blazor` y exige usuario autenticado en todo lo demás. La antiforgery del login queda deshabilitada como tradeoff documentado del perfil local (SameSite=Lax, solo loopback).
