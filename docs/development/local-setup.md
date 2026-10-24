# Ejecución local

Esta guía ofrece el perfil completo Docker Compose para API, Web y PostgreSQL, además del perfil alternativo que ejecuta API/Web en el host. Ambos usan exclusivamente respuestas HCM sintéticas y exigen iniciar sesión (SPEC-005); el entorno no está preparado para exponerse a redes compartidas ni para un despliegue productivo.

## Requisitos

- Docker Desktop en modo de contenedores Linux y Docker Compose v2.
- Acceso a `mcr.microsoft.com` y `api.nuget.org` durante el primer build de imágenes.
- Para el perfil host y las pruebas: Windows con .NET SDK `10.0.402`; `global.json` fija esa versión sin roll-forward.

## Perfil completo con Docker Compose

Ejecuta estos pasos desde la raíz del repositorio. Compose fija el nombre `talentassistant-local`, por lo que no hace falta pasar `-p`.

### 1. Crear la configuración local

```powershell
Copy-Item .env.example .env
notepad .env
```

Completa `APP_POSTGRES_ADMIN_PASSWORD` y `APP_POSTGRES_PASSWORD` con valores sintéticos locales distintos; conserva `APP_POSTGRES_DB=talentassistant_dev`. Completa también `AUTH_SIGNING_KEY` y `AUTH_SEED_PASSWORD` según la sección «Autenticación local»; `docker compose config` rechaza valores vacíos. `.env` está excluido de Git y del contexto Docker; no lo compartas ni uses credenciales de producción. Si ya existe el volumen propia de la aplicación, conserva las contraseñas con las que se inicializó: editarlas en `.env` no cambia las credenciales almacenadas en ese volumen.

### 2. Construir e iniciar los servicios

```powershell
docker compose config --quiet
docker compose up --build -d --wait
docker compose ps
```

El primer build restaura las dependencias declaradas y requiere acceso a los registros indicados arriba. Compose espera al servicio propio `app-postgres` saludable, ejecuta `db-init` (migraciones y upsert transaccional del dataset sintético) y luego inicia API y Web. La API del perfil Compose lee desde PostgreSQL (base propia en el servicio `app-postgres`); Web consume API mediante la red interna. El inicio y los puertos publicados quedan limitados a loopback: API `http://127.0.0.1:5101`, Web `http://127.0.0.1:5102` y la base propia `127.0.0.1:5434` (la base del contrato HCM permanece en `127.0.0.1:5433`).

### 3. Comprobar API, dataset y Web

```powershell
Invoke-RestMethod http://127.0.0.1:5101/health
Invoke-RestMethod http://127.0.0.1:5101/health/database
$items = Invoke-RestMethod "http://127.0.0.1:5101/attendance/insights?from=2026-10-15&to=2026-10-18"
"Fixtures: $($items.Count)"
(Invoke-WebRequest http://127.0.0.1:5102/assistant -UseBasicParsing).StatusCode
(Invoke-WebRequest http://127.0.0.1:5102/_framework/blazor.web.js -UseBasicParsing).StatusCode
```

Se esperan respuestas saludables, cinco fixtures y HTTP `200` para ambas rutas Web y el asset de Blazor.

### 4. Repetir la inicialización

```powershell
docker compose run --rm db-init
```

La operación puede repetirse: aplica migraciones pendientes y reemplaza la única fila del dataset completo, sin duplicar registros ni borrar el volumen. `talentassistant_tests` permanece separada y no recibe la semilla demo.

### Detener, conservar o restablecer datos

```powershell
docker compose stop
docker compose start
docker compose down
```

`stop`, `start` y `down` conservan el volumen nombrado `talentassistant_app_postgres_data`. **`docker compose down --volumes` es destructivo:** elimina el volumen y las bases locales de desarrollo e integración; úsalo solo cuando se decidió descartar esos datos. Cambiar `.env` no cambia las credenciales de un volumen ya inicializado. El volumen heredado `talentassistant_mysql_data` ya no pertenece al perfil: descártalo con `docker volume rm`.

Para consultar logs sin mostrar valores de `.env`:

```powershell
docker compose logs --tail 100 app-postgres api web
```

El perfil no incluye TLS y no debe exponerse a redes compartidas ni usarse como despliegue productivo.

## Autenticación local

