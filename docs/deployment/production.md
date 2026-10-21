# Guía de despliegue a producción (SPEC-015)

Guía operativa exclusiva de la VPS de producción descrita en SPEC-015: **systemd + nginx, sin Docker**. Los demás sitios que ya corren en la VPS no cambian nada de su configuración; Talent Assistant es completamente adyacente: usuario, servicios, puertos, base de datos y server block son nuevos y propios.

## 1. Verificaciones previas en la VPS

```bash
# Los puertos 5101/5102 deben estar libres (si no, se ajustan en la unidad y en nginx)
ss -tlnp | grep -E ':(5101|5102)\b' && echo "PUERTO OCUPADO; ajustar" || echo "libres"
dotnet --list-sdks        # runtime ASP.NET Core 10 instalado, o instálolo
```

- Nada más conflicte: la aplicación NO reutiliza credenciales, roles, bases, certificados ni unidades de los sitios existentes.

## 2. Instalación única (la hace el dueño de la VPS, una vez)

### 2.1 Usuario de despliegue (sin root para trabajar, sudo mínimo acotado)

```bash
sudo adduser talentassistant        # usuario de servicio; su clave se usa SOLO para este despliegue
sudo mkdir -p /opt/talentassistant
sudo chown talentassistant:talentassistant /opt/talentassistant
```

El despliegue solo necesita reiniciar SUS dos servicios; nada más. El `sudoers` acotado (en `/etc/sudoers.d/talentassistant-deploy`, creado con `visudo -f`):

```
talentassistant ALL=(root) NOPASSWD: /usr/bin/systemctl restart talentassistant-api
talentassistant ALL=(root) NOPASSWD: /usr/bin/systemctl restart talentassistant-web
```

### 2.2 Servicios systemd (plantillas del repo)

```bash
sudo cp deploy/systemd/talentassistant-api.service.tpl /etc/systemd/system/talentassistant-api.service
sudo cp deploy/systemd/talentassistant-web.service.tpl /etc/systemd/system/talentassistant-web.service
sudo systemctl daemon-reload
```

Las unidades endpoints: `EnvironmentFile=/opt/talentassistant/.env.production`, `User=talentassistant`, endurecimiento activo (`NoNewPrivileges`, `PrivateTmp`, `ProtectSystem=strict`), bindean SOLÓ `127.0.0.1` (vía `ASPNETCORE_URLS` del archivo de entorno). Si las NUEVAS web/api FK de producción deben liberar otros puertos, se cambia en el archivo de entorno, no en la unidad.

### 2.3 Base de datos de producción (nueva, en el postgres EXISTENTE)

```sql
-- conectado como administrador del postgres de la VPS; NO toca las BD existentes
CREATE ROLE talentassistant_prod LOGIN PASSWORD '<contraseña-sintética-única>';
CREATE DATABASE talentassistant_prod OWNER talentassistant_prod;
\c talentassistant_prod
ALTER SCHEMA public OWNER TO talentassistant_prod;
```

`talentassistant_prod` es dueño SOLO de su base. Las migraciones EF (Aplicadas en concreto por el pipeline, idempotentes) crean el esquema completo en el primer arranque.

