# SPEC-004 — Entorno local contenedorizado y semilla sintética

- **Estado:** Diseño técnico aprobado para planificación de implementación (2026-10-15).
- **Implementación:** T1 y T2 committeadas; T2 verificada local e independientemente; T3 pendiente.
- **Requisitos relacionados:** FR-006, FR-010, FR-018, FR-020; NFR-004, NFR-005, NFR-008, NFR-017.
- **Criterios relacionados:** AC-001, AC-002, AC-007, AC-008, AC-012..AC-015.
- **Decisión arquitectónica:** ADR-0006 aceptado para planificación el 2026-10-15.
- **Diseño aprobado para planificar:** fixture JSON versionado en una fila de MySQL, inicializador one-shot explícito y adaptador DB seleccionado solo por Compose.
- **Versión:** 0.1.

## 1. Objetivo

Permitir que un desarrollador construya y ejecute localmente Talent Assistant como un stack Docker Compose con API, Web y MySQL, y que la API consulte una semilla determinista de respuestas HCM sintéticas persistidas en MySQL. El procedimiento local debe quedar documentado de principio a fin.

La semilla representa datos y respuestas del HCM para demostración; no crea un HCM, no convierte Talent Assistant en sistema de registro y no traslada reglas de negocio.

## 2. Alcance propuesto

### Incluido

- Un Dockerfile versionado para API, uno para Web y uno para MySQL.
- Compose para construir, iniciar, conectar y comprobar salud de los tres servicios.
- Persistencia en MySQL de los fixtures mínimos usados por la fuente HCM sintética.
- Una inicialización reproducible e idempotente que prepare el esquema requerido y cargue los datos sintéticos.
- Lectura de los fixtures por la API mediante `IHcmAttendanceSource`; Web continúa consumiendo la API con el cliente HTTP tipado.
- Puertos locales publicados solo en loopback y configuración local de credenciales fuera de Git y fuera de las imágenes.
- Actualización de `docs/development/local-setup.md` y de las referencias de ejecución local del README después de implementar y verificar.

### Fuera de alcance

- Despliegue productivo, CI/CD, autenticación, TLS o exposición a redes compartidas.
- Conectar un HCM real, datos reales, exportaciones corporativas o secretos productivos.
- Construir módulos HCM completos de empleados, TimeCheck, TimeOff o Tasks.
- Duplicar reglas de marcaje, aprobación, duración, calendarios o ausencias.
- Cambiar proveedor/versión de MySQL o EF Core, agregar dependencias, LLM o agentes.
- Modificar otras decisiones de SPEC-001 o ADR aceptados fuera del cambio local expresamente aprobado.

## 3. Requisitos y límites arquitectónicos

La fuente sintética persistirá solo la proyección/fixture necesario para responder el contrato de asistencia ya consumido por Application. `IHcmAttendanceSource` se mantiene como frontera; Application no dependerá de EF Core ni de Infrastructure.

### Diseño de persistencia recomendado

- Infrastructure añadirá una entidad técnica `SyntheticAttendanceDataset`, no una entidad HCM, con clave fija `DatasetId`, `PayloadVersion` y `PayloadJson` de texto largo.
- Cada fila contendrá la colección completa de respuestas `HcmAttendanceInsight` serializada con `System.Text.Json`. Para el volumen sintético actual (cinco fixtures), la API carga el documento y conserva en .NET el filtro de empleado y fecha local existente.
- El payload tendrá versión explícita. Si la aplicación no reconoce esa versión, la inicialización/fuente falla con un error controlado; no se reinterpretan datos con otro esquema.
- El seed reemplaza transaccionalmente la fila del dataset completo por `DatasetId` mediante upsert. Esto actualiza el snapshot entero sin filas huérfanas, duplicados, `TRUNCATE` ni borrado de otros datos.
- La lista canónica de fixtures se define una sola vez en Infrastructure y la reutilizan la fuente en memoria y el seed para evitar deriva.

El formato JSON opaco es deliberado para guardar respuestas sintéticas, no entidades/relaciones de un HCM. No permite filtros SQL por empleado/fecha; el filtro actual se aplica después de deserializar. Si el dataset deja de ser pequeño o aparecen consultas de negocio, ese formato deberá revisarse en otra especificación.

### Inicialización y perfiles