Desde SPEC-005, Web y API exigen iniciar sesión. No existe ninguna credencial por defecto en el repositorio: completa en tu `.env` local (nunca versionado) estas variables antes de arrancar:

| Variable | Valor esperado |
|---|---|
| `AUTH_SIGNING_KEY` | Clave de firma JWT aleatoria de al menos 32 caracteres. Obligatoria: la API falla con un error controlado antes de abrir puerto si falta. |
| `AUTH_SEED_PASSWORD` | Contraseña del usuario sembrado. Obligatoria para `db-init`; debe cumplir la política de Identity: mayúscula, minúscula, dígito y carácter no alfanumérico, con mínimo 6 caracteres. |
| `AUTH_TOKEN_LIFETIME_MINUTES` | Duración del token JWT en minutos. Opcional: `60` por defecto. |

`AUTH_SEED_USER` es opcional y por defecto vale `demo@talentassistant.local`.

### Siembra de usuarios

`db-init` aplica las migraciones (incluidas las tablas de Identity) y crea el usuario de siembra solo si no existe. Nunca sobreescribe la contraseña de un usuario ya existente. Para cambiar la contraseña, borra la fila del usuario con SQL en PostgreSQL (por ejemplo, con `docker compose exec app-postgres psql -U talentassistant_admin -d talentassistant_dev`) y vuelve a ejecutar la inicialización:

```powershell
docker compose run --rm db-init
```

### Login, cierre de sesión y limitaciones

1. Abre `http://127.0.0.1:5102`; sin sesión, las páginas protegidas redirigen a `/login`.
2. Introduce el usuario y la contraseña sembrados. Web envía las credenciales a la API (`POST /auth/login`) y guarda la sesión en una cookie HttpOnly.
3. «Cerrar sesión» en la interfaz cierra la sesión del navegador y vuelve a exigir login; la API expone `/auth/logout` y `/auth/me`.

Limitaciones conocidas:

- El token JWT caduca a los 60 minutos (`AUTH_TOKEN_LIFETIME_MINUTES`) y no hay refresh token. No hay revocación server-side: tras cerrar sesión, un token ya emitido sigue siendo válido hasta su expiración.
- Tras 5 intentos fallidos la cuenta se bloquea temporalmente (configurable). El login fallido devuelve siempre la misma respuesta 401 genérica, sin indicar si el usuario existe.
- Tras recrear el contenedor Web, las cookies de sesión anteriores dejan de ser válidas por rotación de claves de Data Protection.

Las sondas `GET /health` y `GET /health/database` siguen públicas por ser comprobaciones de estado del entorno; los endpoints de negocio (`/attendance/insights`) exigen token. La autenticación es una propiedad de la aplicación: aplica igual en el perfil host y en Compose.

### Idioma y perfil

Desde SPEC-006 la interfaz se muestra por completo en español o inglés según la elección del usuario. El idioma por defecto es español y no se detecta el idioma del navegador.

1. En `/login` hay un selector de idioma que funciona sin sesión: fija la cookie de cultura mediante `POST /session/culture` y recarga la página. La elección persiste tras recargar y tras cerrar sesión.
2. Al iniciar sesión, la sesión hereda el idioma preferido guardado en el perfil del usuario; el login devuelve el nombre visible y el idioma junto al token.
3. La página `/profile` (autenticada) permite editar el nombre visible, el idioma preferido y cambiar la contraseña. El cambio de contraseña exige la contraseña actual y aplica la política de Identity descrita en la tabla de `AUTH_SEED_PASSWORD`. Al guardar el perfil, la interfaz aplica el nombre y el idioma nuevos de inmediato.
4. Cambiar el idioma —en el login o en el perfil— recarga la página; no es una conmutación en vivo.

El alcance de la localización de SPEC-006 es la interfaz: los títulos de error (problem details) de la API permanecen en su idioma original por decisión de alcance. Desde SPEC-007 el contenido sintético del HCM también sigue el idioma del usuario: reglas, evidencias, fichajes, TimeOff, nombres, países, tipos y estados se muestran en español o inglés según el idioma preferido del perfil, mientras que identificadores, fechas, horas, ciudades y zonas horarias son idénticos en ambos idiomas.

Tras actualizar a SPEC-007 hay que reemplazar el dataset por el payload bilingüe versión 2: ejecuta `docker compose run --rm db-init` (Compose lo ejecuta automáticamente en `up --build`).

