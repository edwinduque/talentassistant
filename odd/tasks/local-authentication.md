# Seguimiento ODD: autenticación local

Objetivo: proteger Web y API con un login real usando identidades locales en MySQL (ASP.NET Core Identity en la API, JWT para la API y sesión en Web), sin servicios externos ni datos reales.
Rama: `feat/local-authentication`; entrega: `feature-branch-chain` (misma estrategia elegida para SPEC-004); runner: SDK `10.0.402`, `dotnet build`, `dotnet test`, smoke Compose y navegador; revisión nativa desactivada globalmente (`disabled/unmanaged`).

## Specs

### S1 — Identidad y sesión en la API

Alcance: la API registra Identity con su propio contexto y migración aditiva en MySQL, expone login/logout/consulta de usuario, emite JWT firmado con clave de configuración externa y exige token en los endpoints de negocio; las sondas de salud permanecen públicas.

Texto exacto del usuario: “Quiero crear un login a la app web y por ende la api tambien”.

Aceptación: login válido devuelve token; `/attendance/insights` sin token responde 401 controlado y con token responde igual que antes; login fallido devuelve una única respuesta genérica idéntica para usuario inexistente y contraseña errónea, sin enumerar usuarios ni exponer detalles; el bloqueo por intentos queda activo y configurable; `/health` y `/health/database` siguen públicos.

### S2 — Interfaz Web protegida

Alcance: página `/login`, estado de autenticación en el circuito, cierre de sesión visible y adjunción del token a las llamadas existentes de `BackendApiClient`; Web sigue sin acceso a base de datos.

Texto exacto del usuario: “Quiero crear un login a la app web y por ende la api tambien”.

Aceptación: sin sesión, las páginas protegidas redirigen a `/login`; con credenciales correctas se accede y los insights se muestran igual que antes; el cierre de sesión limpia la sesión y vuelve a exigir login; el token nunca se escribe en logs ni en el HTML fuera de la cookie protegida.

### S3 — Usuarios y secretos sintéticos

Alcance: siembra idempotente de un usuario local a partir de configuración externa; sin contraseña por defecto; secretos solo en `.env`/User Secrets.

Texto exacto del usuario: “hay que asegurar la aplicación”.

Aceptación: el inicializador crea el usuario solo si no existe y no sobreescribe su contraseña; sin contraseña de siembra configurada, no se crea ningún usuario y se informa con error controlado; no hay credenciales en repo, imágenes ni logs; las contraseñas quedan solo como hash.

### S4 — Límites arquitectónicos

Alcance: la identidad vive detrás de los contratos existentes; Domain y Application no conocen Identity; el dataset de asistencia de SPEC-004 y la base de pruebas separada no cambian.

Aceptación: ninguna referencia de Domain/Application a Identity ni a tablas de usuarios; autorización aplicada en el backend; el dataset de asistencia y su semilla idempotente quedan intactos; `talentassistant_tests` no recibe usuarios de demostración.

### S5 — Documentación, requisitos y verificación

Alcance: guía local con login/logout y nuevas variables, catálogos de requisitos y criterios con los IDs nuevos, registro de decisiones y frases obsoletas de "MVP sin autenticación" actualizadas.

Texto exacto del usuario: “todo manejado con sdd”.

Aceptación: FR-021, FR-022, NFR-018 y AC-016..AC-018 añadidos con trazabilidad; guía reproducible desde checkout limpio; suite completa en verde; smoke de navegador con login/logout; verificador independiente sin bloqueos por el riesgo de seguridad; sin servicios externos añadidos.

Risk: item 2 (autenticación, credenciales y permisos). T1 y T2 requieren verificación independiente tras sus checks propios.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1, S3, S4 | Inline | Implementada; suite 21/21, smoke login/401/token/logout y verificación independiente sin bloqueos (5/5 claims MET) | `2e89c719ae4e6c18c45b662801b8fcc0284e3783` + snapshot `ab715c16bf62786194eb1126b45b98f95db5ca01` |
| T2 | S2, S4 | Inline | Implementada; build 0/0 e integración 16/16; UnitTests 4/5 por bloqueo ambiental App Control en `UnitTests\bin` (bytes idénticos OK en otra ruta); verificador pendiente | Commit pendiente en `feature-branch-chain` |
| T3 | S3, S5 | Inline | Planificada | Depende de T1/T2 |

Forecast inicial: aproximadamente 700–1000 líneas authored entre implementación, pruebas y documentación; archivos generados excluidos. Estrategia `feature-branch-chain` reutilizada de SPEC-004; el umbral de ~400 líneas es heurístico, no un límite de tamaño.

## Log

### L1 — Solicitud original (2026-10-16)

tengo un nuevo requerimiento. hay que asegurar la aplicación. Quiero crear un login a la app web y por ende la api tambien. ayudame con este requerimiento todo manejado con sdd

### L2 — Decisión de modelo de autenticación (2026-10-16)

Texto exacto del usuario: “Identity local + MySQL (recomendado)”. Identidades locales en la misma MySQL con ASP.NET Core Identity, sin servicios externos.

### L3 — Aprobación para planificación (2026-10-16)