- El perfil existente ejecutado en el host conserva `SyntheticHcmAttendanceSource` en memoria por defecto.
- El perfil Compose configura `Attendance__Source=MySql` y registra un adaptador DB con ciclo de vida scoped, compatible con `TalentAssistantDbContext` scoped.
- Un servicio one-shot `db-init`, construido desde la misma imagen API, ejecuta un modo interno `--initialize-local-data`: aplica migraciones versionadas y upsert del dataset, y termina con éxito/error. API normal no aplica migraciones ni seed al arrancar.
- Compose inicia MySQL y espera su healthcheck; el operador ejecuta `db-init`; API y Web se inician después. Web consume API por el nombre de servicio interno.
- El inicializador puede repetirse sobre el volumen actual o uno nuevo; no exige `docker compose down --volumes`. `talentassistant_tests` permanece separada y sin seed demo.

Las decisiones de representación, versión de payload e inicializador son parte del diseño propuesto, no contratos implementados. Requieren aprobación antes de crear la migración o el modo de inicialización.

La propuesta amplía el alcance actual de ejecución local: ADR-0004 y SPEC-001 indican que solo MySQL se ejecuta en Docker. La extensión afecta solo el perfil de desarrollo local y requiere aprobar ADR-0006; este borrador no modifica dichos documentos aceptados.

## 4. Flujo local esperado

1. El desarrollador prepara la configuración local siguiendo la guía, sin guardar secretos en Git.
2. `docker compose up --build -d mysql` construye/inicia MySQL y espera disponibilidad saludable.
3. `docker compose run --rm db-init` aplica migraciones pendientes y reemplaza atómicamente el dataset sintético versionado.
4. `docker compose up --build -d api web` inicia API con `Attendance__Source=MySql` y Web usando `http://api:5101/` por la red interna.
5. El desarrollador verifica `/health`, `/health/database`, `/attendance/insights` y la página Web en los puertos locales.
6. `docker compose down` detiene y elimina contenedores/red conservando el volumen; reiniciar el flujo vuelve a aplicar el seed sin duplicar registros. La guía identifica por separado cualquier comando destructivo de eliminación del volumen.

Los puertos de host propuestos son los existentes (Web `127.0.0.1:5102`, API `127.0.0.1:5101`, MySQL `127.0.0.1:3306`). La API y Web se comunicarán internamente mediante los nombres de servicio Compose.

## 5. Componentes candidatos

| Componente | Responsabilidad propuesta | Línea base que debe conservar |
|---|---|---|
| `src/TalentAssistant.Api/Dockerfile` | Restaurar/publicar API en multi-stage y ejecutar con ASP.NET Core fijado. | SDK `10.0.402`, runtime `10.0.13`; secretos solo por entorno. |
| `src/TalentAssistant.Web/Dockerfile` | Restaurar/publicar Blazor Web en multi-stage. | Consume API, no DbContext ni Infrastructure. |
| `docker/mysql/Dockerfile` | Construir la imagen local DB desde la imagen MySQL oficial aprobada e incorporar solo los recursos de inicialización requeridos. | MySQL `8.4.11` y digest fijados por ADR-0004. |
| `compose.yaml` | Coordinar MySQL, `db-init`, API y Web, dependencias, healthchecks, red, volumen y puertos locales. | Secretos no versionados; `talentassistant_tests` sigue separada. |
| `db-init` | Reutilizar la imagen API con el modo interno `--initialize-local-data`; migrar y sembrar, luego salir. | No se ejecuta durante el arranque normal de API. |
| Modelo Infrastructure | Añadir una tabla técnica para un dataset JSON versionado, no tablas Employee/TimeCheck/TimeOff. | Application solo ve `IHcmAttendanceSource`; DB source scoped. |
| Fuentes HCM sintéticas | Conservar fuente en memoria para host/tests; añadir fuente MySQL para perfil Compose, ambas tras `IHcmAttendanceSource`. | Reutilizan el mismo catálogo de fixtures y el mismo filtro local de incidencia. |
| Guía local | Explicar configuración, build, seed, acceso, parada, persistencia y restablecimiento seguro. | Procedimientos probados en este checkout; no describir comandos no verificados. |

Las rutas son propuestas de diseño, no archivos implementados.

## 6. Semilla sintética propuesta

La semilla debe reutilizar los cinco escenarios sintéticos actuales de asistencia, incluyendo contexto de TimeOff aprobado, pendiente, rechazado, cancelado y nulo. Debe preservar tipo, estado, referencia, fechas, duración/unidad y evidencia suministrados por los fixtures, sin calcular semánticas nuevas ni inferir relaciones. El mismo catálogo alimenta la fuente en memoria y el payload persistido.

El proceso de inicialización debe:

