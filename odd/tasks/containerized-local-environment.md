# Seguimiento ODD: entorno local contenedorizado

Objetivo: construir un perfil local Compose para API, Web y MySQL que sirva fixtures sintéticos persistidos mediante `IHcmAttendanceSource`.
Rama: `feat/containerized-local-environment`; entrega: `feature-branch-chain`; runner: SDK `10.0.402`, `dotnet build`, checks de Compose y smoke HTTP/Web; ejecución xUnit requiere autorización explícita.

## Specs

### S1 — Stack local con contenedores

Alcance: Dockerfile propio para API, Web y MySQL; Docker Compose coordina los servicios con los pins aprobados y expone puertos únicamente en loopback.

Texto exacto del usuario: “que se pueda dockerizar api, web y bd. con los respectivos dockerfile”.

Aceptación: `docker compose config --quiet` pasa; las tres imágenes se construyen con las versiones fijadas; MySQL, API y Web inician y responden en las URLs locales documentadas. No se crea un perfil de despliegue productivo.

### S2 — Semilla sintética leída por la API

Alcance: el perfil Compose selecciona una fuente MySQL detrás de `IHcmAttendanceSource`; el perfil host/tests conserva la fuente en memoria. MySQL almacena solo un dataset sintético de respuestas HCM, no módulos HCM.

Texto exacto del usuario: “se pueda tener una semilla con los datos sinteticos”. Confirmación exacta: “Sí, API lee MySQL (recomendado)”.

Aceptación: el one-shot `db-init` aplica migraciones y hace upsert transaccional de un único dataset versionado; repetirlo actualiza el snapshot completo sin duplicados, filas huérfanas, borrado general ni destrucción del volumen. La API entrega los cinco fixtures esperados y preserva resultados, estados, evidencia y filtros locales.

### S3 — Ejecución local documentada

Alcance: actualizar `docs/development/local-setup.md` y las referencias de ejecución del README con prerequisitos, configuración de secretos, build/start, URLs, seed, parada, persistencia y reset destructivo.

Texto exacto del usuario: “la documentación necesaria para ejecutar en local sea actualizada”.

Aceptación: otra persona puede seguir el runbook desde un checkout limpio sin pasos implícitos; los comandos documentados se ejecutan y sus resultados corresponden al stack verificado.

### S4 — Mantener límites del HCM y de arquitectura

Alcance: preservar las reglas HCM en el HCM; guardar solo el payload sintético mínimo. No introducir datos reales, módulos de empleados/TimeCheck/TimeOff/Tasks, reglas locales, dependencias, LLM/agentes, autenticación, TLS ni exposición en redes compartidas.

Aceptación: Application depende solo de `IHcmAttendanceSource`; EF Core/Infrastructure permanecen detrás del límite aprobado; la base de pruebas queda separada y no recibe la semilla demo. El perfil host/in-memory de SPEC-002 sigue disponible.

### S5 — Migración y seed persistente seguros

Alcance: añadir una migración aditiva para una fila de dataset sintético con clave fija, versión de payload y JSON serializado; no cambiar ni eliminar datos existentes.

Aceptación: migración y seed funcionan tanto con volumen vacío como con el volumen local existente; un payload de versión desconocida falla de forma controlada; ninguna operación exige `docker compose down --volumes`.

Risk: item 1 (migración/seed sobre persistencia), item 5 (entorno de ejecución contenedorizado). T1 requiere verificación independiente después de sus checks propios.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S2, S4, S5 | Inline | Implementada; build, smoke y verificador pasan; suite completa 11/12, limitación aceptada para continuar | `4b82fe2baa5adddcc90cb0b1e9ad133256ef218d` |
| T2 | S1, S2, S4 | Inline | Implementada; imágenes, Compose, seed y smoke HTTP/Web pasan; verificador independiente sin bloqueos | `bcfec3a26452ee5729a0c804ab7a28f488c815c0` |
| T3 | S3, S4, S5 | Inline | Implementada; guía, README y referencias SDD actualizados; comprobaciones estructurales y Compose pasan | `b910561060e8fe33860934768c5a4da04d6821d4` |

Forecast inicial: aproximadamente 600–800 líneas authored entre implementación, pruebas y documentación; archivos generados excluidos. El usuario eligió `feature-branch-chain`; el umbral de ~400 líneas es heurístico, no un límite de tamaño.

## Log

### L1 — Solicitud original (2026-10-15)

quiero agregar un nuevo requerimiento. que se pueda dockerizar api, web y bd. con los respectivos dockerfile y se pueda tener una semilla con los datos sinteticos. quiero manejarlo complaramente con el flujo sdd y que la documentación necesaria para ejecutar en local sea actualizada

