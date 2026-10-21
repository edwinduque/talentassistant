# SPEC-001: Fundación técnica del proyecto

- **Estado:** Lista para pasar a implementación; la aplicación y todos sus criterios siguen sin implementar ni verificar. Nota de herencia (2026-10-21): el motor MySQL que esta especificación introdujo fue sustituido por PostgreSQL según [ADR-0013](../../docs/architecture/adr/ADR-0013-postgresql-persistence.md)/[SPEC-013](../013-postgres-only-persistence/spec.md); su texto de esquema/Compose se conserva como historial.
- **Proyecto:** Talent Assistant
- **Tipo:** Fundación técnica
- **Fecha:** 2026-10-14

## 1. Objetivo

Establecer la fundación técnica mínima de Talent Assistant para que las siguientes funcionalidades puedan desarrollarse con una arquitectura coherente, pruebas automatizadas, configuración reproducible y documentación versionada.

Esta especificación cubre la estructura inicial de la solución y su verificación técnica. No implementa el asistente completo de RR. HH. ni introduce funcionalidades de inteligencia artificial.

## 2. Alcance

### Incluido

- Crear la solución .NET y sus proyectos.
- Configurar los límites acordados de Clean Architecture.
- Crear una aplicación frontend Blazor Web App.
- Crear una API backend con ASP.NET Core Web API.
- Configurar la persistencia con MySQL y Entity Framework Core.
- Crear la migración inicial de la base de datos.
- Establecer los proyectos de pruebas con xUnit.
- Configurar los ajustes locales y documentar las instrucciones de ejecución.
- Ejecutar MySQL 8.4.11 para desarrollo y pruebas mediante Docker Compose; mantener la aplicación .NET ejecutándose directamente en Windows.
- Proporcionar una verificación mínima de extremo a extremo entre frontend, API y persistencia, cuando corresponda.
- Mantener en Git los documentos SDD y las decisiones arquitectónicas.

### Excluido

- Integración con LLM.
- Frameworks de agentes u orquestación de agentes.
- Servicio de IA independiente desarrollado en Python.
- Integraciones con sistemas de RR. HH. reales o uso de datos reales de empleados.
- Autenticación y autorización más allá de los requisitos de fundación que se aprueben expresamente.
- Despliegue e infraestructura de producción.
- Implementación de la canalización de CI. `docs/requirements/non-functional-requirements.md` NFR-009 y `docs/acceptance/mvp-acceptance-criteria.md` AC-009 siguen vigentes para el MVP y se asignarán a una especificación posterior; no se consideran cumplidos por las comprobaciones locales de esta especificación.
- Dashboards completos de RR. HH., analítica, recomendaciones o decisiones automatizadas sobre empleados.

Los elementos excluidos no deben implementarse como añadidos incidentales a esta especificación.

## 3. Línea base tecnológica aprobada

- .NET 10 LTS.
- SDK de .NET `10.0.402` y runtime `10.0.13` para esta revisión de la línea base.
- Blazor Web App.
- ASP.NET Core Web API.
- Clean Architecture.
- Monolito modular.
- MySQL Server `8.4.11` LTS para desarrollo y pruebas, y Entity Framework Core `10.0.13`.
- Proveedor Oracle `MySql.EntityFrameworkCore` `10.0.9` estable; `MySql.Data` `26.7.0` se resuelve como dependencia del proveedor.
- xUnit.
- CQRS ligero mediante Commands y Queries explícitos, sin MediatR.
- Datos sintéticos.
- Sin LLM ni framework de agentes en la fundación inicial.

Las versiones anteriores fijan el punto de partida de la fundación. NuGet declara que el proveedor `MySql.EntityFrameworkCore` `10.0.9` soporta EF Core 10, incluye un target `net10.0` y requiere EF Core `>= 10.0.9` y `MySql.Data >= 26.7.0`. La documentación general de Oracle declara MySQL Server 8.0 o posterior, pero su tabla de versiones todavía describe EF Core 10 como preview. Por ello, la primera comprobación de persistencia deberá restaurar y compilar las dependencias y crear/aplicar una migración contra MySQL 8.4.11 antes de continuar con persistencia funcional. Esta especificación no afirma que dichas pruebas ya se hayan ejecutado.