- Usar un identificador fijo de dataset y `PayloadVersion` para reconocer el formato.
- Reemplazar de forma transaccional el payload completo del dataset en cada seed; al ser una fila por dataset, no quedan registros de incidencias que hayan sido retiradas del catálogo.
- Poder repetirse sin duplicados ni cambios en los resultados de regla/evidencia; no ejecutar borrados generales ni limpiar el volumen.
- Cargar datos solo en `talentassistant_dev`, nunca en `talentassistant_tests` por defecto.
- Funcionar tanto con un volumen recién creado como con el volumen de desarrollo existente, sin exigir borrar datos para volver a aplicar el seed.
- Documentar explícitamente el comando que conserva el volumen y el comando destructivo de restablecimiento.

El mecanismo recomendado es el modo interno `--initialize-local-data` del ejecutable API, invocado solo por `db-init`; no se propone SQL con JSON duplicado ni depender de `docker-entrypoint-initdb.d`, que no se ejecuta sobre un volumen ya inicializado.

## 7. Criterios de aceptación propuestos

| ID | Criterio verificable propuesto | Evidencia futura |
|---|---|---|
| AC-012 | Docker Compose construye API, Web y MySQL desde sus respectivos Dockerfiles y deja los servicios requeridos listos. Los endpoints API/Web son accesibles en loopback; la base tiene healthcheck. | `docker compose config --quiet`, build de imágenes, `up --build --wait`, healthchecks HTTP y MySQL. |
| AC-013 | La API lee desde MySQL los fixtures sintéticos; el seed reemplaza idempotentemente el dataset completo por clave/version, no duplica ni deja filas huérfanas al cambiar el catálogo, y conserva los resultados HCM. | Consultar `/attendance/insights` tras arranque limpio y tras seed repetido/catálogo actualizado; comparar IDs y valores esperados sin borrar el volumen. |
| AC-014 | La guía local documenta prerrequisitos, `.env`/User Secrets, construcción, inicio, URLs, health, seed, parada, persistencia y restablecimiento destructivo. | Revisión siguiendo la guía desde un checkout limpio, sin pasos implícitos. |
| AC-015 | Las imágenes no contienen secretos; `.env` permanece ignorado; puertos host quedan limitados a `127.0.0.1`; no se expone el stack como despliegue productivo. | Inspección de Compose/Dockerfiles y prueba de configuración; revisión de bindings/logs. |

AC-012..AC-015 son propuestas y no se declaran aceptadas hasta aprobación de SPEC-004 y ADR-0006.

## 8. Diseño técnico aprobado para planificación

El usuario aprobó SPEC-004 y ADR-0006 para pasar a la planificación de implementación. Se adoptan estas decisiones de diseño para preparar el plan; todavía no autorizan cambios de código:

1. Un único dataset JSON (`DatasetId`, `PayloadVersion`, `PayloadJson`) en una tabla técnica Infrastructure, en vez de normalizar módulos HCM.
2. `db-init` one-shot, usando la imagen API con `--initialize-local-data`, que aplica migraciones versionadas y upsert transaccional del dataset. API normal no migra ni siembra.
3. Perfil Docker Compose selecciona `MySqlHcmAttendanceSource`; ejecución host/test conserva el adaptador en memoria. Ambos implementan `IHcmAttendanceSource` y usan el mismo catálogo.
4. El filtro por empleado y fecha local se realiza después de deserializar el pequeño dataset; no se filtra por JSON en MySQL.
5. El seed conserva el volumen y nunca requiere reset destructivo para volver a aplicarse.
6. La ampliación local de ADR-0004/SPEC-001 se limita a servicios contenedorizados; no habilita producción.

La decisión de que la API del perfil Compose lea fixtures desde MySQL fue confirmada por el usuario. La representación y el flujo de seed quedan definidos para planificar. Crear tablas, migraciones, Dockerfiles, Compose o inicializador espera la aprobación del plan de trabajo.

## 9. Plan SDD propuesto

1. FR-020, NFR-017, AC-012..AC-015, SPEC-004 y ADR-0006 fueron aprobados para planificación el 2026-10-15.
2. El seguimiento ODD se creó en `odd/tasks/containerized-local-environment.md` con T1–T3 y espejo Engram; no contiene cambios de código.
3. Añadir Dockerfiles y el Compose local, conservando proveedor y versiones aprobadas.
4. Añadir migración de la tabla de dataset, catálogo único, inicializador one-shot y fuente MySQL scoped.
5. Actualizar la guía local y README con comandos realmente verificados.
6. Verificar build de imágenes, migración sobre volumen limpio y existente, seed repetido/actualizado, API/Web/DB health, persistencia y límites de red/secretos.
7. Cerrar con matriz requisito/criterio/evidencia y registrar pruebas ejecutadas, pendientes y límites.