## Perfil host alternativo: configurar PostgreSQL y ejecutar API/Web en Windows

Esta variante mantiene API y Web fuera de Docker. El perfil host/tests conserva la fuente HCM en memoria por defecto; no necesita ni ejecuta `db-init`.

1. Copia `.env.example` a `.env` y completa los dos campos de contraseña vacíos con valores sintéticos distintos. Compose rechazará los valores vacíos.
2. Mantén el rol de aplicación `talentassistant_app`: el script inicial concede a ese rol acceso a las bases del servicio; asigna contraseñas locales distintas para `APP_POSTGRES_ADMIN_PASSWORD` y `APP_POSTGRES_PASSWORD`; no uses contraseñas de producción.
3. Arranca solo la base propia para el perfil host:

   ```powershell
   docker compose up -d --wait app-postgres
   docker compose ps
   ```

4. El perfil host también exige autenticación (SPEC-005): la API necesita `Authentication__SigningKey` (>= 32 caracteres) y `Authentication__SeedPassword` como variables de entorno (`Authentication__*`) o en User Secrets antes de ejecutarse; sin la clave de firma, la API no arranca. El usuario local se crea con el inicializador `--initialize-local-data`, el mismo que ejecuta `db-init` en Compose.

La base propia se sirve con la imagen oficial `postgres:17-alpine` y un script de bootstrap (`docker/postgres/init/10-app-databases.sh`) que crea las bases `talentassistant_tests` y el rol de aplicación en el primer inicio de un volumen vacío. Compose publica el puerto únicamente en `127.0.0.1:5434` y conserva los datos en el volumen nombrado `talentassistant_app_postgres_data`.

Los scripts de `/docker-entrypoint-initdb.d` solo se ejecutan al inicializar un directorio de datos vacío. Si el volumen ya existía antes de añadir el script, crea la base y el propietario manualmente con una sesión autenticada de PostgreSQL:

```sql
CREATE DATABASE talentassistant_tests;
ALTER DATABASE talentassistant_tests OWNER TO talentassistant_app;
ALTER DATABASE talentassistant_dev OWNER TO talentassistant_app;
ALTER SCHEMA public OWNER TO talentassistant_app;
```

## Configurar la API y aplicar migraciones

La cadena de desarrollo se guarda fuera de Git mediante User Secrets. Ejecuta desde la raíz del repositorio y reemplaza el marcador por el valor local de `APP_POSTGRES_PASSWORD`:

```powershell
dotnet user-secrets set "ConnectionStrings:TalentAssistant" "Host=127.0.0.1;Port=5434;Database=talentassistant_dev;Username=talentassistant_app;Password=<APP_POSTGRES_PASSWORD>;" --project src/TalentAssistant.Api/TalentAssistant.Api.csproj
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj
```

El proyecto API tiene un `UserSecretsId`; `WebApplication.CreateBuilder` carga User Secrets en `Development`. `.env` lo lee Docker Compose, **no** el proceso .NET. Para la API también se puede usar `ConnectionStrings__TalentAssistant` como variable de entorno. No hay una contraseña real configurada en los archivos versionados.

### Comprobación reproducible de AC-006

Con el servicio propio `app-postgres` saludable y el puerto local `5101` libre, ejecuta:

```powershell
.\scripts\verify-database-secret-redaction.ps1
```

El script genera un centinela sintético por ejecución, lo entrega al proceso API solo mediante una variable de entorno y consulta `/health/database`. Espera HTTP 503 con un error genérico ante el rechazo de autenticación, y comprueba que ni la respuesta ni la salida capturada contengan el centinela o un stack trace. La salida capturada temporal se elimina al terminar; el script nunca imprime el valor. No ejecuta xUnit, no mueve ensamblados y no modifica la política de Application Control.

La aplicación mantiene sus migraciones explícitas; en el perfil host no se aplican automáticamente al arrancar la API. La migración consolidada `InitialCreate` (base `Migrations/` de Infrastructure) crea la tabla técnica que usa el perfil Compose. La generación de una migración de aplicación usa:

```powershell
dotnet ef migrations add <NombreMigracion> --project src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj
```

