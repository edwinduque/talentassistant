# ADR-0013: Persistencia con PostgreSQL y Entity Framework Core

- **Estado:** Aceptado (2026-10-21)
- **Fecha:** 2026-10-21
- **Responsables de la decisión:** Equipo de desarrollo de Talent Assistant
- **Sustituye a:** ADR-0004 (Persistencia con MySQL y Entity Framework Core), que queda en estado Sustituido como registro histórico.

## Contexto

La persistencia propia de Talent Assistant (identidad + asistencia sintética) estaba definida por ADR-0004 sobre MySQL 8.4.11 con el proveedor Oracle `MySql.EntityFrameworkCore` 10.0.9. Desde SPEC-011/012 el conector HCM consume PostgreSQL de solo lectura a través de `Npgsql 10.0.3` (ya referenciado en Infrastructure), y el entorno de producción previsto para la aplicación utiliza exclusivamente PostgreSQL. Mantener dos motores relacionales en el stack (MySQL para la aplicación, PostgreSQL para el HCM) obliga a correr ambos en producción solo para sostener un módulo heredado, duplica mediciones de disponibilidad, procedimientos de respaldo y scripts operativos, y arrastra una pendiente de licencia (GPL-2.0-only WITH Universal-FOSS-exception-1.0 de `MySql.Data`/`MySql.EntityFrameworkCore`) que exige revisión legal antes de cualquier distribución.

El usuario informó la corrección de dirección el 2026-10-21: “solo vamos a trabajar con postgres no mysql”, y aprobó la migración completa (camino 1) tras ver la evidencia de que MySQL vive en el código, no solo en la documentación. Los datos propios son sintéticos y reproducibles, por lo que la migración no requiere mover datos del volumen local existente.

## Decisión

Se utilizará **PostgreSQL como única base de datos relacional y Entity Framework Core (EF Core) como ORM**, con el proveedor `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`.

| Componente | Versión fijada | Evidencia de compatibilidad |
|---|---:|---|
| Target de .NET | `net10.0` | El proveedor publica un grupo `net10.0` (nuspec, 2026-10-21). |
| .NET SDK | `10.0.402` | Sin cambios respecto a ADR-0004; versión estable vigente. |
| EF Core | `10.0.13` | El proveedor declara dependencia `[10.0.4, 11.0.0)` para EF Core y EF Core Relational; `10.0.13` la satisface (nuspec verificado). |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.3` | Última versión estable publicada para EF Core 10 a la fecha (index.json de NuGet verificado 2026-10-21); no se selecciona `11.0.0-preview./-rc.` ni `10.0.0-preview./10.0.0-rc.`. Licencia `PostgreSQL` (permisiva). |
| `Npgsql` | `10.0.3` | Ya referenciado en Infrastructure para las fuentes HCM PostgreSQL (SPEC-011); el proveedor EF lo fija como dependencia de ese mismo grupo. |
| PostgreSQL Server local | `17` (`postgres:17-alpine`) | Misma serie 17 ya usada por el contenedor HCM del stack local (SPEC-011/012). La base propia se monta en un servicio Compose propio con bases `talentassistant_dev` y `talentassistant_tests`. |

Se conservan las reglas de arquitectura de ADR-0004 adaptadas al proveedor:

1. El acceso a la base de datos y las configuraciones de EF Core pertenecen a Infrastructure.
2. Las entidades de Domain no dependen de EF Core, de las API específicas de PostgreSQL ni de detalles de implementación de la persistencia.
3. Los casos de uso de Application no dependen directamente de `DbContext` ni de tipos específicos del proveedor.
4. Los cambios de esquema se gestionan con migraciones de EF Core versionadas en Git, regeneradas para el nuevo proveedor.
5. Las cadenas de conexión y credenciales se suministran por configuración, variables de entorno o User Secrets; los secretos no se incluyen en el repositorio.
6. Las pruebas automatizadas no dependen de una base de datos de producción.
7. Los datos iniciales son sintéticos y su generación es reproducible.

## Restricciones de implementación

- No permanece ninguna referencia a paquetes MySQL en la solución (criterio observable: `grep MySql` en los `.csproj` con 0 hits).
- Las migraciones EF se regeneran desde cero para PostgreSQL; las anotaciones `MySQL:Charset` y `MySQL:ValueGenerationStrategy` desaparecen. El volumen MySQL local se descarta (datos sintéticos) y se reconstruye con Compose.
- El símbolo del proveedor (`UseNpgsql`) queda confinado a Infrastructure, junto al `NpgsqlDataSource` ya existente para el HCM.
- El valor de configuración `Attendance:Source` pasa a aceptar únicamente los valores sin herencia de MySQL; el valor antiguo se rechaza en arranque con mensaje claro. Sin consumidores fuera del repositorio.
- Ejecutar la base propia local únicamente mediante el servicio definido en `compose.yaml`; no compartir la instancia con el contenedor HCM (bases separadas, roles de menor privilegio).
- La suite de integración completa debe verificar la migración contra PostgreSQL antes de aceptar el cambio (gate de implementación, equivalente al de ADR-0004).

## Comparación de proveedores y evidencia

La revisión se realizó el **2026-10-21**.

| Proveedor | Versión y compatibilidad publicada | Mantenimiento, licencia y coste | Evaluación |
|---|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.3` estable (index.json verificado); nuspec declara EF Core `[10.0.4, 11.0.0)` para `net10.0` y `Npgsql 10.0.3`. | Proyecto npgsql con mantenimiento activo y rol principal en el ecosistema EF Core; licencia `PostgreSQL` (permisiva); sin coste. | **Seleccionado**: única opción que satisface EF Core 10 estable sin coste ni revisión legal de licencia; su motor también es el del conector HCM y del entorno de producción previsto. |
| Oracle `MySql.EntityFrameworkCore` (ADR-0004) | `10.0.9` estable, en uso hoy. | GPL-2.0-only WITH Universal-FOSS-exception-1.0 con pendiente de revisión legal para distribución. | **Sustituido**: exige mantener un segundo motor en el stack y arrastra el aplazamiento de licencia del registro de decisiones. |
| Devart `Devart.Data.MySql.EFCore` | `10.2.223.10` estable para EF Core 10. | Edición Developer USD 269.95/licencia individual; Express sin EF. | No seleccionado (igual que en ADR-0004). |

