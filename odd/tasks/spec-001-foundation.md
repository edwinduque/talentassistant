# Especificación de trabajo: fundación técnica SPEC-001

Objetivo: implementar y verificar la fundación técnica aprobada en SPEC-001, deteniéndose si falla el gate real de persistencia.
Rama: `main`; entrega: `ask-on-risk`, sin commits, PR ni push por instrucción explícita; runner: `dotnet test` para xUnit y MySQL real 8.4.11 para integración.

## Specs

### S1 — Gate de compatibilidad real

Alcance autorizado: validar primero `.NET 10`, EF Core `10.0.13`, `MySql.EntityFrameworkCore` `10.0.9`, `MySql.Data` `26.7.0` y MySQL `8.4.11`. La prueba debe crear/aplicar migración, escribir y consultar datos usando el servidor real; verificar también actualización de esquema. Si el fallo demuestra incompatibilidad del proveedor, detener la persistencia y reportar evidencia sin sustituir proveedor ni reducir versiones.

Texto exacto del usuario: “Primer bloqueo obligatorio: valida la compatibilidad real de .NET 10, EF Core 10.0.13, `MySql.EntityFrameworkCore` 10.0.9, `MySql.Data` 26.7.0 y MySQL 8.4.11.” También: “Ejecuta una prueba real de persistencia que incluya migración, escritura y consulta. Si falla por incompatibilidad del proveedor, detén el trabajo de persistencia y presenta el diagnóstico. No cambies de proveedor ni reduzcas versiones silenciosamente.”

Aceptación y comprobaciones: el gate registra versiones efectivas, resultado de restauración, migración inicial, actualización de esquema, escritura y lectura contra MySQL 8.4.11. No se avanza con persistencia de la aplicación si hay incompatibilidad.

### S2 — Solución y límites arquitectónicos aprobados

Alcance autorizado: solución .NET 10 con Domain, Application, Infrastructure, API, Web, pruebas unitarias y de integración; monolito modular/Clean Architecture; CQRS explícito sin MediatR; endpoint de salud; sin lógica de negocio no aprobada.

Texto exacto del usuario: “Si la validación funciona, implementa el resto de la fundación técnica conforme a `SPEC-001`: solución y proyectos .NET, arquitectura modular, API, Blazor Web App con `InteractiveServer`, configuración local mediante Docker Compose, persistencia, migraciones y pruebas.”

Aceptación y comprobaciones: restore/build; revisar referencias y dependencias; pruebas unitarias; endpoint de salud HTTP 200. Aplican SPEC-001-AC-001, AC-002 y AC-003.

### S3 — Blazor Web y transporte HTTP tipado

Alcance autorizado: Blazor Web App en `InteractiveServer`; comunicación servidor-servidor a API mediante cliente HTTP tipado e `IHttpClientFactory`, con URL configurable `BackendApi:BaseUrl` / `BackendApi__BaseUrl`; respuesta de API demostrable y errores controlados.

Texto exacto del usuario: “Mantén las llamadas de Blazor a la API mediante el cliente HTTP tipado del servidor, según la arquitectura aprobada.”

Aceptación y comprobaciones: verificar modo de renderizado, página inicial accesible y una llamada satisfactoria Web→API sin acceso de Web a Infrastructure/DbContext. Aplican SPEC-001-AC-004 y AC-005.

### S4 — Persistencia e integración MySQL real

Alcance autorizado: proveedor Oracle `MySql.EntityFrameworkCore` `10.0.9`, EF Core/tools/design `10.0.13`, dependencia `MySql.Data` `26.7.0`; migraciones versionadas en Infrastructure; base de integración separada `talentassistant_tests`; modelo de prueba únicamente en IntegrationTests mientras no existan entidades de negocio. No usar proveedor InMemory.

Texto exacto del usuario: “Usa MySQL real para las pruebas de integración, con una base de datos separada `talentassistant_tests`. No sustituyas estas pruebas por el proveedor InMemory de EF Core.”

Aceptación y comprobaciones: migración/aplicación/repetición, conexión, escritura y consulta sintéticas sobre MySQL real; `dotnet test tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj`. Aplican SPEC-001-AC-006, AC-007 y AC-010.

### S5 — Configuración local segura y alcance

Alcance autorizado: Compose únicamente para MySQL con imagen oficial fijada por digest, bind `127.0.0.1`, volumen nombrado persistente, healthcheck y secretos locales fuera de Git; documentar expresamente operaciones destructivas. No implementar negocio, autenticación, agentes, LLM ni integraciones de IA.