### L2 — Decisión de fuente de semilla (2026-10-15)

Texto exacto del usuario: “Sí, API lee MySQL (recomendado)”.

### L3 — Aprobación para planificación técnica (2026-10-15)

Texto exacto del usuario: “si”. Se aprobó SPEC-004 y ADR-0006 para avanzar a planificación de implementación; esto no inicia cambios de código hasta la aprobación del plan.

### L4 — Diseño técnico aprobado para planificar (2026-10-15)

El diseño propuesto usa un dataset JSON versionado en una fila MySQL, reemplazado por upsert transaccional completo desde un one-shot `db-init`; Compose selecciona la fuente MySQL y host/tests conservan la fuente en memoria. No se modifican SPEC-001 ni ADR-0004; ADR-0006 añade un perfil local opcional.

### L5 — Aprobación para iniciar T1 (2026-10-15)

Texto exacto del usuario: “si”. Se autoriza iniciar T1 según este plan; el resto de tareas sigue su orden y alcance aprobados.

### L6 — Implementación T1 y evidencia inicial (2026-10-15)

Se añadió la fila `SyntheticAttendanceDataset` con payload JSON/versionado, el serializer, el adaptador MySQL scoped, el inicializador `--initialize-local-data` y la migración `20261016001733_AddSyntheticAttendanceDataset`. La fuente en memoria permanece como perfil por defecto para host/tests.

Verificación observada: `dotnet build TalentAssistant.sln --configuration Debug --no-restore --verbosity minimal` con SDK `10.0.402` — PASS, 0 warnings/0 errors; `git diff --check` — PASS. El one-shot aplicó la migración en `talentassistant_dev`; la segunda ejecución fue no-op para migraciones y mantuvo un único dataset. Smoke API con fuente MySQL devolvió HTTP 200, cinco fixtures con estados Approved/Pending/Rejected/Cancelled y contexto nulo; rango inválido HTTP 400. El verificador independiente informó S2/S4/S5 MET en una BD desechable aislada; el workspace quedó intacto.

Una prueba de integración que sembraba `talentassistant_tests` se retiró para mantener esa base separada y sin seed demo. El verificador independiente confirmó S2/S4/S5; su único aviso quedó resuelto al retirar esa prueba. T2 y T3 no han comenzado. No hay commit ni push de este feature.

### L7 — Autorización y resultado de xUnit (2026-10-15)

Texto exacto del usuario: “si”.

La prueba enfocada `dotnet test tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~SyntheticAttendanceDatasetSerializerTests"` pasó 3/3. La suite `dotnet test TalentAssistant.sln --configuration Debug --no-restore` obtuvo 5/5 UnitTests y 6/7 IntegrationTests; `MySqlPersistenceTests.AppliesMigrationsAndRoundTripsSyntheticDataOnMySql8411` falló antes de operar en MySQL porque no está configurada `ConnectionStrings__TalentAssistantTests`. No se reintentó ni se investigó el bloqueo existente, ni se cambió la configuración de seguridad. T1 queda en Needs your decision por esta prueba de integración pendiente.

### L8 — Pausa inicial de T2/T3 por decisión del usuario (2026-10-15)

Texto exacto del usuario: “Pausar T2 (recomendado)”. T2 y T3 permanecen pausadas hasta que se autorice resolver la configuración/migración de la base dedicada de pruebas o se decida continuar con la evidencia parcial. No se reintentará xUnit.

### L9 — Continuación con evidencia parcial (2026-10-15)

Texto exacto del usuario: “si”. El usuario autorizó continuar a T2 manteniendo documentado el fallo de `MySqlPersistenceTests` por falta de `ConnectionStrings__TalentAssistantTests`. No se autorizó reintentar xUnit ni modificar políticas; T1 puede avanzar como implementada con verificación parcial aceptada. La estrategia de cadena sigue pendiente antes del commit de T1.

### L10 — Reanudación del trabajo (2026-10-16)

Texto exacto del usuario: “continua”. Se reanuda T2 con la limitación de T1 documentada; no se reintenta xUnit ni se cambia la configuración de seguridad.

### L11 — Estrategia de entrega (2026-10-16)

Texto exacto del usuario: “feature-branch-chain (recomendado)”. Los work-unit commits se organizarán en `feat/containerized-local-environment`; no implica push, PR ni actualización de `main`.

### L12 — Work-unit commit de T1 (2026-10-16)

Commit: `4b82fe2baa5adddcc90cb0b1e9ad133256ef218d` — `feat: persist synthetic attendance dataset`. Incluye T1 y los documentos/criterios SDD aprobados. Commit local en `feat/containerized-local-environment`; no se hizo push.

### L13 — Inicio de T2 (2026-10-16)