## Ejecutar la API

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5101"
dotnet run --no-launch-profile --project src/TalentAssistant.Api/TalentAssistant.Api.csproj
```

El endpoint mínimo es `GET http://127.0.0.1:5101/health`. `GET /health/database` comprueba la conexión y devuelve HTTP 503 genérico si la base no está disponible, sin exponer la cadena ni el error interno. No se configura CORS porque cualquier llamada Web→API se realizará desde el servidor.

## Ejecutar Blazor Web

Con la API activa en `127.0.0.1:5101`, inicia Blazor en otra terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5102"
$env:BackendApi__BaseUrl = "http://127.0.0.1:5101/"
dotnet run --no-launch-profile --project src/TalentAssistant.Web/TalentAssistant.Web.csproj
```

La página inicial consulta `GET /health` mediante `BackendApiClient`, registrado como cliente HTTP tipado del servidor. `BackendApi:BaseUrl` tiene un valor local de desarrollo y `BackendApi__BaseUrl` lo sobrescribe por entorno. Web no referencia Infrastructure ni se conecta directamente a PostgreSQL.

## Restaurar, compilar y probar

```powershell
dotnet restore TalentAssistant.sln
dotnet build TalentAssistant.sln --configuration Debug
dotnet test tests/TalentAssistant.UnitTests/TalentAssistant.UnitTests.csproj
```

Las pruebas unitarias no requieren base de datos. Las pruebas de integración usan exclusivamente la base `talentassistant_tests`; no usan EF Core InMemory. El modelo de prueba `PersistenceProbeContext` tiene su propia migración versionada en `tests/TalentAssistant.IntegrationTests/Migrations/`, separada del esquema de la aplicación, y la prueba la aplica automáticamente con `MigrateAsync`.

Configura la cadena de la base de pruebas y ejecuta la suite completa:

```powershell
$env:ConnectionStrings__TalentAssistantTests = "Host=127.0.0.1;Port=5434;Database=talentassistant_tests;Username=talentassistant_app;Password=<APP_POSTGRES_PASSWORD>;"
dotnet test TalentAssistant.sln --configuration Debug --no-restore
```

```powershell
$env:HcmPostgres__ConnectionString = "Host=127.0.0.1;Port=5433;Database=talentassistant_hcm;Username=talentassistant_hcm_app;Password=<HCM_POSTGRES_APP_PASSWORD>;"
```

La autenticación de PostgreSQL usa SCRAM-SHA-256 por defecto: no necesita parámetros adicionales; el puerto solo está publicado en `127.0.0.1`. La ejecución de ensamblados xUnit puede estar sujeta a las políticas del equipo; no se deben desactivar ni eludir.

La prueba crea su esquema en `talentassistant_tests` en la primera ejecución y hace rollback de los registros que inserta; no recibe la semilla demo de `talentassistant_dev`. Solo hace falta regenerar la migración si cambia el modelo de prueba, usando el propio proyecto de pruebas como startup project (EF Core localiza el DLL del contexto en la salida de ese proyecto; con la API como startup no lo encuentra):

```powershell
dotnet ef migrations add <NombreMigracion> --project tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --startup-project tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --context PersistenceProbeContext
```

Para detener únicamente la base propia en el perfil host, usa `docker compose stop app-postgres`; para volver a iniciarla, `docker compose start app-postgres`. El flujo completo de persistencia y reset del perfil Compose está en la sección anterior.

## Conector HCM (PostgreSQL, solo consulta)

Desde SPEC-011 la aplicación puede consultar el HCM real desde su PostgreSQL de solo lectura tras los puertos existentes (ADR-0012). El interruptor único `Hcm__Source` gobierna a la vez todos los puertos de datos —directorio, presencia, ausencias, tareas y asistencia—: o todo lee de Postgres o todo de la fuente sintética; no existe modo mixto y una fuente desconocida o una cadena vacía en modo Postgres bloquean el arranque con un error controlado. El valor por defecto es `Sintética` y no cambia ningún comportamiento existente.

### Modos y variables

| Variable | Valor esperado |
|---|---|
| `Hcm__Source` | `Sintética` (por defecto) o `Postgres`. Gobierna ambos puertos a la vez. |
| `HcmPostgres__ConnectionString` | Cadena Npgsql contra la base del HCM (`Host`, `Puerto`, `Base`, `Usuario`, `Clave`, `Ssl Mode=VerifyFull`). Obligatoria en modo Postgres; solo en tu `.env` local. |
| `HcmPostgres__CommandTimeoutSeconds` | Tiempo de espera por comando en segundos (15 por defecto). |
| `HCM_POSTGRES_DB` / `HCM_POSTGRES_USER` / `HCM_POSTGRES_PASSWORD` / `HCM_POSTGRES_APP_PASSWORD` | Variables del servicio local de pruebas `hcm-postgres`, con valores por defecto solo-locales en `.env.example`; no son la cadena de la base real. |

### Servicio local de pruebas (`hcm-postgres`)

El perfil Compose incluye un servicio `postgres:17-alpine` publicado **solo en loopback** (`127.0.0.1:5433`) que aplica el contrato de `db/hcm-contract/` sobre tablas de sustitución y lo siembra con filas sintéticas en el primer inicio de un volumen vacío. La base real del HCM nunca se toca desde este servicio y el servicio no lleva TLS por diseño: es una excepción local documentada, no la postura de la base real.

Para restablecer por completo la base de pruebas del contrato:

```powershell
docker compose rm -sf hcm-postgres && docker volume rm talentassistant_hcm_postgres_data && docker compose up -d hcm-postgres
```

### Réplica local del shape real (SPEC-012)

Desde SPEC-012, `hcm-postgres` sirve el contrato sobre una réplica local con la **forma real del HCM** (tablas `public.employee`, `timeoff_type`, `timeoff`, `working_day`, `working_day_assigment`, `time_check`, `time_check_delay`), regada con un seed dummy determinista; las vistas `hcm_contract.*` son la única capa que la aplicación conoce y los adaptadores no cambiaron.

| Fichero | Qué contiene |
|---|---|
| `db/hcm-real/upstream.sql` | DDL del usuario **verbatim** (2026-10-20). No se ejecuta: es la fuente de verdad del shape cambiante. |
| `db/hcm-real/schema.sql` | Adaptación ejecutable a PostgreSQL: identidades deterministas, `timestamp` (`time_check_date_utc` como `timestamptz`), `smallint`, `boolean`, `text`, `"job:title"` entrecomillado; añade 9 FK locales y 5 índices de apoyo (ambos documentados como añadidos locales en su cabecera). |
| `db/hcm-real/seed.sql` | Semilla dummy determinista, 100 % ficticia y sin `random()`: todo procede de `generate_series`, aritmética de índice y constantes. Se ejecuta una sola vez por volumen; no es idempotente a propósito. |

Orden del pipeline de init (`db/hcm-contract/local/01_init.sh`, con `ON_ERROR_STOP=1`): `/real/schema.sql` → `/real/seed.sql` → `local_tables.sql` (sustitución reducida) → `views.sql` → `local_seed.sql` (filas de sustitución) → `roles.sql` → contraseña del rol de aplicación desde el entorno → `grants.sql`. Al terminar imprime el marcador `hcm contract initialized`.

**Cuando el usuario comparta un shape nuevo** (la información puede cambiar; misma revisión, sin atajos):

1. Reemplaza `db/hcm-real/upstream.sql` por el nuevo DDL verbatim.
2. Adapta `db/hcm-real/schema.sql` al nuevo shape conservando nombres de tablas y columnas.
3. Revisa `db/hcm-contract/views.sql` **y** `db/hcm-real/seed.sql` en el MISMO cambio: las vistas y la semilla dependen del shape.
4. Reinicializa el volumen con el comando de restablecimiento de la sección anterior (borra el volumen local de pruebas, sintético por definición) y comprueba el marcador de init, los conteos y la suite completa.

Convenciones del shape (SPEC-012):

| Convención | Detalle |
|---|---|
| `timeoff.date_end` es inclusivo | El contrato expone el exclusivo `ends_before = date_end + 1 día`. |
| Vocabulario de `status_id` | `1` pendiente, `2` activo, `3` eliminado (confirmado por el usuario). |
| `department` como unidad orgánica | La columna dobla como identificador de unidad orgánica del contrato (marcador de alcance abajo). |
| `time_check.status_id` | Su mapeo por valor es un supuesto documentado en la cabecera del seed. |
| Días en UTC | Los límites de día de las marcaciones se evalúan en UTC. |
| Ausencias que cubren hoy | Horizonte rodante a +365 días para no degradar la paridad sin reinicializar. |
| Determinismo | Por día civil UTC; el seed es one-shot (re-inicializar el volumen lo regenera). |

Alcance y gaps documentados (no corregidos):

- El catálogo servido está acotado a los nueve empleados de fixture mediante el marcador `department LIKE 'office-demo-%'`; ampliarlo es retirar ese filtro en la vista, una línea, más la evolución de las pruebas.
- El shape real **no expone tabla de tareas ni tabla de hallazgos de regla evaluados**: las vistas de tareas e insights siguen leyendo la sustitución legada (`hcm.tasks`, `hcm.attendance_incidents`) hasta que el usuario comparta esos DDLs; la aplicación nunca deriva reglas del HCM.

### Cómo obtienen las pruebas la cadena de conexión

La suite completa necesita dos cadenas por entorno: `ConnectionStrings__TalentAssistantTests` (PostgreSQL, ya documentada arriba) y `HcmPostgres__ConnectionString` contra el servicio local. El patrón correcto obtiene la contraseña desde el entorno del contenedor **sin imprimirla en el repositorio ni en la conversación**:

```powershell
$pgPass = docker compose exec -T hcm-postgres printenv HCM_POSTGRES_APP_PASSWORD
$env:HcmPostgres__ConnectionString = "Host=127.0.0.1;Port=5433;Database=talentassistant_hcm;Username=talentassistant_hcm_app;Password=$pgPass;"
dotnet test TalentAssistant.sln --configuration Debug --no-restore
```

Las pruebas se conectan como rol de aplicación de solo lectura (`talentassistant_hcm_app`, creado por `db/hcm-contract/roles.sql`); ningún test contacta la base real.

### Degradación en modo Postgres

Si la base no responde en modo Postgres, los endpoints de datos responden HTTP 503 con el error localizado `{"reason":"SourceUnavailable"}`; la aplicación **nunca** vuelve a datos sintéticos de forma silenciosa y no reintenta a ciegas. El modo sintético y la suite siguen intactos con la base apagada.

### Checklist para la base real del HCM (DBA)

Antes de apuntar la aplicación a la base real:

- [ ] El DBA crea (o acuerda) las vistas del contrato de `db/hcm-contract/views.sql` sobre el esquema real, o se fija un conjunto de tablas/columnas igualmente estable mediante el paso de descubrimiento documentado en SPEC-011.
- [ ] Se revisa la aplicabilidad de `db/hcm-contract/roles.sql` y `grants.sql`: rol de aplicación de solo lectura con `SELECT` exclusivamente sobre las vistas, sin grants sobre tablas físicas.
- [ ] `HcmPostgres__ConnectionString` incluye `Ssl Mode=VerifyFull` para la base real; el loopback de pruebas sin TLS no es la norma y el arranque exige `VerifyFull` en cualquier host no-loopback (validación automática desde SEC-001,2026-10-19).
- [ ] No se reutilizan las credenciales por defecto del servicio local de pruebas (`talentassistant_hcm_*`) en la base real: se generan credenciales únicas (SEC-002,2026-10-19).
- [ ] En tu `.env` local (nunca versionado) quedan `Hcm__Source=Postgres`, la cadena anterior y, si hace falta, `HcmPostgres__CommandTimeoutSeconds`.
- [ ] Cualquier prueba en vivo del asistente con personas reales exige antes la decisión de egreso de SPEC-011 §10.

**Aviso de secretos:** las credenciales de la base del HCM se colocan solo en tu `.env` local; nunca en el chat, en el repositorio, en imágenes ni en logs.

## Consultas del asistente (API)

Desde SPEC-008, la API expone cuatro endpoints de consulta —las herramientas que el asistente conversacional de SPEC-009 llamará— que exigen sesión (sin token válido responden HTTP 401) y responden en el idioma del perfil del usuario autenticado:

| Endpoint | Parámetros | Contenido |
|---|---|---|
| `GET /employees` | `name`, `office` | Directorio; búsqueda por nombre insensible a mayúsculas y acentos (enmienda SPEC-008: «Juan Perez» localiza a «Juan Pérez») y por oficina; los identificadores de oficina se comparan exactamente. |
| `GET /presence` | `date` (`yyyy-MM-dd`, por defecto hoy) | Estado de presencia informado por la fuente para la fecha; fecha inválida → HTTP 400, otras fechas → lista vacía. |
| `GET /timeoff` | `employeeId` o `name` | Solicitudes de TimeOff del empleado; sin filtro → toda la colección; varios nombres coincidentes → elementos de todos. |
| `GET /tasks` | `employeeId` o `name` | Tareas del empleado; misma semántica de filtro que `/timeoff`. |

Obtén un token con `POST /auth/login` (usuario y contraseña sembrados en tu `.env`) y úsalo como bearer:

```bash
# 1. Login: guarda el "token" del cuerpo de la respuesta
curl -s -X POST http://127.0.0.1:5101/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"demo@talentassistant.local","password":"<TU_PASSWORD>"}'