## 4. Límites y responsabilidades de la solución

La solución debe separar las siguientes responsabilidades. Para que los comandos de esta especificación sean reproducibles, se utilizarán inicialmente estos nombres y ubicaciones; pueden ajustarse durante la planificación si se conservan los límites de dependencia:

| Proyecto | Ubicación inicial |
|---|---|
| Domain | `src/TalentAssistant.Domain` |
| Application | `src/TalentAssistant.Application` |
| Infrastructure | `src/TalentAssistant.Infrastructure` |
| API | `src/TalentAssistant.Api` |
| Web | `src/TalentAssistant.Web` |
| Pruebas unitarias | `tests/TalentAssistant.UnitTests` |
| Pruebas de integración | `tests/TalentAssistant.IntegrationTests` |

La solución se denominará `TalentAssistant.sln` en la raíz del repositorio.

### Domain

Contiene las entidades de negocio, los objetos de valor, las reglas del dominio y las abstracciones propias del dominio cuando sean necesarias.

No debe depender de Application, Infrastructure, API, Web, EF Core ni del proveedor de MySQL.

### Application

Contiene los casos de uso y los contratos de aplicación.

Las operaciones que modifican el estado deben expresarse mediante Commands; las operaciones de consulta, mediante Queries. Se pueden utilizar servicios o manejadores explícitos sin introducir MediatR.

No debe depender de Infrastructure, API ni del frontend Blazor.

### Infrastructure

Contiene la configuración de EF Core, `DbContext`, las migraciones, la integración con MySQL y las implementaciones de las abstracciones de persistencia.

Puede depender de Domain y Application cuando sea necesario para implementar sus contratos.

### API

Contiene los endpoints de ASP.NET Core, la composición de dependencias, los aspectos propios de HTTP, la validación en los límites de la API y la configuración de inicio.

Debe invocar los casos de uso de Application. No debe contener reglas de negocio ni acceder directamente a la base de datos.

### Web

Contiene la aplicación Blazor Web App, los componentes de interfaz, la lógica de presentación y la comunicación con la API.

No debe acceder directamente a `DbContext`, Infrastructure ni a la base de datos.

### Tests

Utiliza xUnit para las pruebas automatizadas.

Las pruebas unitarias deben ejecutarse sin una instancia activa de MySQL. Se añadirán pruebas de integración para persistencia y comportamiento HTTP cuando corresponda.

### Modo de renderizado y comunicación Web-API

La fundación utilizará `InteractiveServer`. Los componentes interactivos y su estado se ejecutan en el servidor a través de circuitos SignalR; cada circuito consume recursos del servidor y depende de conectividad persistente. El alcance no establece objetivos de concurrencia ni despliegue horizontal, por lo que esta selección solo cubre la fundación local del MVP.

Web llamará a API desde el servidor mediante un cliente HTTP tipado registrado con `IHttpClientFactory`. La URL base se leerá de `BackendApi:BaseUrl`, configurable por entorno mediante `BackendApi__BaseUrl`; no se codificará en los componentes. Esta llamada servidor-servidor no requiere CORS. Una modalidad futura con llamadas desde el navegador deberá especificar orígenes CORS explícitos.

Los errores HTTP, de red y de tiempo de espera se transformarán en estados controlados de interfaz; el diagnóstico se registrará en el servidor sin cadenas de conexión, secretos ni datos sensibles. La autenticación está excluida de SPEC-001. API y Web se ejecutarán localmente con datos sintéticos y no se expondrán a redes compartidas; el uso fuera de ese entorno requiere una decisión y especificación de autenticación.