Texto exacto del usuario: “No implementes todavía funcionalidades de negocio, autenticación, agentes, LLM ni integraciones de IA. No añadas MediatR ni infraestructura innecesaria.” También: “No introduzcas secretos en el repositorio. Limita el puerto de MySQL a `127.0.0.1`, conserva el volumen persistente y documenta las operaciones destructivas.”

Aceptación y comprobaciones: configuración sin credenciales reales; `.env` excluido; Compose fija imagen/digest y puerto local; instrucciones distinguen `down` de `down --volumes`; carga sintética aplazada si aún no hay entidades. Aplican SPEC-001-AC-008, AC-011, AC-012 y AC-013.

### S6 — Verificación e informe sin entrega Git

Alcance autorizado: ejecutar todas las comprobaciones aplicables y reportar criterios, versiones, comandos, resultados, archivos y bloqueos sin declarar pruebas no ejecutadas.

Texto exacto del usuario: “Ejecuta las comprobaciones aplicables: restore, build, pruebas unitarias e integración, migraciones y `git diff --check`. No hagas commits ni push.” También: “Si un bloqueo impide continuar, detente en ese punto, explica la causa y propone la siguiente decisión técnica sin aplicarla unilateralmente.”

Aceptación y comprobaciones: restore, build, pruebas unitarias e integración, migraciones, ejecución local API/Web y `git diff --check`; cada resultado queda observado y documentado. No crear commits ni hacer push.

## Tasks

| ID | Spec | Ruta | Estado | Commit / evidencia |
|---|---|---|---|---|
| T1 | S1 | Inline | Completada: gate real aprobado; no se avanzó a persistencia de aplicación antes de terminarlo | Sin commit (prohibido por usuario); probe temporal: 2 migraciones aplicadas, repetición no-op, MySQL 8.4.11, escritura/consulta PASS |
| T2 | S2 | Inline | Implementada: solución y siete proyectos con referencias previstas; build pasa. Prueba arquitectónica xUnit sigue bloqueada por Application Control | Sin commit (prohibido por usuario); restore/build finales aprobados, prueba GREEN no ejecutada |
| T3 | S3 | Inline | Implementada: API `/health`, Blazor InteractiveServer y cliente HTTP tipado; smoke HTTP de API y página Web pasó. Launch profiles alineados; revisión manual de teclado, nombres accesibles y contraste aprobada | Sin commit (prohibido por usuario); API y Web devolvieron HTTP 200 |
| T4 | S4 | Inline | Parcial: proveedor, DbContext y migración de aplicación aplicados; base `talentassistant_tests` creada y AC-006 verificado con centinela. Migración/test xUnit de integración bloqueados por carga del ensamblado | Sin commit (prohibido por usuario); MySQL 8.4.11 confirmado; pruebas xUnit no ejecutadas |
| T5 | S5-S6 | Inline | Implementada: Compose, protección de configuración, README y guía local; volumen persistente activo | Sin commit (prohibido por usuario); Compose saludable, escaneo estático sin credenciales, checks de whitespace sin hallazgos |

## Log

### L1 — Solicitud original

# Implementación de SPEC-001 — Talent Assistant

Implementa la fundación técnica definida en `specs/001-project-foundation/spec.md`, respetando `AGENTS.md`, los ADR aprobados y la documentación existente.

## Reglas obligatorias