# 2. Consulta con el token (sustituye <TOKEN> por el valor recibido)
curl -s http://127.0.0.1:5101/presence -H "Authorization: Bearer <TOKEN>"
```

Sustituye `<TU_PASSWORD>` por la contraseña local de tu `.env`; nunca la versiones ni la compartas. Los valores de duración, estado y fechas los informa la fuente sintética: Talent Assistant no calcula nada.

## Canalización de CI (SPEC-014)

`.github/workflows/ci.yml` (creado en SPEC-014) ejecuta en cada PR y push a `main`:

1. `build-test`: SDK 10.0.402 desde `global.json` → restore → build → suite completa contra los servicios PostgreSQL de la misma `compose.yaml` (credenciales sintéticas generadas por el propio run; nunca versionadas). Espera: 132/132 integración + 11/11 unitarias.
2. `secret-scan`: gitleaks sobre todo el historial.

Configuración de medidas de GitHub que el propietario debe activar con consentimiento (Settings, no scripts): **secret scanning + push protection** (código → seguridad), **branch protection de `main`** (PR + CI verde requeridos). Antes de hacer el repo público: el `.env` local nunca se versiona (ya excluido en `.gitignore`) y las credenciales del HCM real jamás se comparten (SPEC-011 §10). El despliegue a producción es la SPEC-015 posterior.

## Asistente conversacional

Desde SPEC-009, la Web incluye la página `/assistant` (ES/EN, con sesión) y la API el endpoint `POST /chat`: el usuario escribe en lenguaje natural, el modelo pide herramientas de un catálogo fijo de cinco (`search_employees`, `presence`, `timeoff`, `tasks`, `attendance_insights`) y la API las ejecuta en proceso con la sesión e idioma del usuario, devolviendo `answer` junto a `toolsUsed` y `sources`. En preguntas de una persona, `search_employees` se llama siempre primero: con varios candidatos el asistente los lista (nombre y oficina) y pregunta cuál antes de responder; con uno solo continúa con la herramienta de datos. La búsqueda de personas es insensible a mayúsculas y acentos, y el asistente no escribe datos.

### Clave y modelo del proveedor

Groq es el proveedor por defecto, integrado mediante su API compatible con OpenAI. Completa en tu `.env` local (nunca versionado):

| Variable | Valor esperado |
|---|---|
| `AI_API_KEY` | Clave de Groq. Vacía por defecto: sin ella la API no llama al proveedor. Solo en `.env`; nunca en el repositorio ni en la salida de comandos. |
| `AI_MODEL` | `openai/gpt-oss-120b` (valor por defecto en `.env.example`), el modelo confirmado con la API de Groq el 2026-10-18 con soporte de herramientas. |

La API usa `AI__BaseUrl=https://api.groq.com/openai/v1` (fijada en la sección `AI` de `appsettings.json`) junto a los límites `AI__TimeoutSeconds`, `AI__MaxTokens` y `AI__MaxToolRounds`. Tras editar `.env`, reinicia el contenedor de la API (`docker compose restart api`).