## 5. Requisitos funcionales

### SPEC-001-FR-001: Estructura de la solución

El repositorio debe contener una solución .NET compilable, con responsabilidades claramente separadas para Domain, Application, Infrastructure, API, Web y Tests.

**Verificación:** restaurar y compilar la solución correctamente utilizando el SDK de .NET documentado.

### SPEC-001-FR-002: Inicio de la API

La API ASP.NET Core debe iniciarse utilizando la configuración local documentada y exponer un endpoint mínimo de salud o diagnóstico.

**Verificación:** una petición HTTP local obtiene la respuesta satisfactoria esperada.

### SPEC-001-FR-003: Inicio de Blazor

La aplicación Blazor Web App debe iniciarse con la configuración documentada y mostrar una página inicial básica.

**Verificación:** la aplicación se abre localmente y la página se renderiza sin excepciones no controladas durante el inicio.

### SPEC-001-FR-004: Comunicación entre frontend y API

El frontend debe disponer de un mecanismo configurable y documentado para invocar la API backend. La implementación inicial debe demostrar al menos una petición satisfactoria.

**Verificación:** el frontend muestra o confirma el resultado de una petición exitosa a la API sin acceder directamente a la base de datos.

### SPEC-001-FR-005: Persistencia

Infrastructure debe configurar EF Core para utilizar MySQL. La conexión debe ser configurable y no estar codificada directamente en el código fuente.

**Verificación:** la aplicación puede conectarse a la base de datos de desarrollo configurada.

### SPEC-001-FR-006: Migraciones de base de datos

El esquema inicial debe poder reproducirse mediante migraciones de EF Core versionadas.

**Verificación:** una base de datos de desarrollo vacía puede crearse o actualizarse siguiendo el procedimiento documentado.

### SPEC-001-FR-007: Datos sintéticos

La fundación debe proporcionar un procedimiento documentado y repetible para generar datos sintéticos de desarrollo cuando el modelo de dominio inicial lo requiera.

**Verificación:** el procedimiento puede ejecutarse repetidamente sin generar duplicados no controlados.

Si la fundación todavía no requiere entidades de negocio persistidas, se debe documentar el motivo y aplazar la carga de datos hasta la primera especificación funcional que lo necesite.

### SPEC-001-FR-008: Pruebas automatizadas

La solución debe incluir pruebas con xUnit para el comportamiento inicial de la aplicación y las reglas relevantes de arquitectura.

**Verificación:** todas las pruebas pasan utilizando el comando documentado.

### SPEC-001-FR-009: Configuración y secretos

La configuración específica de cada entorno debe estar externalizada. No se deben incluir secretos ni credenciales reales de producción en el repositorio.

**Verificación:** la inspección del repositorio confirma que no hay credenciales ni secretos reales versionados y que las instrucciones locales identifican la configuración necesaria.

### SPEC-001-FR-010: Flujo reproducible de desarrollo

El repositorio debe documentar los prerrequisitos y los comandos necesarios para restaurar dependencias, compilar, ejecutar pruebas, aplicar migraciones e iniciar las aplicaciones.

**Verificación:** otro desarrollador puede seguir las instrucciones desde una clonación limpia, teniendo en cuenta los prerrequisitos documentados.

## 6. Requisitos no funcionales

### SPEC-001-NFR-001: Límites de dependencia

Las referencias entre proyectos deben respetar la arquitectura definida en esta especificación.

### SPEC-001-NFR-002: Mantenibilidad

Utilizar nombres claros, responsabilidades acotadas y convenciones de formato consistentes. Evitar abstracciones innecesarias y generalizaciones prematuras.

### SPEC-001-NFR-003: Capacidad de prueba

La lógica de negocio debe poder probarse sin una base de datos activa ni servicios externos de inteligencia artificial.

### SPEC-001-NFR-004: Seguridad