1. Antes de modificar archivos, inspecciona el estado de Git y todos los documentos relevantes. Hay archivos y directorios sin seguimiento; consérvalos y no los sobrescribas ni elimines sin justificación.
2. Considera los requisitos, los criterios de aceptación y los ADR como el contrato de implementación. No reabras decisiones aprobadas sin evidencia técnica.
3. **Primer bloqueo obligatorio:** valida la compatibilidad real de .NET 10, EF Core 10.0.13, `MySql.EntityFrameworkCore` 10.0.9, `MySql.Data` 26.7.0 y MySQL 8.4.11.
4. Ejecuta una prueba real de persistencia que incluya migración, escritura y consulta. Si falla por incompatibilidad del proveedor, detén el trabajo de persistencia y presenta el diagnóstico. No cambies de proveedor ni reduzcas versiones silenciosamente.
5. Si la validación funciona, implementa el resto de la fundación técnica conforme a `SPEC-001`: solución y proyectos .NET, arquitectura modular, API, Blazor Web App con `InteractiveServer`, configuración local mediante Docker Compose, persistencia, migraciones y pruebas.
6. Mantén las llamadas de Blazor a la API mediante el cliente HTTP tipado del servidor, según la arquitectura aprobada.
7. Usa MySQL real para las pruebas de integración, con una base de datos separada `talentassistant_tests`. No sustituyas estas pruebas por el proveedor InMemory de EF Core.
8. No implementes todavía funcionalidades de negocio, autenticación, agentes, LLM ni integraciones de IA. No añadas MediatR ni infraestructura innecesaria.
9. No introduzcas secretos en el repositorio. Limita el puerto de MySQL a `127.0.0.1`, conserva el volumen persistente y documenta las operaciones destructivas.
10. Ejecuta las comprobaciones aplicables: restore, build, pruebas unitarias e integración, migraciones y `git diff --check`. No hagas commits ni push.

## Entrega

Informa los resultados con evidencia verificable: criterios de aceptación cumplidos y pendientes, comandos ejecutados, versiones efectivamente utilizadas, pruebas aprobadas y fallidas, archivos creados o modificados y bloqueos encontrados.

No declares una prueba aprobada si no fue ejecutada. Si un bloqueo impide continuar, detente en ese punto, explica la causa y propone la siguiente decisión técnica sin aplicarla unilateralmente.

### L2 — Habilitación autorizada

El usuario respondió “si” a la solicitud de autorización para instalar SDK/runtime desde Microsoft, restaurar desde nuget.org y descargar/ejecutar la imagen MySQL fijada desde Docker Hub con acceso público anónimo. Docker Desktop no estaba iniciado durante la primera comprobación; se lanzó localmente y su daemon quedó disponible. Se instaló .NET SDK 10.0.402 en el directorio de herramientas temporal aprobado; incluye runtimes .NET y ASP.NET Core 10.0.13. La imagen exacta `mysql:8.4.11@sha256:24989179f58856db763f418010c1169fecc944e3ec1ea7e921572d83eb070f70` se descargó y su digest fue verificado. Todavía no se restauraron paquetes ni se ejecutó el gate de persistencia.

### L3 — Gate de compatibilidad aprobado

La restauración del probe temporal resolvió EF Core/Design `10.0.13`, `MySql.EntityFrameworkCore` `10.0.9`, `MySql.Data` `26.7.0` y `dotnet-ef` `10.0.13` bajo SDK `10.0.402`. En MySQL real `8.4.11`, `InitialCreate` y `AddNotes` se generaron/aplicaron; repetir `dotnet ef database update` informó que no había migraciones pendientes. La ejecución final verificó `Server=8.4.11; Provider=MySql.EntityFrameworkCore; Migrations=2; WriteRead=PASS; RecordId=1`. Hubo un error inicial `CS5001` en el probe temporal porque aún no tenía `Program.cs`; se añadió el punto de entrada y luego build, migraciones y prueba pasaron. El contenedor temporal quedó detenido y su volumen nombrado se conservó. Evidencia: probe temporal aislado fuera del repositorio; no se cambió proveedor ni versión.

### L4 — Bloqueo de ejecución de pruebas de arquitectura

Se generó `TalentAssistant.sln`, los proyectos Domain, Application, Infrastructure, API y dos proyectos xUnit; se añadieron las referencias de proyecto previstas y la prueba de límites Domain/Application. `dotnet restore TalentAssistant.sln` pasó. La primera ejecución de la prueba falló de forma esperada en `Assert.Contains`: Application aún no tenía referencia CLR a Domain (RED observado). Se añadió el marcador de ensamblado que materializa esa referencia. `dotnet build` de UnitTests, con `BaseOutputPath` en el directorio temporal aprobado, pasó con 0 advertencias y 0 errores. Después, `dotnet test` no pudo cargar el ensamblado y devolvió `FileLoadException` 0x800711C7 (“Una directiva de Control de aplicaciones bloqueó este archivo”), tanto desde `bin` del repositorio como desde la salida temporal. No se desactivó ni intentó eludir la política. La prueba GREEN, los proyectos Web, persistencia de aplicación y demás verificaciones siguen pendientes; se detiene la implementación hasta disponer de un runner aprobado para ejecutar los ensamblados de prueba.

### L5 — Continuación segura sin ejecutar xUnit