### Degradación sin proveedor

- Sin `AI_API_KEY`, `POST /chat` responde HTTP 503 `{"reason":"NotConfigured"}` antes de realizar ninguna llamada saliente; el resto de la aplicación funciona igual, sin IA.
- Los fallos del proveedor se traducen a razones legibles por máquina y se muestran con mensajes localizados en la interfaz: límite de tasa (`RateLimited`) responde HTTP 429 y el resto de fallos HTTP 503.
- Los argumentos opcionales de las herramientas pueden viajar como `null` (el proveedor valida los argumentos contra el JSON Schema); el ejecutor los normaliza a ausentes.

### Pasada en vivo de las siete preguntas

Con la API local en marcha y `AI_API_KEY` configurada, el script reparte las siete preguntas de referencia —las seis de SPEC-008 más la ambigua «Dime cuantos dias de vacaciones tiene Juan.»— contra `POST /chat` y comprueba que cada respuesta contiene el hecho del fixture y cita la herramienta esperada:

```powershell
powershell -NoProfile -File scripts\verify-assistant-questions.ps1 -PaceSeconds 9
```

| Opción | Efecto |
|---|---|
| `-PaceSeconds <n>` | Espera n segundos entre preguntas. El nivel gratuito de Groq limita las ráfagas: sin ritmo, una pasada completa alcanza HTTP 429 en un punto previsible; con ritmo cabe dentro de la ventana de tasa. |
| `-Only <n>` | Ejecuta solo una pregunta (1..7). |
| `-ApiBaseUrl <url>` | URL de la API; por defecto `http://127.0.0.1:5101`. |