No incluir credenciales en el repositorio. Mantener el acceso a la base de datos en el backend. No exponer cadenas de conexión ni secretos en las respuestas de la API o en la configuración del frontend.

### SPEC-001-NFR-005: Reproducibilidad

Documentar suficientemente los requisitos del SDK, los paquetes, las migraciones, la configuración y las pruebas para reproducir la fundación técnica en un entorno limpio.

### SPEC-001-NFR-006: Preparación para IA futura

Mantener la posibilidad de incorporar un servicio de IA en el futuro, respetando el límite arquitectónico definido en ADR-0003. Este requisito no justifica introducir un LLM, un servicio Python ni un framework de agentes en la fundación inicial.

## 7. Criterios de aceptación

La especificación se considerará completada cuando se cumplan todos los criterios aplicables. Ninguno se marca como verificado en esta revisión documental.

| ID | Criterio de aceptación | Requisitos relacionados | Método de verificación | Tipo |
|---|---|---|---|---|
| SPEC-001-AC-001 | La solución restaura dependencias y compila con el SDK fijado. | SPEC-001-FR-001, SPEC-001-NFR-005 | `dotnet restore TalentAssistant.sln` y `dotnet build TalentAssistant.sln --configuration Debug`. | Automático |
| SPEC-001-AC-002 | Las referencias entre proyectos respetan los límites de Domain, Application, Infrastructure, API, Web y Tests; no se añaden buses, MediatR ni abstracciones sin necesidad aprobada. | SPEC-001-NFR-001, SPEC-001-NFR-002 | Revisar referencias de proyectos y ejecutar pruebas de arquitectura, si se incorporan; revisión manual de responsabilidades. | Automático y manual |
| SPEC-001-AC-003 | API inicia localmente y su endpoint de salud devuelve HTTP 200 sin exponer secretos ni stack traces. | SPEC-001-FR-002, SPEC-001-NFR-004 | Ejecutar API y comprobar endpoint con una petición HTTP local. | Automático |
| SPEC-001-AC-004 | Blazor Web App inicia en `InteractiveServer`, renderiza la página inicial sin excepción no controlada y la página básica permite navegación por teclado, etiquetas y contraste legible. | SPEC-001-FR-003; `docs/requirements/non-functional-requirements.md` NFR-012 | Abrir la URL local, recorrer controles con teclado y revisar nombres accesibles y contraste de la página inicial. | Manual |
| SPEC-001-AC-005 | Web obtiene desde API, mediante el cliente HTTP configurado, una respuesta satisfactoria sin acceso directo a persistencia. | SPEC-001-FR-004, SPEC-001-NFR-001 | Prueba HTTP de integración y comprobación de configuración `BackendApi:BaseUrl`. | Automático y manual |
| SPEC-001-AC-006 | API conecta con MySQL 8.4.11 usando configuración externa; el repositorio no contiene credenciales reales y las respuestas/logs no exponen secretos ni stack traces. | SPEC-001-FR-005, SPEC-001-FR-009, SPEC-001-NFR-004 | Arranque con configuración local, prueba de conexión, escaneo estático y comprobación de un error controlado con un secreto sintético centinela. | Automático y manual |
| SPEC-001-AC-007 | Una base MySQL de prueba vacía puede crearse y actualizarse con migraciones EF Core versionadas. | SPEC-001-FR-006, SPEC-001-NFR-005 | Crear y aplicar una migración con `dotnet ef` contra MySQL 8.4.11; comprobar historial y repetición del comando. | Integración automática |
| SPEC-001-AC-008 | Los datos sintéticos se inicializan repetiblemente sin duplicados, o su aplazamiento queda justificado porque aún no existen entidades de negocio persistidas. | SPEC-001-FR-007, SPEC-001-NFR-005 | Si existe inicializador, ejecutarlo dos veces y comprobar el mismo conjunto; si no hay modelo, revisar el aplazamiento documentado. | Automático o manual según aplazamiento |
| SPEC-001-AC-009 | Las pruebas unitarias xUnit pasan sin una instancia activa de MySQL. | SPEC-001-FR-008, SPEC-001-NFR-003 | Ejecutar `dotnet test tests/TalentAssistant.UnitTests/TalentAssistant.UnitTests.csproj`. | Automático |
| SPEC-001-AC-010 | Las pruebas de integración usan MySQL real y verifican conexión, migraciones y operaciones EF Core de lectura/escritura con datos sintéticos aislados. Si aún no hay entidades de negocio, el modelo de prueba permanece en el proyecto de integración y no se añade al dominio ni al esquema de la aplicación. | SPEC-001-FR-005, SPEC-001-FR-006, SPEC-001-FR-008, SPEC-001-NFR-003 | Iniciar el servicio MySQL y ejecutar `dotnet test tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj`; no sustituir el proveedor por una base en memoria. | Integración automática |
| SPEC-001-AC-011 | Las instrucciones permiten a otro desarrollador configurar Docker, restaurar, compilar, probar, ejecutar API/Web y aplicar migraciones desde un checkout limpio. | SPEC-001-FR-010, SPEC-001-NFR-005 | Seguir el procedimiento desde una clonación limpia y registrar comandos/resultados. | Manual y automático |
| SPEC-001-AC-012 | La fundación no incorpora paquetes o servicios de LLM, agentes, Python, n8n o MCP. | SPEC-001-NFR-006 | Revisar proyectos, dependencias y configuración frente a ADR-0003. | Manual y estático |
| SPEC-001-AC-013 | La implementación y sus límites coinciden con los ADR aceptados y el registro de decisiones. | SPEC-001-NFR-001, SPEC-001-NFR-002, SPEC-001-NFR-006 | Comparar implementación, ADR y registro; documentar cualquier desviación. | Manual |