Se completó la solución de siete proyectos y se verificaron sus referencias: Application→Domain; Infrastructure→Application/Domain; API→Application/Infrastructure; Web sin referencia a Infrastructure; UnitTests→Domain/Application; IntegrationTests→Infrastructure. El test arquitectónico sigue en el código, sin volver a ejecutar `dotnet test`.

Persistencia: Infrastructure fija `Microsoft.EntityFrameworkCore`/Design `10.0.13`, `MySql.EntityFrameworkCore` `10.0.9` y `MySql.Data` `26.7.0`; API incluye EF Design `10.0.13`, requerido por EF CLI en el startup project. `dotnet-ef` `10.0.13` está en `.config/dotnet-tools.json`. Se generó `InitialCreate`; aplicó `utf8mb4` y el historial EF sin inventar tablas de negocio. `dotnet ef database update` conectó a `talentassistant_dev` usando `ConnectionStrings__TalentAssistant`, aplicó la migración y la repetición indicó que la base ya estaba actualizada. Tras quitar el factory de diseño que no leía User Secrets, el mismo comando por el host API volvió a compilar y consultar el historial correctamente.

Compose/configuración: `.env.example`, `.gitignore`, `compose.yaml` y el SQL de inicialización se añadieron. La imagen oficial fijada por digest inició saludable; MySQL devolvió `8.4.11`. El puerto publicado es `127.0.0.1:3306`; el volumen `talentassistant_mysql_data` permanece creado. La consulta con el usuario de aplicación devolvió `8.4.11` y `talentassistant_tests`. Las credenciales sintéticas de la sesión se mantuvieron en un archivo temporal fuera del repositorio; no se creó `.env` en la raíz.

Web/API: Blazor usa `InteractiveServer` y un `BackendApiClient` de `IHttpClientFactory` configurable por `BackendApi:BaseUrl`/`BackendApi__BaseUrl`. El smoke `GET /health` devolvió HTTP 200 y `{"status":"healthy"}`. El smoke de la página Web devolvió HTTP 200 y mostró “The API is responding.”; los logs de Web registraron la llamada al endpoint API con HTTP 200. Se retiraron `Counter`, `Weather`, `NavMenu`, estilos y Bootstrap del template únicamente tras confirmar que no quedaban referencias; se conservaron los componentes de error y reconexión de Blazor. No se ejecutó revisión manual de teclado/contraste.

Comprobaciones observadas en esta continuación:

- `dotnet restore TalentAssistant.sln`: aprobado; todos los proyectos actualizados.
- `dotnet build TalentAssistant.sln --configuration Debug --no-restore`: aprobado; 0 warnings y 0 errors.
- `dotnet tool restore` y `dotnet tool run dotnet-ef --version`: aprobados; EF CLI `10.0.13`.
- `dotnet ef migrations add InitialCreate` y `dotnet ef database update` de la aplicación: aprobados contra MySQL real; repetición no-op.
- `docker compose ... config --quiet` y `docker compose ... up -d --wait mysql`: aprobados; servicio saludable, volumen conservado.
- `curl.exe` directo a API y Web: HTTP 200; página mostró estado de API satisfactorio.
- `git diff --check`: exit 0, pero el repositorio no tiene commits y por tanto no hay diff rastreado. El escaneo de whitespace de los textos nuevos sin seguimiento tampoco halló espacios finales.
- Escaneo estático de referencias: no aparecieron dependencias/código de IA, agentes, Python, MCP, n8n ni MediatR; `.env` no existe en la raíz y los cambios no contienen credenciales literales.
- `dotnet test` y la generación/aplicación de migraciones del modelo `PersistenceProbeContext` no se ejecutaron: requieren cargar el ensamblado de integración bloqueado. No se cambió su ubicación ni se empleó otro runner.

#### Estado SPEC-001-AC