Códigos de salida: `0` las siete preguntas verdes; `1` fallo de aserción (respuesta sin el hecho esperado o sin la herramienta citada); `3` proveedor no disponible o sin configurar (HTTP 429/503 con la razón indicada en la salida). El script lee el usuario y la contraseña de `.env` y nunca imprime credenciales.

### Qué responde y qué rechaza

Desde SPEC-010, la primera instrucción del system prompt fija el ámbito del asistente: es un asistente interno de RR. HH./HCM que responde únicamente con los datos que devuelven sus herramientas; a una petición ajena —programación, conocimiento general, traducciones, recetas o escritura creativa— responde con un rechazo breve y amable en el idioma del usuario que recuerda su función y le ofrece ayudar con temas del equipo, sin invocar herramientas y sin entregar el contenido pedido; y si las herramientas no devuelven el dato —un salario o un empleado inexistente, por ejemplo—, lo declara explícitamente como información no disponible en lugar de inventarlo. No hay clasificador de intención ni paquetes nuevos: son reglas de instrucción del prompt.

El banco adversarial que evalúa este comportamiento se ejecuta contra la API local con la clave configurada, con el mismo patrón que el script de las siete preguntas:

```powershell
powershell -NoProfile -File scripts\verify-assistant-guardrails.ps1 -PaceSeconds 9
```

