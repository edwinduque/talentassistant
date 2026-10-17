# Ejecución local

Esta guía ofrece el perfil completo Docker Compose para API, Web y MySQL, además del perfil alternativo que ejecuta API/Web en el host. Ambos usan exclusivamente respuestas HCM sintéticas y exigen iniciar sesión (SPEC-005); el entorno no está preparado para exponerse a redes compartidas ni para un despliegue productivo.

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

Completa `MYSQL_PASSWORD` y `MYSQL_ROOT_PASSWORD` con valores sintéticos locales distintos; conserva `MYSQL_DATABASE=talentassistant_dev` y `MYSQL_USER=talentassistant_app`. Completa también `AUTH_SIGNING_KEY` y `AUTH_SEED_PASSWORD` según la sección «Autenticación local»; `docker compose config` rechaza valores vacíos. `.env` está excluido de Git y del contexto Docker; no lo compartas ni uses credenciales de producción. Si ya existe el volumen MySQL, conserva las contraseñas con las que se inicializó: editarlas en `.env` no cambia las credenciales almacenadas en ese volumen.

### 2. Construir e iniciar los servicios

```powershell
docker compose config --quiet
docker compose up --build -d --wait
docker compose ps
```

El primer build restaura las dependencias declaradas y requiere acceso a los registros indicados arriba. Compose espera a MySQL saludable, ejecuta `db-init` (migraciones y upsert transaccional del dataset sintético) y luego inicia API y Web. La API del perfil Compose lee desde MySQL; Web consume API mediante la red interna. El inicio y los puertos publicados quedan limitados a loopback: API `http://127.0.0.1:5101`, Web `http://127.0.0.1:5102` y MySQL `127.0.0.1:3306`.

### 3. Comprobar API, dataset y Web

```powershell
Invoke-RestMethod http://127.0.0.1:5101/health
Invoke-RestMethod http://127.0.0.1:5101/health/database
$items = Invoke-RestMethod "http://127.0.0.1:5101/attendance/insights?from=2026-10-15&to=2026-10-18"
"Fixtures: $($items.Count)"
(Invoke-WebRequest http://127.0.0.1:5102/attendance-insights -UseBasicParsing).StatusCode
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

`stop`, `start` y `down` conservan el volumen nombrado `talentassistant_mysql_data`. **`docker compose down --volumes` es destructivo:** elimina el volumen y las bases locales de desarrollo e integración; úsalo solo cuando se decidió descartar esos datos. Cambiar `.env` no cambia las credenciales de un volumen ya inicializado.

Para consultar logs sin mostrar valores de `.env`:

```powershell
docker compose logs --tail 100 mysql api web
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

`db-init` aplica las migraciones (incluidas las tablas de Identity) y crea el usuario de siembra solo si no existe. Nunca sobreescribe la contraseña de un usuario ya existente. Para cambiar la contraseña, borra la fila del usuario o edítala por SQL en MySQL y vuelve a ejecutar la inicialización:

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

## Perfil host alternativo: configurar MySQL y ejecutar API/Web en Windows

Esta variante mantiene API y Web fuera de Docker. El perfil host/tests conserva la fuente HCM en memoria por defecto; no necesita ni ejecuta `db-init`.

1. Copia `.env.example` a `.env` y completa los dos campos de contraseña vacíos con valores sintéticos distintos. Compose rechazará los valores vacíos.
2. Mantén `MYSQL_USER=talentassistant_app`: el script inicial concede a ese usuario acceso a la base separada de integración. Asigna contraseñas locales distintas para `MYSQL_PASSWORD` y `MYSQL_ROOT_PASSWORD`; no uses contraseñas de producción.
3. Arranca solo MySQL para el perfil host:

   ```powershell
   docker compose up -d --wait mysql
   docker compose ps
   ```

4. El perfil host también exige autenticación (SPEC-005): la API necesita `Authentication__SigningKey` (>= 32 caracteres) y `Authentication__SeedPassword` como variables de entorno (`Authentication__*`) o en User Secrets antes de ejecutarse; sin la clave de firma, la API no arranca. El usuario local se crea con el inicializador `--initialize-local-data`, el mismo que ejecuta `db-init` en Compose.

El Dockerfile MySQL construye la imagen local desde `mysql:8.4.11` fijada por digest. Compose publica el puerto únicamente en `127.0.0.1:3306` y conserva los datos en el volumen nombrado `talentassistant_mysql_data`. El script `docker/mysql/init/10-create-test-database.sql` crea `talentassistant_tests` y concede acceso al usuario local en el primer inicio de un volumen vacío.

Los scripts de `/docker-entrypoint-initdb.d` solo se ejecutan al inicializar un directorio de datos vacío. Si el volumen ya existía antes de añadir el script, crea la base y concede los permisos manualmente con una sesión autenticada de MySQL:

```sql
CREATE DATABASE IF NOT EXISTS `talentassistant_tests`
    CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
GRANT ALL PRIVILEGES ON `talentassistant_tests`.* TO 'talentassistant_app'@'%';
```

## Configurar la API y aplicar migraciones

La cadena de desarrollo se guarda fuera de Git mediante User Secrets. Ejecuta desde la raíz del repositorio y reemplaza el marcador por el valor local de `MYSQL_PASSWORD`:

```powershell
dotnet user-secrets set "ConnectionStrings:TalentAssistant" "Server=127.0.0.1;Port=3306;Database=talentassistant_dev;User Id=talentassistant_app;Password=<MYSQL_PASSWORD>;" --project src/TalentAssistant.Api/TalentAssistant.Api.csproj
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj
```

El proyecto API tiene un `UserSecretsId`; `WebApplication.CreateBuilder` carga User Secrets en `Development`. `.env` lo lee Docker Compose, **no** el proceso .NET. Para la API también se puede usar `ConnectionStrings__TalentAssistant` como variable de entorno. No hay una contraseña real configurada en los archivos versionados.

### Comprobación reproducible de AC-006

Con MySQL saludable y el puerto local `5101` libre, ejecuta:

```powershell
.\scripts\verify-database-secret-redaction.ps1
```

El script genera un centinela sintético por ejecución, lo entrega al proceso API solo mediante una variable de entorno y consulta `/health/database`. Espera HTTP 503 con un error genérico ante el rechazo de autenticación, y comprueba que ni la respuesta ni la salida capturada contengan el centinela o un stack trace. La salida capturada temporal se elimina al terminar; el script nunca imprime el valor. No ejecuta xUnit, no mueve ensamblados y no modifica la política de Application Control.

La aplicación mantiene sus migraciones explícitas; en el perfil host no se aplican automáticamente al arrancar la API. La migración `20261016001733_AddSyntheticAttendanceDataset` crea la tabla técnica que usa el perfil Compose. La generación de una migración de aplicación usa:

```powershell
dotnet ef migrations add <NombreMigracion> --project src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj --output-dir Persistence/Migrations
```

## Ejecutar la API

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5101"
dotnet run --no-launch-profile --project src/TalentAssistant.Api/TalentAssistant.Api.csproj
```

El endpoint mínimo es `GET http://127.0.0.1:5101/health`. `GET /health/database` comprueba la conexión y devuelve HTTP 503 genérico si MySQL no está disponible, sin exponer la cadena ni el error interno. No se configura CORS porque cualquier llamada Web→API se realizará desde el servidor.

## Ejecutar Blazor Web

Con la API activa en `127.0.0.1:5101`, inicia Blazor en otra terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5102"
$env:BackendApi__BaseUrl = "http://127.0.0.1:5101/"
dotnet run --no-launch-profile --project src/TalentAssistant.Web/TalentAssistant.Web.csproj
```

La página inicial consulta `GET /health` mediante `BackendApiClient`, registrado como cliente HTTP tipado del servidor. `BackendApi:BaseUrl` tiene un valor local de desarrollo y `BackendApi__BaseUrl` lo sobrescribe por entorno. Web no referencia Infrastructure ni se conecta directamente a MySQL.

## Restaurar, compilar y probar

```powershell
dotnet restore TalentAssistant.sln
dotnet build TalentAssistant.sln --configuration Debug
dotnet test tests/TalentAssistant.UnitTests/TalentAssistant.UnitTests.csproj
```

Las pruebas unitarias no requieren MySQL. Las pruebas de integración usan exclusivamente la base `talentassistant_tests`; no usan EF Core InMemory. El modelo de prueba `PersistenceProbeContext` tiene su propia migración versionada en `tests/TalentAssistant.IntegrationTests/Persistence/Migrations/`, separada del esquema de la aplicación, y la prueba la aplica automáticamente con `MigrateAsync`.

Configura la cadena de la base de pruebas y ejecuta la suite completa:

```powershell
$env:ConnectionStrings__TalentAssistantTests = "Server=127.0.0.1;Port=3306;Database=talentassistant_tests;User Id=talentassistant_app;Password=<MYSQL_PASSWORD>;AllowPublicKeyRetrieval=true;"
dotnet test TalentAssistant.sln --configuration Debug --no-restore
```

`AllowPublicKeyRetrieval=true` permite completar la autenticación `caching_sha2_password` cuando MySQL acaba de reiniciar y la conexión loopback no negocia TLS; sin esa opción, el primer intento desde el host devuelve `Access denied for user ... (using password: YES)`. El puerto MySQL solo está publicado en `127.0.0.1`. La ejecución de ensamblados xUnit puede estar sujeta a las políticas del equipo; no se deben desactivar ni eludir.

La prueba crea su esquema en `talentassistant_tests` en la primera ejecución y hace rollback de los registros que inserta; no recibe la semilla demo de `talentassistant_dev`. Solo hace falta regenerar la migración si cambia el modelo de prueba, usando el propio proyecto de pruebas como startup project (EF Core localiza el DLL del contexto en la salida de ese proyecto; con la API como startup no lo encuentra):

```powershell
dotnet ef migrations add <NombreMigracion> --project tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --startup-project tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --context PersistenceProbeContext --output-dir Persistence/Migrations
```

Para detener únicamente MySQL en el perfil host, usa `docker compose stop mysql`; para volver a iniciarlo, `docker compose start mysql`. El flujo completo de persistencia y reset del perfil Compose está en la sección anterior.

## Consultas del asistente (API)

Desde SPEC-008, la API expone cuatro endpoints de consulta —las herramientas que el futuro asistente llamará— que exigen sesión (sin token válido responden HTTP 401) y responden en el idioma del perfil del usuario autenticado:

| Endpoint | Parámetros | Contenido |
|---|---|---|
| `GET /employees` | `name`, `office` | Directorio; búsqueda por nombre insensible a mayúsculas (sin plegado de acentos) y por oficina. |
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