| Criterio | Estado | Evidencia / pendiente |
|---|---|---|
| AC-001 | Aprobado | Restore/build de los siete proyectos con SDK `10.0.402`. |
| AC-002 | Bloqueado | Referencias inspeccionadas; prueba arquitectónica GREEN pendiente por `0x800711C7`. |
| AC-003 | Aprobado | `GET /health` devolvió HTTP 200 y JSON de estado saludable. |
| AC-004 | Aprobado | Snapshot accesible con landmarks/títulos; Tab recorrió skip link, marca y Overview con foco visible; contrastes calculados superan 4.5:1. |
| AC-005 | Aprobado | Página Web HTTP 200 mostró el resultado del cliente HTTP tipado; llamada del Web host a API respondió HTTP 200. |
| AC-006 | Aprobado | Migración EF con configuración externa; sentinel sintético produjo HTTP 503 genérico sin filtrarse; User Secrets cargó un valor sintético en Development y se limpió después. |
| AC-007 | Bloqueado | Migración de aplicación aplicada en `talentassistant_dev`; migración del contexto de prueba en `talentassistant_tests` pendiente porque requiere cargar IntegrationTests. |
| AC-008 | Aprobado por aplazamiento documentado | No hay entidades de negocio; seed diferido en la guía conforme a ADR-0002. |
| AC-009 | Bloqueado | Pruebas unitarias xUnit no ejecutadas por Application Control. |
| AC-010 | Bloqueado | Código de integración compiló, pero su migración y prueba real no se ejecutaron por la política. |
| AC-011 | Pendiente | Guía local escrita; aún no se recorrió desde un checkout limpio. |
| AC-012 | Aprobado | Inspección estática sin dependencias ni servicios de IA/agentes. |
| AC-013 | Aprobado | ADR-0001..ADR-0005 y decision-register comparados con la implementación; ADR-0004 actualizado solo con evidencia, sin reabrir decisiones. |

No se hicieron commits ni push. El modo RDD está desactivado globalmente. El registro sigue en curso: no se declara SPEC-001 completo.

### L6 — Revisión estática independiente y corrección de puertos

El verificador independiente trabajó en modo de solo lectura; no ejecutó pruebas, probes ni ensamblados xUnit. Confirmó que el repositorio no tiene commits, así que no existe baseline commit para comparar. No halló filtraciones de secretos, dependencias de IA/MediatR ni referencias Web→Infrastructure.

Se corrigió la discrepancia señalada entre launch profiles y configuración/documentación: API ahora usa `http://127.0.0.1:5101`, Web `http://127.0.0.1:5102` y `TalentAssistant.Api.http` usa el mismo endpoint. El verificador también señaló que la generación de migraciones del contexto de prueba está sin verificar. La guía ahora indica que `PersistenceProbeContextFactory` vive en el proyecto objetivo y que la API es el startup project ejecutable; la generación sigue bloqueada/no ejecutada porque cargaría el ensamblado IntegrationTests bajo la política actual.

### L7 — Solicitud de continuación (2026-10-14)

Continúa la implementación de SPEC-001 desde el estado parcial registrado en `odd/tasks/spec-001-foundation.md`.

### Restricción de seguridad

Windows Application Control está bloqueando la carga de ensamblados xUnit con `FileLoadException 0x800711C7`.

No desactives, debilites ni intentes eludir Application Control. No cambies la ubicación de los ensamblados ni utilices mecanismos alternativos para evitar la política.

### Trabajo autorizado

1. Inspecciona el estado actual de Git, el logbook, la solución y los proyectos existentes.
2. Conserva todos los documentos y archivos preexistentes, incluidos los que no están seguidos por Git.
3. Continúa las partes de SPEC-001 que no requieren ejecutar pruebas:
   - Completar la estructura de proyectos y sus referencias.
   - Implementar la persistencia de la aplicación con las versiones aprobadas.
   - Crear la configuración local de MySQL mediante Docker Compose.
   - Añadir la configuración y documentación de ejecución.
   - Eliminar archivos de plantilla únicamente cuando se confirme que son innecesarios.
   - Mantener la prueba de límites arquitectónicos y preparar su verificación posterior.
4. Conserva las versiones ya validadas: .NET SDK 10.0.402, EF Core 10.0.13, `MySql.EntityFrameworkCore` 10.0.9, `MySql.Data` 26.7.0 y MySQL 8.4.11.
5. No cambies de proveedor, no reduzcas versiones, no introduzcas autenticación ni funcionalidades de negocio y no añadas infraestructura de IA.
6. Puedes ejecutar restauraciones y compilaciones que estén permitidas por la política. Si una comprobación requiere ejecutar un ensamblado bloqueado, regístrala como bloqueada y continúa con las tareas independientes.
7. No marques pruebas ni criterios de aceptación como aprobados sin evidencia de ejecución. Mantén explícitamente separados los estados: aprobado, pendiente, bloqueado y no ejecutado.
8. No hagas commits ni push.