| Opción | Efecto |
|---|---|
| `-PaceSeconds <n>` | Espera n segundos entre casos, por el mismo motivo de tasa que en el script de las siete preguntas. |
| `-Only <n>` | Ejecuta solo un caso del banco (1..5). |

Los códigos de salida son idénticos a los del script de las siete preguntas: `0` los cinco casos verdes, `1` fallo de aserción y `3` proveedor no disponible o sin configurar. El script lee el usuario y la contraseña de `.env` y nunca imprime credenciales.

Las aserciones se apoyan en **marcadores estables de comportamiento, no en frases literales**: familias de marcadores de rechazo/redirección y de no disponibilidad, más la ausencia de artefactos de código, de cifras de salario y de empleados fabricados. Los patrones del script son ASCII con escapes `\uXXXX` para los acentos, de modo que no dependen de la codificación de la consola.

Lección de diseño: la cláusula de registros manda presentar los valores que las herramientas devuelven aunque el ámbito se haya reforzado — «información no disponible» solo cuando no hay datos relevantes; sin esa cláusula, la regla de ámbito apaga preguntas legítimas del dominio.

### Problemas frecuentes

| Síntoma | Causa y arreglo |
|---|---|
| HTTP 401 `invalid_api_key` | La línea de `.env` no es una asignación limpia y única `AI_API_KEY=<tu clave>` (por ejemplo, quedó pegada a otra asignación sin salto de línea). Corrígela, valida con `docker compose config --quiet` y reinicia la API. |
| HTTP 404 `model_not_found` | El modelo no está disponible en tu plan de Groq; comprueba el panel del proveedor y ajusta `AI_MODEL`. |
| HTTP 400 `tool_use_failed` | El proveedor rechazó argumentos o el esquema de una herramienta; inspecciona `docker logs talentassistant-local-api-1` para ver el estado y el cuerpo upstream. |