### Clasificación de la evidencia de compatibilidad

| Afirmación | Clasificación | Evidencia y límite |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` es estable, targets `net10.0` y declara EF Core `[10.0.4, 11.0.0)`. | Declarada | index.json y nuspec de NuGet verificados 2026-10-21. No demuestra por sí sola el comportamiento en Talent Assistant. |
| `Npgsql 10.0.3` ejecuta consultas en producción local del proyecto. | Ejecución observada | Las fuentes HCM PostgreSQL (SPEC-011/0012) ya corren con esa versión sobre `postgres:17-alpine` en Compose, con la suite en verde. Es ejecución contra el conector HCM, no aún contra los contextos propios. |
| Los contextos propios (`TalentAssistantDbContext`, `TalentAssistantIdentityDbContext`) funcionan con `UseNpgsql` y migraciones regeneradas. | Pendiente de verificación | Gate de implementación: la suite de integración completa en verde contra PostgreSQL (SPEC-013 §4) cierra esta afirmación como Verificada. |
| La licencia PostgreSQL es permisiva y elimina la pendiente de distribución de ADR-0004. | Declarada | nuspec del paquete (`license type="expression"` → `PostgreSQL`); no se interpretan otros efectos legales de distribución. |

## Evidencia observada en la decisión (2026-10-21)

- NuGet publica `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3` como última estable de la línea 10 (entre `10.0.0-rc.2` y `11.0.0-preview.1`).
- El nuspec 10.0.3 declara para `net10.0`: `Microsoft.EntityFrameworkCore [10.0.4, 11.0.0)`, `Microsoft.EntityFrameworkCore.Relational [10.0.4, 11.0.0)` y `Npgsql 10.0.3`; la combinación fijada del proyecto (EF Core 10.0.13) la satisface sin bajar versiones.
- El paquete declara licencia expresión `PostgreSQL` (permisiva, equivalente a BSD/MIT en efectos prácticos de uso interno y redistribución con aviso), lo que retira la condición de legalidad que ADR-0004 dejaba aplazada en el registro de decisiones.
- El código actual ya compila y ejecuta `Npgsql 10.0.3` (fuentes HCM), de modo que el ensamblado no introduce dependencias nativas nuevas al añadir el proveedor EF.

## Fuentes consultadas

- NuGet, índice de versiones: <https://api.nuget.org/v3-flatcontainer/npgsql.entityframeworkcore.postgresql/index.json>
- NuGet, nuspec 10.0.3 (licencia y dependencias declaradas): <https://api.nuget.org/v3-flatcontainer/npgsql.entityframeworkcore.postgresql/10.0.3/npgsql.entityframeworkcore.postgresql.nuspec>
- Código del proyecto: `Npgsql 10.0.3` en `src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj`; fuentes HCM PostgreSQL en `src/TalentAssistant.Infrastructure/HcmPostgres` (SPEC-011/012).

## Consecuencias

### Ventajas

- Un único motor relacional en desarrollo y en el entorno de producción previsto, alineado con el conector HCM existente.
- Licencia permisiva; se elimina el aplazamiento de distribución de ADR-0004.
- Menos superficie operativa: un esquema de respaldo, un control de salud y un solo conjunto de scripts.
- El cambio de proveedor queda documentado: los símbolos MySQL salen del código y la configuración de arranque rechaza valores antiguos.

### Compromisos

- La migración regenera el conjunto de migraciones EF: el volumen MySQL local se descarta; el equipo debe reconstruir su entorno con Compose.
- La suite integración completa debe correrse contra PostgreSQL como gate antes de aceptar el cambio (SPEC-013 §4).
- Los documentos operativos (`local-setup.md`, README) cambian de procedimiento: contraseñas/roles del init SQL y el reset del volumen difieren de MySQL. Son diferencias operativas menores, documentadas en la guía local.

## Verificación

La decisión se considerará implementada cuando:

- La solución compila con SDK `10.0.402` sin referencias a paquetes MySQL.
- Las migraciones regeneradas crean el esquema completo en PostgreSQL (`talentassistant_dev`, `talentassistant_tests`).
- `/health` y `/health/database` responden OK contra la base propia en Compose.
- El dataset sintético se carga de forma reproducible con `db-init`.
- La suite de integración completa está en verde contra PostgreSQL (única admisión: el fallo ambiental `0x800711C7` documentado).
- Domain y Application compilan sin depender de Infrastructure ni del proveedor.

## Alternativas consideradas

- **PostgreSQL de aquí en adelante con MySQL congelado como legado:** descartada; llega a producción con dos motores o con la deuda encima (descartada por el usuario, 2026-10-21).
- **PostgreSQL en el mismo contenedor HCM (bases separadas en un solo servicio):** descartada para no mezclar el ciclo de vida ni los privilegios del HCM con los de la aplicación; el HCM es de solo lectura y su init script es el contrato de vistas (SPEC-012).
- **Persistir en memoria:** no aplica; ADR-0004 ya la descartó y los mismos argumentos siguen vigentes.