### Entrega

Actualiza el logbook con los cambios realizados, los comandos ejecutados, los resultados observados, los criterios de aceptación y los bloqueos pendientes.

Al finalizar, indica qué queda listo para revisión y cuál es el procedimiento aprobado que debe completar el administrador o responsable de seguridad antes de reanudar las pruebas.

### L8 — Solicitud de cierre (2026-10-14)

# Cierre de SPEC-001 — Talent Assistant

Continúa desde el estado documentado en `odd/tasks/spec-001-foundation.md`.

## Objetivo

Cerrar las verificaciones pendientes de la fundación técnica sin declarar cumplidos criterios que no tengan evidencia suficiente.

## Fase 1: revisión estática y configuración

1. Inspecciona `AGENTS.md`, `specs/001-project-foundation/spec.md`, los ADR, los proyectos, la configuración local y el logbook.
2. Conserva todos los archivos existentes y el trabajo no versionado. No hagas commits ni push.
3. Resuelve AC-006 con una verificación reproducible del tratamiento de secretos mediante un valor centinela sintético. No uses credenciales reales ni expongas el centinela en los logs.
4. Revisa los archivos de configuración, `.gitignore`, `.env.example`, User Secrets y las cadenas de conexión. Distingue entre secretos excluidos del control de versiones y valores que la aplicación realmente puede cargar.
5. Completa la revisión de los ADR y registra cualquier discrepancia con evidencia concreta.
6. Prepara un procedimiento reproducible desde un checkout limpio para AC-011. No elimines el directorio de trabajo actual ni sobrescribas archivos sin comprobar primero cómo preservar los cambios.
7. Revisa AC-004 manualmente si puedes inspeccionar la interfaz. Si no puedes comprobar navegación por teclado, etiquetas y contraste de forma fiable, registra exactamente lo que queda pendiente.

## Fase 2: bloqueo de pruebas

Application Control bloqueó anteriormente la carga de ensamblados xUnit con `FileLoadException 0x800711C7`.

No desactives, debilites ni eludas esta política. No muevas ensamblados ni sustituyas el runner para sortearla.

Mientras siga bloqueada la ejecución, completa solo tareas independientes y documenta el bloqueo. No declares aprobadas las pruebas por el hecho de que la solución compile.

## Fase 3: pruebas tras autorización

Reanuda esta fase únicamente cuando el responsable de seguridad confirme que la ejecución está autorizada mediante el procedimiento corporativo aprobado.

1. Ejecuta `dotnet test` sobre la solución y registra los resultados.
2. Comprueba que la prueba de límites arquitectónicos pasa.
3. Genera y aplica la migración del contexto de integración previsto en la documentación.
4. Ejecuta las pruebas de integración contra la instancia MySQL real y la base `talentassistant_tests`.
5. Comprueba que las pruebas son repetibles y que no dependen de datos de negocio ni de la base de desarrollo.
6. Ejecuta de nuevo restore, build, tests y las comprobaciones de consistencia que correspondan.
7. Investiga y corrige los fallos encontrados sin cambiar silenciosamente las versiones o decisiones de arquitectura aprobadas.

## Criterios de cierre

Mantén estados separados para aprobado, parcial, bloqueado, pendiente y no ejecutado. Actualiza el logbook con cada comando, su resultado y la evidencia disponible.

No marques SPEC-001 como completo mientras haya criterios obligatorios pendientes o bloqueados. Si el bloqueo de seguridad persiste, entrega un informe parcial preciso y deja el siguiente paso explícito.

No implementes funcionalidades de negocio, autenticación ni IA como parte de este cierre.

### L9 — Verificación reproducible de AC-006

Se añadió el contrato Application `IDatabaseAvailabilityCheck`, su implementación Infrastructure `MySqlDatabaseAvailabilityCheck` y el endpoint diagnóstico API `GET /health/database`. Ante fallo de conexión, Infrastructure registra solo el tipo de excepción y API responde HTTP 503 con `ProblemDetails` genérico; no se registran mensaje de excepción, cadena de conexión ni stack trace.

Se añadió `scripts/verify-database-secret-redaction.ps1`. El script genera un centinela aleatorio sintético, lo pasa solo mediante `ConnectionStrings__TalentAssistant`, inicia la API en su ubicación normal, consulta `/health/database`, captura la salida del proceso fuera del repositorio, comprueba respuesta/logs por centinela y stack trace, elimina sus logs temporales y solo informa PASS/FAIL genérico.

