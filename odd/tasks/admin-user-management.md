# Feature: admin-user-management (SPEC-016)

Objetivo: usuario sembrado con rol **admin** y páginas/endpoint para crear usuarios (solo admin), autorización validada en el backend. Para que personas del equipo de producto prueben.
Branch: `feat/admin-user-management` (base: `feat/production-deploy`). Entrega: `ask-on-risk`. Runner: full suite (NFR/AC existentes).

## Specs

- **S1** (usuario, verbatim): «quiero tener un usuario administrador y que con ese usuario pueda crear usuarios para que algunas personas puedan probar». Todo el alta pasa por >un< usuario admin; los demo-invites se crean desde la app.
- **S2**: el seeder local (`InitializeLocalAuthenticationData`) crea el rol `admin` y se lo asigna al usuario de siembra, idempotente (usuarios preexistentes reciben el rol al re-corrida; no reescribe contraseñas).
- **S3**: el JWT incluye claims de rol; `LoginResponse` trae `Roles`; `/auth/me` también los expone → para pruebas y el menú web.
- **S4**: endpoints de admin (API): `GET /admin/users` (lista: id/usuario/nombre/idioma) y `POST /admin/users` (alta; email+contraseña+nombre+idioma+flag admin opcional), con **autorización en el backend** (requiere role `admin`; sin sesión 401; no-admin 403). Errores de política de Identity → problem details 400. Sin exponer hashes ni datos sensibles.
- **S5**: Web: página `/admin/users` (listar + crear, en Blazor Server, con el JWT de sesión), `@attribute [Authorize(Roles = "admin")]`, menú «Usuarios» visible solo si el usuario está en el rol; recursos localizados ES/EN con paridad (SPEC-006/009).
- **S6**: desplegable con el pipeline existente: **sin migración nueva de esquema** (las tablas de roles de Identity ya existen); el deploy re-corre el seeder → la producción recibe el rol y la página. Non-admin demo guests no pueden ver la página (403/redirect).
- **S7 (fuera de alcance)**: desactivar/borrar usuarios, reset de contraseñas en masa, roles adicionales (spec futura).

## Tasks

| ID | Specs | Ruta | Estado | Commit |
|---|---|---|---|---|
| T1 | — | inline | completado | `odd/tasks/admin-user-management.md` + specs/016 + RED (4 fallos como se esperaba) |
| T2 | S2,S3 | inline | completado | `feat: seed admin role and backend-controlled /admin/users api (SPEC-016)` (con T3) |
| T3 | S4 | inline | completado | (el mismo commit; 4/4 RED→GREEN observados) |
| T4 | S5 | inline | completado | `feat: web admin page for users with localized menu (SPEC-016)` |
| T5 | S6 | inline | pendiente | — deploy supervisado a producción (seeder aplica el rol al admin existente) |

## Log

- **L1** (2026-10-23, verbatim): «quiero tener un usuario administrador y que con ese usuario pueda crear usuarios para que algunas personas puedan probar» (y «disculpa continua»). Feature autorizada tras ofrecerla como SPEC-016.
- **L2** (2026-10-23): exploración: `AccessTokenService` (sub+uniqueName sin roles) en Api/Authentication; `/session/login` del Web construye claims a mano (nombre/accesos/idioma) — el rol se hereda vía claims JWT → cookie; seeder idempotente ya encuentra usuario; Identity registre con `IdentityRole` disponible.
- **L3** (2026-10-23): política test-first: primero los tests de integración RED (endpoint inexistente hoy → 404): (a)usuario sembrado ES admin y `GET /admin/users` responde 200; (b)`POST /admin/users` crea un usuario y el nuevo puede loguear; (c)no-admin obtiene 403. NuGrey: los construye sobre el banco de `ChatHttpContract` factories. Excepción NONE: aplica RED/GREEN.
