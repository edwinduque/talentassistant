# SPEC-015 — Despliegue a producción en la VPS (systemd + nginx, sin Docker)

- **Estado:** Propuesto (2026-10-21, borrador para aprobación).
- **Rama de implementación (propuesta):** `feat/production-deploy` (base: `feat/ci-pipeline`).
- **Requisitos relacionados:** sin FR/NFR nuevo; concluye la intención del usuario: «como nuevo requerimiento debo ir pensando en publicar mi sitio en produccion».
- **Decisión arquitectónica:** ninguna nueva (topología de despliegue, no de aplicacíón); se registra como decisión de producto/aproximación en esta spec.
- **Decisión de producto:** el usuario confirmó que los servicios .NET existentes en la VPS corren como **binarios systemd detrás de nginx**; se sigue ese patrón (coherente con «quiero hacer ci/cd sin afectar esos sitios» y con no instalar Docker solo para la aplicación).
- **Versión:** 0.1.

## 1. Objetivo

Desplegar Web y API de Talent Assistant en la VPS como servicios systemd detrás del nginx existente, con CI verde obligatorio previo, despliegue por SSH desde GitHub Actions (runner alojado), release-atómico con symlink y rollback, migraciones EF aplicadas idempotemente y la **API 100% privada (solo loopback)**: el único punto público es la Web.

## 2. Alcance

### Incluido

1. **Workflow `deploy.yml`** (trigger `workflow_dispatch` solamente; nunca automático):
   - **Environment `production`** con protección de revisor humano — GitHub pide aprobación al dueño antes de ejecutar.
   - Dependencia del CI: el job exige que la suite esté verde en el commit lanzado (pin SHA del commit despachado; el pipeline de CI corre primero en el mismo ref).
   - `dotnet publish` de Web y API (framework-dependiente, linux-x64, ReadyToRun desactivado) → empaquetado → subida por SSH/SCP.
   - Ejecución remota sobre `/opt/talentassistant`: patrón `releases/<timestamp>` + symlink `current` + unidades systemd (`talentassistant-api.service` en `127.0.0.1:5101`, `talentassistant-web.service` en `127.0.0.1:5102`, puertos ajustables); `daemon-reload`, restart y health-probe local (`GET /health` y `/health/database`); si falla, symlink vuelve a la release anterior y queda rojo el run.
   - El primer boot (o reintentos) ejecutan el inicializador idempotente `--initialize-local-data` (migraciones EF + usuario demo de siembra, nunca resemeando contraseña).
2. **Secretos en el Environment `production`** (nunca en el repo): `SSH_HOST`, `SSH_PORT`, `SSH_USER`, `SSH_PRIVATE_KEY` — máscara de protección de secrets.
3. **Plantillas versionadas** (aplicadas por el usuario, no por el pipeline): unidad systemd con endurecimiento (`DynamicUser`/usuario de servicio no-root, `NoNewPrivileges`, `PrivateTmp`, `ProtectSystem=strict`), server block de nginx nuevo y conjunto de comandos `certbot --nginx -d <dominio-nuevo>`; el pipeline no toca nginx ni certificados.
4. **Base de datos de producción:** NUEVA base `talentassistant_prod` + rol de menor privilegio en el postgres EXISTENTE de la VPS (colisiones con las BD y roles actuales: ninguna); la cadena vive en `.env.production` en la VPS (chmod 600, propietario de servicio) y jamás se versiona.
5. **Documentación:** guía operativa (`docs/deployment/production.md` en español): estructura `/opt/talentassistant`, unidades, nginx, certbot, `.env.production` (no sube), rollback y checklist de seguridad de la VPS (fail2ban, SSH solo-clave, ufw 22/80/443, backups del postgres).

### Fuera de alcance

- CI (ya implementada por SPEC-014); modificarla solo en lo mínimo necesario referentea `deploy.yml`.
- Cambios al nginx/certificados de los sitios EXISTENTES: solo un server block nuevo para el dominio nuevo.
- TLS interno (ginx→Kestrel por loopback http interno).
- Backups automatizados, monitoreo y alertas (quedan como próximos pasos documentados).
- Renombrar el dominio o la migración del HCM real: la base del HCM real (si el llega) es decisión del DBA con SPEC-011 §10 vigente.

## 3. Requisitos y límites

- **Nada automático sin aprobación humana**: `workflow_dispatch` + Environment con revisor; el push a main NUNCA despliega solo.
- **API inalcanzable desde Internet**: ambos servicios bindean SOLO a loopback; el nginx oficial proxy_pass internamente a la Web; la API solo la alcanza la Web (regla de topología confirmada con SPEC-013: Blazor server-side llama la API desde el proceso del servidor).
- **Sin secretos en el repo ni en el run**: la salida del CI/CD no imprime valores; el `SSH_PRIVATE_KEY` es una clave de despliegue dedicada (usuario con permiso solo sobre `/opt/talentassistant`, no root).
- **El despliegue es reproducible y reversible**: firmado por el SHA del commit; symlink atómico; rollback automático a la release previa ante health-check fallido.
- **Secretos del HCM real** (si se activa `Hcm__Source=Postgres` en producción) se exigen con `Ssl Mode=VerifyFull` (SEC-001/ADR-0013) y viven solo en `.env.production` de la VPS.
- El pipeline no ejecuta comandos de nginx/certbot/systemd-owner más allá del servicio propio de la aplicación; el resto son pasos manuales documentados del usuario.

## 4. Verificación

- El workflow espera la aprobación del Environment antes de ejecutarse (evidencia: run pendiente de aprobación en GitHub).
- Tras el deploy: `GET /health` y `/health/database` en loopback de la VPS, la Web accesible por el dominio nuevo con HTTPS, la API SIN puerto abierto externo (`ss -tlnp | grep 5101` muestra `127.0.0.1`).
- Migraciones: `talentassistant_prod` con esquema completo tras el primer arranque, sin borrar las BD existentes.
- Rollback probado una vez (desplegar la versión previa o simular un fallo de health y verificar el symlink regresado).

## 5. Riesgos y supuestos

- El repo pasará a público: la topología, las plantillas systemd/nginx y la guía quedan públicos pero contienen solo nombres sintéticos y placeholders — sin dominios reales, IPs, usuarios ni credenciales.
- `SSH_PORT`/usuario/ puerto SSH de la VPS: valores en secrets; el pipeline no hardcodea nada.
- Los ports 5101/5102 en la VPS deben estar libres (supuesto: sí; se validan en la guía con `ss -tlnp`).
- La clave de firma JWT de producción (`AUTH_SIGNING_KEY`) vive en `.env.production`; si se pierde, los tokens emitidos dejan de validar (documentado, sin rotación automática).

## 6. Orden de implementación (propuesto)

1. Plantillas systemd + nginx + guía operativa (docs).
2. `deploy.yml` con Environment + SSH + health/rollback.
3. Checklist manual del usuario en la VPS (BD nueva, roles, `.env.production`, server block, certbot).
4. Primer despliegue supervisado junto al usuario (executes manual), con la verificación §4 como evidencia de cierre.