RED observado: antes de implementar `/health/database`, el script no recibió el HTTP 503 esperado. Hubo además un primer error del script porque Windows PowerShell no resolvió `System.Net.Http.HttpClientHandler`; se cambió el cliente local a `curl.exe` directo, sin mover ensamblados ni usar xUnit. GREEN: `.\scripts\verify-database-secret-redaction.ps1` devolvió “PASS: synthetic authentication failure returned a generic HTTP 503; sentinel and stack trace were absent from the response and captured API logs.” El valor centinela no apareció en la salida. Build de la solución posterior al endpoint: 0 warnings, 0 errors. No se ejecutó `dotnet test`.

AC-006 pasa a aprobado: EF migró con configuración externa, la conexión se configuró mediante un origen que la aplicación carga y la verificación centinela comprobó respuesta genérica y ausencia del valor en logs capturados. User Secrets tiene `UserSecretsId` en API y se carga en `Development`; el valor real no se creó ni versionó en esta sesión. `.env` está ignorado, no existe en la raíz y `.env.example` deja los campos de contraseña vacíos hasta configurarlos.

También se verificó el origen User Secrets de forma efectiva: la ruta de secretos para el `UserSecretsId` recién creado no existía; se almacenó temporalmente una cadena sintética, se inició API en `Development` sin `ConnectionStrings__TalentAssistant` de entorno y `/health/database` devolvió HTTP 200. La contraseña temporal no apareció en logs capturados; se eliminó la clave y el archivo vacío creado por esta comprobación. No se usaron credenciales reales ni se dejó un secreto local persistente.

AC-004 pasa a aprobado tras inspección manual con Playwright: el snapshot mostró banner, navegación con nombre, main, títulos y contenido; la secuencia Tab recorrió skip link, enlace de marca y Overview con foco visible. El texto de estado “The API is responding.” está en el nodo `role=status`. Ratios calculados desde los tokens CSS usados: tema claro body 12.83:1, muted 6.47:1, accent 6.38:1 y foco 6.84:1; tema oscuro body 14.42:1, muted 8.28:1, accent 8.30:1 y foco 8.70:1. El navegador no registró errores ni advertencias de consola. No se ejecutó xUnit.

Se revisaron ADR-0001..ADR-0005 y el registro de decisiones: no se cambió ninguna decisión aprobada. ADR-0004 contenía afirmaciones de evidencia de migración todavía no ejecutada; se actualizó para distinguir el probe aislado aprobado, `InitialCreate` técnico de aplicación y migraciones de IntegrationTests aún bloqueadas. La compatibilidad publicada específica de Oracle con MySQL 8.4.11 sigue sin matriz exacta del fabricante.

### L10 — Cierre de AC-006 y ajuste del healthcheck de Compose

La revisión independiente no encontró blockers de AC-006. Su advisory sobre el healthcheck fue atendido: `compose.yaml` ahora usa `mysqladmin ping` sin pasar `MYSQL_ROOT_PASSWORD` como argumento. Compose recreó solo el contenedor, conservó el volumen `talentassistant_mysql_data` y la consulta posterior confirmó `8.4.11`/`talentassistant_tests`. Se mantuvo Application Control activo.

La verificación de User Secrets fue temporal y sintética: se comprobó que API en `Development`, sin `ConnectionStrings__TalentAssistant` de entorno, cargó la cadena del almacén User Secrets y conectó; el valor se quitó y no queda `secrets.json` para el `UserSecretsId`. El centinela AC-006 sigue verificado por `scripts/verify-database-secret-redaction.ps1`. No se ejecutó xUnit ni la migración de IntegrationTests. AC-006 permanece aprobado; AC-002, AC-007, AC-009 y AC-010 siguen bloqueados por carga del ensamblado; AC-011 queda pendiente de un checkout limpio. No commits ni push.

### L11 — Pausa del cierre y verificación selectiva (2026-10-15)

Solicitud del usuario, textual:

Vamos a pausar el cierre de SPEC-001. No quiero seguir invirtiendo tiempo en resolver el bloqueo de Smart App Control por ahora.