### 2.4 `/opt/talentassistant/.env.production` (chmod 600, solo TU VPS; nunca versionado)

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__TalentAssistant=Host=127.0.0.1;Port=5432;Database=talentassistant_prod;Username=talentassistant_prod;Password=<tu-contraseña>;
BackendApi__BaseUrl=http://127.0.0.1:5101/
Authentication__SigningKey=<clave-JWT-de-producción-≥-32-caracteres>
Authentication__SeedUserName=<usuario-demo-admin>
Authentication__SeedPassword=<contraseña-inicial-demo; solo para primera siembra>
Attendance__Source=Dataset
Hcm__Source=            # vacío = sintético; 'Postgres' SOLO con la contraseña real en Ssl Mode=VerifyFull (SEC-001)
```

Los puertos de escucha (`ASPNETCORE_URLS`) NO viven aquí: están fijados en cada plantilla de unidad systemd (`Environment=` por unidad en loopback). Si algún día se necesita cambiar un puerto, se edita la unidad + `daemon-reload`. Las credenciales reales NUNCA salen de la VPS y el pipeline únicamente las consume en el servidor; jamás las imprime.

### 2.5 nginx: server block nuevo + certificado

```bash
sudo cp deploy/nginx/talentassistant.conf.tpl /etc/nginx/sites-available/talentassistant.conf
sudo sed -i 's/TALENTASSISTANT_DOMAIN/<tu-dominio-nuevo>/' /etc/nginx/sites-available/talentassistant.conf
sudo ln -sf /etc/nginx/sites-available/talentassistant.conf /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d <tu-dominio-nuevo>          # genera y renueva el TLS solo de ESTE server block
```

El server block SOLO toca este dominio: los certificados y proxies de tus sitios actuales no se tocan.

## 3. Qué hace el pipeline en cada despliegue (`.github/workflows/deploy.yml`)

Despliegue manual obligatorio: **el Environment `production` pide aprobación del dueño antes de relasear cada ejecución**; ninguna rama despliega automáticamente. El commit compilado es el despachado. Secuencia:

1. `dotnet publish` de API y Web (Release, framework-dependiente, linux-x64).
2. Subida del paquete por SSH (clave de despliegue dedicada en Environment secrets; `SSH_KNOWN_HOSTS` verificado: tú la generas con `ssh-keyscan` en tu VPS y la pegas en el secret; **halt StrictHostKeyChecking queda desactivado**).
3. En la VPS: extracción a `/opt/talentassistant/releases/<timestamp>`, migraciones idempotentes con tu `.env.production`, pre-check de salud en un puerto de sondeo PRIVADO (5199) sin tocar el servicio en producción ni tus demás sitios.
4. Symlink atómico `current` → release nueva, reinicio de solo SUS DOS servicios, y comprobación en vivo de `/health` y `/health/database`.
5. Si el health-check en vivo falla: **rollback automático** al symlink de la release anterior + reinicio.
6. Secrets del Environment: `SSH_HOST`, `SSH_PORT`, `SSH_USER`, `SSH_PRIVATE_KEY`, `SSH_KNOWN_HOSTS` — el SSH se genera con `ssh-keyscan` de tu VPS y se pega en el secret; nunca se desactiva `StrictHostKeyChecking`.

## 4. Checklist de post-despliegue (§4 de SPEC-015)

- [ ] `GET /health` y `GET /health/database` en `127.0.0.1:5101` responden healthy.
- [ ] El dominio nuevo responde por HTTPS y los sitios existentes siguen intactos.
- [ ] `ss -tlnp | grep 5101` muestra `127.0.0.1:5101` (la API NO escucha en una IP pública).
- [ ] `psql` en la VPS: la base `talentassistant_prod` tiene las tablas EF; las demás bases sin cambios.
- [ ] El SSH usa clave (sin `PasswordAuthentication`), fail2ban activo, `ufw` solo 22/80/443.
- [ ] Backups: `pg_dump talentassistant_prod` programado (cron en la VPS o tu método).

## 5. Rollback manual (si hiciera falta de forma independiente)

```bash
cd /opt/talentassistant
ln -sfn "$PWD/releases/<timestamp-anterior>" current
sudo systemctl restart talentassistant-api
sudo systemctl restart talentassistant-web
```

Las releases antiguas quedan en directorios; se limpian manualmente (conserva al menos dos).

## 6. Seguridad: recordatorio global

- La web pública (443) es la ÚNICA superficie de la aplicación; la API no se publica.
- Los secretos de producción nunca viven en el repo (SPEC-009/011: la clave de IA es opcional y degrada controladamente).
- Swagger en producción: `ASPNETCORE_ENVIRONMENT=Production` lo mantiene fuera.
- La retención de secretos también aplica aquí: `deploy.yml` usa exclusivamente los secrets del Environment y jamás los imprime; gitleaks (SPEC-014) cubre el historial del repo.