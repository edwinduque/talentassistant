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
| T2 | S1, S2, S4 | Inline | Planificada; el usuario autorizó continuar con T1 parcial | Iniciar tras el commit de T1 y elegir cadena |
| T3 | S3, S4, S5 | Inline | Planificada | Depende de T1/T2 |

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
