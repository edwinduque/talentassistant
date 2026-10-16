# ADR-0007 — Autenticación local con ASP.NET Core Identity y JWT

- **Estado:** Propuesto (2026-10-16).
- **Fecha:** 2026-10-16.
- **Aprobación de arquitectura:** pendiente de aprobación del usuario junto con SPEC-005.
- **Especificación relacionada:** SPEC-005 — Autenticación local para Web y API.

## Contexto

Hasta ahora, API y Web se ejecutan localmente sin autenticación: el acceso queda limitado a desarrollo local con datos sintéticos (decisión registrada al cerrar SPEC-001 y reforzada por ADR-0006, que restringe el perfil contenedorizado a desarrollo local sin autenticación).

El usuario solicitó un login para la interfaz Web y, por consiguiente, para la API. Se eligió el modelo “Identity local + MySQL”: identidades locales en la misma base de datos, sin servicios externos.

## Decisión propuesta

- La API es la dueña de la identidad: registra ASP.NET Core Identity con su propio `DbContext` y una migración aditiva en el mismo servidor MySQL; Domain y Application no conocen Identity ni tablas de usuarios.
- El login se expone como endpoint de la API, que valida con Identity (incluido bloqueo por intentos fallidos) y emite un JWT firmado de forma simétrica con una clave provista por configuración externa.
- La API exige token en los endpoints de negocio; las sondas `/health` y `/health/database` permanecen públicas por ser comprobaciones de estado del entorno.
- Web no accede a la base de datos: autentica contra la API mediante su contrato HTTP, conserva la sesión en una cookie protegida con Data Protection y adjunta el token en `BackendApiClient`.
- Los usuarios son locales y sintéticos: un usuario sembrado de forma idempotente por el inicializador existente, con contraseña proveniente solo de `.env` o User Secrets; sin contraseñas por defecto en el código.
- La autorización de esta fase es “usuario autenticado”; los roles y permisos granulares quedan para una especificación posterior.
- La autenticación aplica a los perfiles host y Compose por igual: es una propiedad de la aplicación, no del perfil de ejecución.

## Impacto en decisiones vigentes

- ADR-0001 (monolito modular), ADR-0002 (datos sintéticos), ADR-0003 (límite del servicio de IA) y ADR-0005 (CQRS ligero) no cambian: la identidad es una responsabilidad de entrega detrás de los contratos existentes.
- ADR-0004 (MySQL/EF Core) se extiende de forma natural: las tablas de Identity usan el mismo servidor y proveedor, con su propia migración aditiva; el dataset de asistencia de SPEC-004 no se modifica.
- ADR-0006 limitaba el perfil local contenedorizado a “desarrollo local sin autenticación”. SPEC-005 modifica ese supuesto para el perfil local: el stack contenedorizado pasará a incluir autenticación. El despliegue productivo, TLS y redes compartidas siguen fuera de alcance.
- El aplazamiento “Autenticación y despliegue de producción quedan fuera de SPEC-001” sigue vigente para SPEC-001; esta especificación habilita autenticación local, no despliegue productivo.

## Consecuencias esperadas

### Positivas

- La demostración local deja de ser de acceso abierto en el equipo: Web y API exigen sesión.
- Identity aporta hash de contraseñas, bloqueo por intentos y validación probados, en lugar de lógica de seguridad propia.
- La autorización queda en el backend y la Web sigue siendo un cliente sin acceso a datos.

### Compromisos

- Se añaden tablas de Identity, una migración y una dependencia de Identity EF Core.
- Hay secretos nuevos obligatorios en configuración externa (clave de firma, contraseña sembrada); si faltan, la aplicación debe fallar con error controlado.
- Sin revocación server-side: cerrar sesión descarta el cliente, y la expiración del token es el control de duración.
- La rotación de claves de Data Protection al recrear el contenedor Web invalida cookies previas; es una limitación local aceptada y documentada.
- El flujo de pruebas se amplía con pruebas de autenticación sobre la base de pruebas dedicada.

## Alternativas consideradas

- **Usuario único de configuración:** una credencial fija de entorno sin tablas de usuarios. Más simple, pero no aporta identidad por usuario, bloqueo ni estándar; se descarta por no ser un login real.
- **Proveedor externo de identidad:** login corporativo real. Requiere servicios externos, registro de aplicación, credenciales y red saliente; queda fuera de los límites MVP (local, sintético, sin datos reales).
- **Identity alojado en Web:** la Web tendría la base de usuarios y la API confiaría en ella. Rompe el principio de autorización en el backend y duplica responsabilidades; se descarta.
- **Cookie de sesión directa entre Web y API:** sin JWT. Obliga a compartir contexto de sesión entre dos aplicaciones en procesos distintos; se descarta por acoplamiento.

## Verificación de la decisión

La decisión se considerará implementada cuando AC-016..AC-018 de SPEC-005 tengan evidencia observada: pruebas de login/logout/401, inspección de secretos y hash, suite completa en verde y smoke de navegador. La aprobación de ADR-0007 autoriza planificar; la implementación se ejecuta por tareas del plan ODD.