### Requisito general de CI fuera de esta especificación

`docs/requirements/non-functional-requirements.md` **NFR-009** y `docs/acceptance/mvp-acceptance-criteria.md` **AC-009** siguen vigentes para el MVP. La canalización queda fuera de SPEC-001 y debe asignarse a una especificación posterior cuyo ID/ruta aún no se ha creado. Ni las comprobaciones locales ni este aplazamiento satisfacen esos requisitos.

## 8. Entorno local, persistencia y pruebas

### 8.1. Versiones y compatibilidad fijadas

La fundación usará el SDK .NET `10.0.402`, target `net10.0`, runtime `10.0.13`, EF Core y herramientas EF `10.0.13`, Oracle `MySql.EntityFrameworkCore` `10.0.9`, y `MySql.Data` `26.7.0`. El servidor de desarrollo y pruebas será MySQL `8.4.11` LTS. ADR-0004 registra fuentes, licencias, alternativas y la diferencia entre compatibilidad declarada y pruebas ejecutadas.

La compatibilidad declarada no se considera una prueba de ejecución. La primera tarea de implementación que toque persistencia debe restaurar/compilar y aplicar una migración en MySQL 8.4.11; si falla, se detiene la persistencia funcional y se revisa ADR-0004 sin bajar a EF Core 9 ni cambiar silenciosamente la línea base.

### 8.2. Docker Compose para MySQL local

El futuro `compose.yaml` definirá únicamente el servicio de base de datos durante esta fase:

- Imagen oficial fijada: `mysql:8.4.11@sha256:24989179f58856db763f418010c1169fecc944e3ec1ea7e921572d83eb070f70`; no usar `latest`.
- Publicar `127.0.0.1:3306:3306` para que la aplicación .NET ejecutada en Windows conecte a `127.0.0.1:3306`; el hostname `mysql` solo es resoluble dentro de la red Compose.
- Guardar datos en un volumen nombrado para conservarlos al recrear el contenedor.
- Recibir `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD` y `MYSQL_ROOT_PASSWORD` desde variables locales requeridas; nunca incluir valores reales en el YAML versionado.
- Definir una comprobación de salud de MySQL y esperar a estado saludable antes de aplicar migraciones o pruebas.
- Requerir Docker Desktop en Windows en modo de contenedores Linux y Docker Compose v2 disponible.

