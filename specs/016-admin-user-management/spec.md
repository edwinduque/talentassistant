# SPEC-016 — Administración de usuarios (rol admin en la app)

- **Estado:** Aprobado e implementado (2026-10-23); despliegue en producción via SPEC-015 pendiente del run.
- **Rama:** `feat/admin-user-management` (base `feat/production-deploy`).
- **Decision de producto (verbatim usuario):** “quiero tener un usuario administrador y que con ese usuario pueda crear usuarios para que algunas personas puedan probar” (demo del equipo de producto; datos 100% dummy, sin datos de la compañia — SEC y AGENTS vigentes).
- **Versión:** 0.2.

## 1. Objetivo

Un usuario administrador (el sembrado) puede crear usuarios desde la propia app para dar accesos al equipo de producto. La autorización vive en el backend; la Web administra con el JWT de sesión.

## 2. requisitos y límites (resumen ejecutivo)

- El seeder crea el rol `admin` y lo asigna al usuario de siembra, idempotente (la producción EXISTENTE recibe el rol al re-corriembre el deploy), nunca reescribe contraseñas.
- API: `GET /admin/users` y `POST /admin/users` (policy backend `AdminOnly` = rol). Acceptación: sin sesión 401; sin rol 403; alta con validación de Language y errores de Identity como problem details 400.
- JWT: claims `role` estándar (mapa inbound ClaimTypes.Role); `LoginResponse.Roles` y `/auth/me` los exponen.
- Web: `/admin/users` (listar + alta) con `[Authorize(Roles="admin")]`; nav «Usuarios» solo para admin; recursos ES/EN con paridad (SPEC-006/009).
- Sin nueva migración de esquema (tablas Identity de roles ya existen). Fuera de alcance: baja/desactivación de usuarios y más roles (spec futura).
- Datos: nunca hashes/correlaciones sensibles fuera del backend; alta de demo con contraseñas políticas (mayús/minús/digito/símbolo, mínimo 6). Cualquier chequeo de datos personales reales: N/A (todo demo).

## 3. verificación

- RED→GREEN observados: `AdminUserManagementTests` (4 facts) — 0/4 al inicio (3× HTTP 404 + rol ausente) y **4/4 en verde** tras la implementación (commit `4ffc347`).
- Suite completa desde BD vacía: **136/136 integración + 11/11 unitarias** (commit `7db6a11` + log ODD).
- Production: al ejecutar el deploy del pipeline, el seeder asigna el rol al admin existente y la página sube; se verifica con el login del admin y el alta de un invitado.

## 4. registro de decisiones observadas

- La asignación del rol se hace en el SEEDER (no en el deploy script): única fuente de verdad de la identidad de demo; scripts de pipeline no tocan datos.
- El nombre de la dirección es text-level 'employee-'||e.id y 'timeoff-'||t.id si se presta a la producción también — fuera de aquí (ver SPEC-012/demol).