Texto exacto del usuario: “Aprobar SPEC-005 y ADR-0007 (recomendado)”. Se aprueban `specs/005-local-authentication/spec.md` y `docs/architecture/adr/ADR-0007-local-authentication.md` para planificar; esto no autoriza cambios de código hasta autorizar tareas.

### L4 — Implementación y verificación de T1 (2026-10-16)

Texto exacto del usuario: “Autorizar T1 (recomendado)”. Descarga de paquetes autorizada: “Sí, autorizo esos dos paquetes” (Identity EF Core y JwtBearer 10.0.13).

Implementado: `TalentAssistantIdentityDbContext` + migración aditiva `20261016202119_AddIdentityTables`; siembra idempotente de usuario desde configuración externa (sin contraseña por defecto; aplica su migración antes de sembrar); endpoints `/auth/login` (401 genérico idéntico para usuario inexistente y contraseña errónea, con bloqueo), `/auth/logout` y `/auth/me`; JWT HS256 con clave de `Authentication:SigningKey` (≥32 chars, obligatoria para servir); `/attendance/insights` exige token y `/health*` sigue público.

Hallazgo y corrección: `AddIdentity` instala la cookie Identity como esquema de desafío por defecto, por lo que los endpoints protegidos redirigían a `/Account/Login` (404 aparente). Se fijó JwtBearer como esquema por defecto; el smoke confirmó 401 sin token, 200 con token y 401 con token alterado.

Evidencia: build 0 warnings/0 errores; prueba enfocada 9/9; suite completa 21/21 exit 0; inicializador aplicó la migración de Identity en `talentassistant_dev` y sembró un usuario (dataset de asistencia intacto, 1 fila); smoke login/logout/me/token contra API host en puerto efímero. Red de pruebas: 5 unit + 16 integración.

Risk: item 2 (autenticación y credenciales: política de contraseñas heredada de Identity, bloqueo y manejo de secretos).

### L5 — Verificación independiente de T1 (2026-10-16)

Verificador independiente sin bloqueos, 5/5 claims MET en copia scratch y puertos libres (5107-5109), sin tocar el stack principal: diff acotado (sin cambios en Domain/Application ni referencias a Identity), migración aditiva verificada con dumps byte-idénticos del dev DB, posture de seguridad (clave vacía en repo, fail-fast antes de abrir puerto, 401 idéntico en los tres casos de login inválido, hash solo, 0 secretos en logs), comportamiento completo (initializer idempotente ×2, insights 401/200 con 5 fixtures byte-idénticas a la respuesta baseline, token alterado 401, logout 200) y suite 21/21 leída desde el workspace. Advisory: la política Application Control bloquea la carga de `TalentAssistant.Domain.dll` desde rutas temp del scratch (0x800711C7); no es defecto del código. Advisory: el reto 401 del framework lleva cuerpo vacío frente al problem JSON del login fallido. El token sigue válido hasta expirar tras logout (compromiso documentado en ADR-0007).

### L6 � Implementaci�n de T2 (2026-10-16)

Texto exacto del usuario: "si" (autoriza T2). Escrita por worker acotado (context backstop); el writer escal� un fallo real del dise�o original (el POST en loopback desde el circuito no puede entregar la cookie HttpOnly al navegador) y se corrigi� con fetch same-origin del navegador v�a IJSRuntime, conservando el contrato JSON `/session/*`.

Implementado en Web: cookie HttpOnly SameSite=Lax con login path `/login`; FallbackPolicy autenticado con `AllowAnonymous` en assets est�ticos, `/login` y `/session/login`; `AuthApiClient` contra la API; `AuthenticationMessageHandler` adjuntando el token solo al `BackendApiClient`; `AuthorizeRouteView`+`CascadingAuthenticationState`; p�gina `/login` accesible en espa�ol; usuario y "Cerrar sesi�n" en MainLayout.

Evidencia: build 0 warnings/0 errores; integraci�n 16/16; UnitTests 4/5. El fallo es ambiental y reproducido por el padre: `DomainAndApplicationRespectTheirDependencyBoundaries` falla con FileLoadException 0x800711C7 (Application Control) cargando `TalentAssistant.Domain.dll` desde `tests\TalentAssistant.UnitTests\bin`, con SHA256 id�ntico al que s� carga desde `IntegrationTests\bin`; se limpi� bin/obj y reconstruy�, se reproduce igual; el diff de T2 no toca Domain/Application y la misma suite pas� 21/21 antes de T2. No se desactiva ni elude la pol�tica. El smoke de navegador end-to-end queda para T3.

Risk: item 2 (sesi�n y transporte de credenciales en Web).

### L7 � Correcci�n del blocker de T2 (2026-10-16)

El verificador detect� que el 401 vac�o de `/session/login` con contrase�a err�nea lo capturaba `UseStatusCodePagesWithReExecute("/not-found")` y sal�a como 400. Corregido devolviendo un Problem JSON con cuerpo (las p�ginas de estado solo act�an sobre respuestas sin cuerpo). Recheck limitado del padre: `POST /session/login` con contrase�a err�nea ? 401; `/login` ? 200. Commits: correcci�n en esta entrada.