La aplicación Blazor y la API se ejecutarán directamente en el equipo; no se crearán contenedores de aplicación en SPEC-001. El digest corresponde al manifiesto oficial multi-arquitectura para `linux/amd64` y `linux/arm64`. Actualizar tag/digest únicamente en una revisión explícita y tras repetir la verificación de compatibilidad.

### 8.3. Configuración y secretos

- El archivo local `.env` aporta variables a Compose y no se versiona. `.env.example` puede versionarse solo con nombres y valores de ejemplo no utilizables.
- La implementación debe añadir reglas a `.gitignore` para excluir `.env` y archivos locales con credenciales; el repositorio actual no contiene `.gitignore`, por lo que esta comprobación permanece pendiente hasta la implementación.
- La cadena `ConnectionStrings:TalentAssistant` se guarda en User Secrets para desarrollo local o en `ConnectionStrings__TalentAssistant` en variables de entorno. Las credenciales de MySQL del contenedor y la cadena de conexión deben corresponder.
- La cadena local tendrá este formato, sustituyendo los marcadores por los valores locales de `.env`; los valores reales no se escriben en archivos versionados:

  ```text
  Server=127.0.0.1;Port=3306;Database=talentassistant_dev;User Id=<MYSQL_USER>;Password=<MYSQL_PASSWORD>;
  ```

- User Secrets se inicializa desde el proyecto API y almacena el valor fuera del repositorio. Los entornos posteriores reciben secretos desde variables o un almacén de secretos aprobado.

  ```powershell
  Set-Location src/TalentAssistant.Api
  dotnet user-secrets init
  dotnet user-secrets set "ConnectionStrings:TalentAssistant" "Server=127.0.0.1;Port=3306;Database=talentassistant_dev;User Id=<MYSQL_USER>;Password=<MYSQL_PASSWORD>;"
  Set-Location ../..
  ```
- `BackendApi:BaseUrl` es configuración no secreta y se define por entorno; puede venir de configuración de desarrollo y sobrescribirse con `BackendApi__BaseUrl`.
- La configuración no secreta puede residir en `appsettings.json` y `appsettings.Development.json`; las credenciales no. Otros entornos reciben secretos desde su mecanismo de variables/secretos, no desde los archivos versionados de desarrollo.
- Los archivos de configuración versionados no contendrán contraseñas, tokens ni cadenas de conexión reales. No se configura producción en SPEC-001.

### 8.4. Inicio, detención y persistencia local

Prerrequisito: Docker Desktop iniciado y `.env` local creado con credenciales sintéticas.

```powershell
docker compose up -d mysql
docker compose ps
docker compose stop mysql
docker compose start mysql
docker compose down
```

`stop` y `start` conservan el contenedor y volumen. Para recrear el contenedor sin perder datos se usa `docker compose down` seguido de `docker compose up -d mysql`; `down` elimina contenedores y red, pero conserva el volumen nombrado. **`docker compose down --volumes` elimina el volumen y los datos persistidos**; solo se utilizará cuando se quiera descartar explícitamente la base local y no haya datos que conservar. Cambiar variables iniciales en `.env` no modifica las credenciales de un volumen ya inicializado.

Con MySQL saludable, la API y Web se ejecutan en terminales separadas. Los puertos de ejemplo se pueden cambiar mediante configuración local:

```powershell
# Terminal 1: API
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5101"
dotnet run --no-launch-profile --project src/TalentAssistant.Api/TalentAssistant.Api.csproj

# Terminal 2: Web
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5102"
$env:BackendApi__BaseUrl = "http://127.0.0.1:5101/"
dotnet run --no-launch-profile --project src/TalentAssistant.Web/TalentAssistant.Web.csproj
```