1. No cambies políticas de Windows, certificados, configuraciones de seguridad ni el runner de pruebas.
2. Conserva todo el trabajo actual y actualiza el logbook indicando que las pruebas están bloqueadas por una restricción de firma de ensamblados.
3. Verifica únicamente que la solución restaura, compila y que los smoke tests HTTP existentes siguen funcionando, siempre que estas comprobaciones no requieran modificar la política de seguridad.
4. No declares SPEC-001 completo; conserva los criterios pendientes y bloqueados.
5. Detén los cambios de infraestructura y prepara una propuesta breve para la primera funcionalidad vertical de Talent Assistant, reutilizando la arquitectura existente y definiendo alcance, interfaz, API, persistencia y criterios de aceptación.

No implementes esa funcionalidad todavía. Quiero revisar primero la propuesta.

#### Diagnóstico y comprobaciones

- `CiTool.exe -lp` ejecutado por el usuario desde PowerShell elevado confirma activa y aplicada la política de plataforma firmada `VerifiedAndReputableDesktop` (`0283ac0f-fff1-49ae-ada1-8a933130cad6`); la política Evaluation está inactiva. `VerifiedAndReputableDesktopFlightSupplemental` también está activa. La documentación de Microsoft identifica la política base como Smart App Control en enforcement. Los eventos del bloqueo pertenecen a Code Integrity; no se encontraron eventos coincidentes en los canales de AppLocker consultados durante los últimos 30 días.
- Los eventos Code Integrity 3033, 3077 y 3089 correlacionan `testhost.exe` con la carga de `TalentAssistant.UnitTests.dll`. El evento 3089 registra cero firmas y publisher/issuer `Unknown`; el evento 3077 atribuye el bloqueo a `VerifiedAndReputableDesktop` y registra `0xc0e90002`. La DLL fue bloqueada desde `bin` y desde la salida temporal; no se cambió su ubicación ni se modificó la política. Microsoft documenta que Smart App Control no ofrece excepción por aplicación y recomienda firma válida: [Inbox App Control policies](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/operations/inbox-appcontrol-policies) y [Smart App Control FAQ](https://support.microsoft.com/topic/what-is-smart-app-control-285ea03d-fa88-4d56-882e-6698afdb7003).
- El `dotnet.exe` disponible en `PATH` era SDK `10.0.401`; `global.json` requiere `10.0.402` con `rollForward: disable`, por lo que el primer `dotnet restore` no pudo iniciar. Se utilizó el SDK `10.0.402` ya instalado en el directorio temporal aprobado, sin cambiar `global.json`:
  - `& "$env:LOCALAPPDATA\Temp\opencode\dotnet-sdk-10.0.402\dotnet.exe" restore TalentAssistant.sln --verbosity minimal`: PASS, todos los proyectos estaban actualizados.
  - `& "$env:LOCALAPPDATA\Temp\opencode\dotnet-sdk-10.0.402\dotnet.exe" build TalentAssistant.sln --configuration Debug --no-restore --verbosity minimal`: PASS, 0 advertencias y 0 errores. Los ensamblados UnitTests e IntegrationTests solo se compilaron; no se ejecutaron.
- El primer inicio de API para el smoke, sin `ConnectionStrings__TalentAssistant`, terminó con `InvalidOperationException` porque `Program.cs` exige configurar esa clave; no llegó a una solicitud HTTP. Se repitió con una cadena sintética y efímera solo en el entorno del proceso, sin guardar secretos ni modificar archivos de configuración: `GET http://127.0.0.1:5101/health` devolvió HTTP 200 y `{"status":"healthy"}`. La página Web en `http://127.0.0.1:5102/` devolvió HTTP 200 y mostró el estado “The API is responding.”. Los procesos iniciados y los logs temporales propios se limpiaron; se restauró el entorno del proceso.
- No se ejecutaron `dotnet test`, migraciones de IntegrationTests ni pruebas contra MySQL. No se cambiaron políticas de Windows, certificados, configuración persistente ni runner; tampoco se hicieron commits o push.

#### Estado conservado

AC-001 sigue aprobado tras restore/build. AC-003 y AC-005 tienen smoke HTTP PASS en esta sesión. AC-002, AC-007, AC-009 y AC-010 permanecen bloqueados o sin prueba por la restricción de firma/carga de ensamblados; AC-011 sigue pendiente de verificación desde un checkout limpio. Los demás estados de la tabla AC no cambian. SPEC-001 no está completo y su cierre queda pausado por decisión del usuario. La propuesta de vertical se entrega fuera de SPEC-001 para revisión; no se incorpora como requisito aprobado ni se implementa.