Se inicia T2 tras el commit de T1 y con estrategia `feature-branch-chain` seleccionada. La limitación de `MySqlPersistenceTests` permanece documentada; no se reejecutará xUnit ni se modificará la configuración de seguridad.

### L14 — Verificación local parcial de T2 (2026-10-16)

`docker compose config --quiet` pasó con valores sintéticos efímeros. El Dockerfile MySQL construyó usando el digest ya presente en el equipo. Las imágenes base `mcr.microsoft.com/dotnet/sdk:10.0.402` y `mcr.microsoft.com/dotnet/aspnet:10.0.13` no están en caché; no se contactó el registro ni se intentó descargarlas. La compilación de las imágenes API/Web queda pendiente de autorización remota explícita.

### L15 — Autorización de imágenes base (2026-10-16)

Texto exacto del usuario: “Sí, autoriza pulls anónimos”. Autoriza descargar los tags .NET fijados desde `mcr.microsoft.com`, sin credenciales; no autoriza todavía la descarga/restauración de paquetes desde NuGet.

### L16 — Autorización de restore NuGet (2026-10-16)

Texto exacto del usuario: “Sí, autoriza restore anónimo”. Autoriza restaurar únicamente los paquetes ya declarados en los proyectos desde `api.nuget.org`, sin credenciales ni nuevas dependencias.

### L17 — Verificación local de T2 (2026-10-16)

`docker compose config --quiet` pasó. Los Dockerfiles de MySQL, API, Web y la reutilización de API para `db-init` construyeron correctamente; `docker compose -p talentassistant-local up --build -d --wait` terminó con éxito, DB healthy y `db-init` exit code 0. La ejecución repetida de `db-init` no reaplicó migraciones ni duplicó el dataset; API MySQL siguió devolviendo cinco fixtures. `/health`, `/health/database`, Web `/`, `/attendance-insights` y `/_framework/blazor.web.js` respondieron HTTP 200. El navegador mostró los cinco casos sintéticos y la consola quedó sin errores tras corregir el asset Blazor.

La primera comprobación de Blazor detectó 404 de `blazor.web.js`; el restore de Web Dockerfile ocurría antes de copiar los `.razor`. Se añadió restore después de copiar fuentes Razor; el rebuild y la comprobación interactiva posterior pasan. xUnit no se reejecutó. No se hizo commit ni push.

### L18 — Verificación independiente de T2 (2026-10-16)

El verificador independiente validó S1/S2/S4/S5 sin bloqueos en un scratch y con volumen temporal; `db-init` repetido conservó una fila y el mismo payload, y el stack principal permaneció intacto. También confirmó que `name: talentassistant-local` hace que `docker compose` sin `-p` apunte al proyecto y servicios ya existentes. `gentle-ai review mode status` informó OFF global; outcome RDD: `disabled/unmanaged`, sin iniciar lifecycle de review.

### L19 — Work-unit commit de T2 (2026-10-16)

Commit: `bcfec3a26452ee5729a0c804ab7a28f488c815c0` — `feat: containerize local API web and MySQL stack`. Incluye Dockerfiles, Compose y evidencia de verificación de T2; commit local en `feat/containerized-local-environment`, sin push.

Risk: item 5 (Dockerfiles y Compose alteran el entorno local y el arranque de servicios persistentes; sin borrar el volumen).

### L20 — Inicio de T3 (2026-10-16)

T3 inicia tras los commits de T1/T2, dentro del alcance original aprobado: documentar el perfil Compose local y conservar claramente el flujo host alternativo. La guía se redacta en español, y sus comandos reflejarán únicamente la configuración observada.

### L21 — Verificación de T3 (2026-10-16)

La guía local documenta prerequisitos, `.env`, build/arranque Compose, migración/seed automático, URLs y smoke HTTP/Web, repetición de `db-init`, persistencia y reset destructivo; README enlaza la guía, SPEC-004 y ADR-0006. ADR-0006 y el registro de decisiones ya no indican que la implementación esté pendiente. `git diff --check`, los destinos de enlaces y `docker compose config --quiet` con variables sintéticas pasaron. Los comandos de stack/seed se apoyan en las pruebas T2 local e independiente; no se detuvo ni eliminó el volumen principal. xUnit no se reejecutó.

Risk: none (solo documentación pasiva; no cambió código ni configuración ejecutable en T3).

### L22 — Work-unit commit de T3 (2026-10-16)

Commit: `b910561060e8fe33860934768c5a4da04d6821d4` — `docs: document Docker Compose local workflow`. Incluye guía local, README, estado ADR/registro y evidencia SPEC-004. Commit local en `feat/containerized-local-environment`, sin push.