Estos comandos se documentan como flujo previsto; no se ejecutaron porque los proyectos aún no existen.

### 8.5. Migraciones

Prerrequisitos: SDK `10.0.402` indicado en `global.json`, servicio MySQL saludable, cadena local configurada, manifiesto `.config/dotnet-tools.json` con `dotnet-ef` `10.0.13` y referencia de diseño `Microsoft.EntityFrameworkCore.Design` `10.0.13` en el proyecto que proporciona `DbContext`. El manifiesto se creará/versionará durante la implementación; no existe todavía.

```powershell
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.13
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef migrations add InitialCreate --project src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj --output-dir Persistence/Migrations
dotnet ef database update --project src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj
New-Item -ItemType Directory -Force artifacts | Out-Null
dotnet ef migrations script --idempotent --project src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj --startup-project src/TalentAssistant.Api/TalentAssistant.Api.csproj --output artifacts/migrations.sql
```

Las migraciones se versionan en Infrastructure. En desarrollo se aplican de forma explícita con `dotnet ef database update`; no se aplican migraciones automáticamente al iniciar la aplicación ni en producción. Para despliegues futuros, el script generado requiere revisión y un proceso aprobado separado. Estos comandos son instrucciones previstas, no fueron ejecutados en esta auditoría.

### 8.6. Datos sintéticos

SPEC-001 no inventa entidades ni registros de negocio. Si no hay entidades persistidas, la carga de datos se aplaza y el motivo se registra conforme a SPEC-001-FR-007 y ADR-0002. La primera especificación funcional que introduzca entidades definirá el inicializador: datos deterministas, relaciones válidas, ejecución repetible sin duplicados y activación explícita solo en Development/Testing. No se conectará a servicios externos ni se usarán datos personales reales.

### 8.7. Pruebas

- Unitarias: xUnit para Domain, Application y reglas de arquitectura; se ejecutan sin MySQL ni servicios externos.
- Integración: Docker Desktop y MySQL real `8.4.11`; la base `talentassistant_tests` verifica conectividad, migraciones y operaciones de lectura/escritura con datos sintéticos. No se considera equivalente un proveedor en memoria.
- La base y credenciales de pruebas se mantienen separadas de `talentassistant_dev`; la preparación y limpieza solo afectan a la base de pruebas y no eliminan el volumen de desarrollo.
- API/Web: comprobar el endpoint de salud y una llamada satisfactoria Web → API en `InteractiveServer`; CORS no aplica a esta llamada servidor-servidor.
- Ejecutar por separado los proyectos unitarios e integración indicados en los criterios de aceptación, para que las pruebas unitarias no requieran Docker.
- La autenticación no se implementa en esta especificación; no exponer la aplicación a redes compartidas ni utilizar datos reales hasta aprobar controles de acceso en una especificación posterior.

## 9. Restricciones de implementación

- No introducir MediatR, event sourcing, un broker de mensajes ni microservicios como parte de esta especificación.
- No añadir dependencias sin un requisito claro.
- No exponer detalles de persistencia al proyecto Web.
- No implementar funcionalidades de RR. HH. que correspondan a otra especificación.
- No modificar silenciosamente los requisitos ni los ADR aceptados.
- Si un requisito no puede cumplirse, documentar el motivo y proponer un cambio formal en lugar de marcarlo como completado.

## 10. Informe de verificación

Al terminar la implementación, entregar:

1. Proyectos y archivos creados o modificados.
2. Comandos ejecutados y sus resultados.
3. Resultados de compilación y pruebas.
4. Verificación de migraciones y del inicio local.
5. Estado de cada criterio de aceptación: cumplido, fallido o bloqueado.
6. Desviaciones respecto a esta especificación y los ADR.
7. Problemas pendientes que deban resolverse antes de iniciar la siguiente especificación.