## 10. Estado y aprobación

El usuario aprobó SPEC-004 y ADR-0006 para planificación el 2026-10-15. El plan ODD está en `odd/tasks/containerized-local-environment.md`. T1 está implementada con su work-unit commit `4b82fe2baa5adddcc90cb0b1e9ad133256ef218d`; T2 está implementada con `bcfec3a26452ee5729a0c804ab7a28f488c815c0`. La limitación de `MySqlPersistenceTests` permanece documentada; no se reejecutó xUnit. T3 aún no comienza. El perfil host y los contratos HCM actuales continúan vigentes.

## 11. Verificación de T1

- Build fijado: `dotnet build TalentAssistant.sln --configuration Debug --no-restore --verbosity minimal` con SDK `10.0.402`: PASS, 0 warnings/0 errors; compilaron UnitTests e IntegrationTests.
- Migración `20261016001733_AddSyntheticAttendanceDataset` generada y aplicada de forma aditiva en `talentassistant_dev`; la base solo tenía previamente el historial EF. El inicializador repetido no aplicó nuevas migraciones ni duplicó el dataset.
- Smoke API con `Attendance:Source=MySql`: `/health/database` 200; `/attendance/insights?from=2026-10-15&to=2026-10-18` 200 con cinco fixtures; estados y contexto nulo preservados; rango inválido 400.
- Verificador independiente: S2, S4 y S5 MET en una copia scratch con una instancia MySQL desechable; seed de una fila y segundo seed conservaron el mismo snapshot. El espacio de trabajo no fue modificado por el verificador.
- La prueba enfocada `SyntheticAttendanceDatasetSerializerTests` pasó 3/3. La suite completa pasó 5/5 UnitTests y 6/7 IntegrationTests; `MySqlPersistenceTests` falló antes de conectarse porque falta `ConnectionStrings__TalentAssistantTests`. No se reintentó ni se investigó ese bloqueo. Una prueba que sembraba `talentassistant_tests` se quitó para mantener esa base separada y sin seed demo.

El usuario aceptó continuar a T2 con el fallo de `MySqlPersistenceTests` documentado; no se reintentará xUnit ni se cambiará configuración de seguridad. T1 queda implementada con esa limitación aceptada y commit local registrado en el task log. La estrategia `feature-branch-chain` está elegida; T2 está en curso y T3 depende de T2. SPEC-004 no se declara completa.

## 12. Verificación de T2

- `docker compose config --quiet` pasó con variables sintéticas efímeras; no se creó `.env`.
- El Dockerfile MySQL construyó sin descarga porque su imagen oficial fijada por digest ya estaba en caché local.
- Los Dockerfiles MySQL/API/Web construyeron correctamente. El usuario autorizó pulls anónimos de las imágenes .NET fijadas desde MCR y restore de paquetes ya declarados desde NuGet; no se añadieron dependencias.
- `docker compose -p talentassistant-local up --build -d --wait` terminó con éxito; MySQL está healthy, `db-init` salió 0, API y Web están en marcha. Un segundo `db-init` fue no-op de migración y el API siguió devolviendo cinco fixtures.
- Smoke HTTP: API `/health` 200, `/health/database` 200, `/attendance/insights` 200 con cinco registros; Web `/` y `/attendance-insights` 200. En el navegador, el filtro `2026-10-15` a `2026-10-18` mostró los cinco casos y sus estados fuente.
- La primera comprobación Web detectó 404 en `/_framework/blazor.web.js`: el restore se ejecutaba antes de copiar los componentes Razor. Se añadió restore después de esa copia; rebuild, script HTTP 200 y conexión interactiva Blazor quedaron observados sin errores de consola.
- Verificación independiente: S1/S2/S4/S5 MET, sin bloqueos, en copia scratch con volumen temporal; seed repetido conservó una fila y payload idéntico. El stack principal permaneció intacto. La revalidación confirmó que `name: talentassistant-local` hace que `docker compose` sin `-p` resuelva y use el stack ya iniciado.
- `gentle-ai review mode status` informó OFF global; T2 queda `disabled/unmanaged`, sin review nativo ni receipt.
- No se modificaron políticas, certificados, `global.json` ni configuración de seguridad. xUnit no se reejecutó; se mantiene documentado el resultado parcial de T1. La guía local completa corresponde a T3.
