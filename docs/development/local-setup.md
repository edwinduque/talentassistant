# Ejecución local

Esta guía levanta MySQL 8.4.11 en Docker Compose y ejecuta la API en Windows. La aplicación usa datos sintéticos; la fundación no incluye autenticación ni está preparada para exponerse a redes compartidas.

> **Evolución aprobada para planificación:** SPEC-004 y ADR-0006 definen un perfil Compose para API, Web y MySQL con dataset sintético persistido. La implementación aún no ha comenzado; esta guía conserva el flujo vigente (MySQL en Compose, API/Web en el host) hasta que el nuevo perfil y sus instrucciones se implementen y verifiquen.

## Requisitos

- Windows con .NET SDK `10.0.402` (el SDK incluye runtime .NET y ASP.NET Core `10.0.13`). `global.json` fija esa versión sin roll-forward.
- Docker Desktop en modo de contenedores Linux y Docker Compose v2.
- NuGet accesible para restaurar paquetes la primera vez.

## Configurar y arrancar MySQL

1. Copia `.env.example` a `.env` y completa los dos campos de contraseña vacíos con valores sintéticos distintos. Compose rechazará los valores vacíos.
2. Mantén `MYSQL_USER=talentassistant_app`: el script inicial concede a ese usuario acceso a la base separada de integración. Asigna contraseñas locales distintas para `MYSQL_PASSWORD` y `MYSQL_ROOT_PASSWORD`; no uses contraseñas de producción.
3. Arranca el único servicio contenedorizado de esta fundación:

   ```powershell
   docker compose up -d --wait mysql
   docker compose ps
   ```

Compose fija la imagen oficial `mysql:8.4.11` por digest, publica el puerto únicamente en `127.0.0.1:3306` y conserva los datos en el volumen nombrado `talentassistant_mysql_data`. El script `docker/mysql/init/10-create-test-database.sql` crea `talentassistant_tests` y concede acceso al usuario local en el primer inicio de un volumen vacío.

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

La migración `InitialCreate` de la aplicación crea el historial de EF Core; todavía no existen entidades de negocio ni tablas de negocio. Las migraciones se aplican explícitamente, no durante el arranque de la API. La generación de una migración de aplicación usa:

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

Las pruebas unitarias no requieren MySQL. Las pruebas de integración usan exclusivamente la base `talentassistant_tests`; no usan EF Core InMemory. El modelo de prueba requiere una migración propia, separada del esquema de la aplicación. **No ejecutes la prueba de integración hasta generar y aplicar esa migración.** La generación requiere cargar el ensamblado `TalentAssistant.IntegrationTests`; en equipos donde Application Control bloquee esa carga, la migración y la prueba quedan pendientes hasta la autorización de seguridad correspondiente.

Después de que la política aprobada permita esa carga, configura ambas cadenas (la de desarrollo de API mediante User Secrets y la de pruebas mediante esta variable), y genera/aplica la migración del contexto de prueba:

```powershell
$env:ConnectionStrings__TalentAssistantTests = "Server=127.0.0.1;Port=3306;Database=talentassistant_tests;User Id=talentassistant_app;Password=<MYSQL_PASSWORD>;"
dotnet ef migrations add InitialCreate --project tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj --context PersistenceProbeContext --output-dir Persistence/Migrations
dotnet ef database update --project tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj --context PersistenceProbeContext
dotnet test tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj
```

El contexto y su `IDesignTimeDbContextFactory` residen en el proyecto de integración, que es el proyecto de migraciones; API es el startup project ejecutable. EF Core busca la factory en el proyecto del contexto y la usa para creación de diseño. Este comando aún no se ejecutó en este checkout: su verificación requiere que Application Control permita cargar el ensamblado `TalentAssistant.IntegrationTests`. La ejecución de ensamblados xUnit puede estar sujeta a las políticas del equipo; no se deben desactivar ni eludir. La prueba y las migraciones de integración siguen pendientes hasta la autorización de seguridad correspondiente.

## Detener servicios y conservar datos

```powershell
docker compose stop mysql
docker compose start mysql
docker compose down
```

`stop`, `start` y `down` conservan el volumen nombrado. **`docker compose down --volumes` elimina el volumen y los datos de desarrollo e integración**; úsalo solo si se decidió descartar expresamente ambas bases. Cambiar las variables de `.env` no cambia las credenciales de un volumen ya inicializado.

La carga de datos de negocio sintéticos se aplaza hasta que una especificación funcional introduzca entidades, conforme a ADR-0002. La base de integración puede contener únicamente datos de prueba efímeros.
