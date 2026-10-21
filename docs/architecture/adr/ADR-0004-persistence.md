# ADR-0004: Persistencia con MySQL y Entity Framework Core

- **Estado:** Sustituido (2026-10-21) por [ADR-0013](ADR-0013-postgresql-persistence.md), que migra toda la persistencia propia a PostgreSQL y retira MySQL del stack. El contenido siguiente se conserva como registro histórico de la decisión original.
- **Fecha:** 2026-10-14
- **Responsables de la decisión:** Equipo de desarrollo de Talent Assistant

## Contexto

Talent Assistant necesita persistir los datos de la aplicación. El MVP inicial utilizará datos sintéticos y debe disponer de un entorno de desarrollo y pruebas reproducible.

La capa de persistencia debe permanecer aislada de las reglas de negocio y de los casos de uso de la aplicación. La solución debe permitir gestionar migraciones, ejecutar pruebas automatizadas y facilitar una futura transición hacia fuentes de datos reales sin acoplar el dominio a un proveedor de base de datos.

## Decisión

Se utilizará **MySQL como base de datos relacional y Entity Framework Core (EF Core) como ORM**.

Para la fundación se selecciona el proveedor de Oracle **`MySql.EntityFrameworkCore` 10.0.9**, estable, con EF Core 10 y destino `net10.0`. La referencia de EF Core se fija en **10.0.13**, la última versión estable publicada a la fecha de esta revisión; el paquete del proveedor declara una dependencia mínima `>= 10.0.9` y no establece un límite superior. El proveedor declara compatibilidad con MySQL Server 8.0 o posterior.

Para desarrollo y pruebas locales se fija **MySQL Community Server 8.4.11 LTS**, ejecutado con Docker Compose y la imagen oficial `mysql:8.4.11@sha256:24989179f58856db763f418010c1169fecc944e3ec1ea7e921572d83eb070f70`. La aplicación .NET se ejecutará directamente en el equipo de desarrollo. Esta versión del servidor no fija la versión para producción, que queda fuera de esta fundación.

| Componente | Versión fijada | Evidencia de compatibilidad |
|---|---:|---|
| Target de .NET | `net10.0` | El paquete Oracle incluye un grupo `net10.0`; NuGet lo declara compatible. |
| .NET SDK | `10.0.402` | Versión estable más reciente de .NET 10 publicada por Microsoft el 2026-10-13; incluye runtime .NET/ASP.NET Core `10.0.13`. |
| EF Core | `10.0.13` | EF Core 10 requiere SDK y runtime .NET 10. El proveedor Oracle declara `>= 10.0.9`. La combinación fijada restauró y compiló en la solución; la migración inicial técnica de la aplicación se aplicó contra MySQL 8.4.11. El modelo de negocio sigue vacío. |
| `dotnet-ef` y `Microsoft.EntityFrameworkCore.Design` | `10.0.13` | Se mantienen alineados con la versión EF Core utilizada. |
| `MySql.EntityFrameworkCore` | `10.0.9` | Última versión estable observada en NuGet el 2026-10-14; no se selecciona `10.0.0-preview` ni `10.0.0-rc`. |
| `MySql.Data` | `26.7.0` | Dependencia del proveedor Oracle `10.0.9`. |
| MySQL Server local | `8.4.11` | Serie LTS activa; Oracle publica cinco años de soporte Premier y tres años de soporte Extended para una serie LTS. La etiqueta exacta y su digest se verificaron en el registro oficial de imágenes Docker. |

Las dependencias transitivas relevantes del grupo `net10.0` de `MySql.Data` 26.7.0 son `BouncyCastle.Cryptography` 2.6.2, `Google.Protobuf` 3.35.0, `K4os.Compression.LZ4.Streams` 1.3.8, `System.Configuration.ConfigurationManager` 10.0.8, `System.Security.Permissions` 10.0.8 y `ZstdSharp.Port` 0.8.8. Se controlarán mediante restauración y revisión del grafo de dependencias; no se agregarán como referencias directas salvo que la aplicación las use.

Se establecen las siguientes reglas:

1. El acceso a la base de datos y las configuraciones de EF Core pertenecen a Infrastructure.
2. Las entidades de Domain no deben depender de EF Core, de las API específicas de MySQL ni de detalles de implementación de la persistencia.
3. Los casos de uso de Application no deben depender directamente de `DbContext` ni de tipos específicos del proveedor de base de datos.
4. Los cambios en el esquema de la base de datos deben gestionarse mediante migraciones de EF Core versionadas en Git.
5. Las cadenas de conexión y las credenciales deben suministrarse mediante configuración, variables de entorno o User Secrets, según corresponda. Los secretos no deben incluirse en el repositorio.
6. Las pruebas automatizadas no deben depender de una base de datos de producción.
7. Los datos iniciales deben ser sintéticos y su generación debe ser reproducible.

## Restricciones de implementación

- Mantener la selección Oracle `MySql.EntityFrameworkCore` 10.0.9 para la fundación. Sustituirla requiere evidencia de compatibilidad y actualizar formalmente este ADR y el registro.
- Verificar la restauración, una migración inicial y una actualización de esquema contra MySQL 8.4.11 antes de continuar con persistencia funcional. La compatibilidad declarada por NuGet no sustituye esta prueba de integración.
- Mantener la configuración específica del proveedor dentro de Infrastructure.
- Ejecutar MySQL local únicamente mediante el servicio de base de datos definido en `compose.yaml`; la API y Blazor se ejecutan en Windows durante esta fase.
- Documentar el procedimiento de configuración del entorno local.
- Utilizar migraciones para crear y evolucionar el esquema de la base de datos.
- Mantener explícita y repetible la inicialización de la base de datos y la carga de datos sintéticos.
- No introducir automáticamente una abstracción de repositorio para cada entidad. Incorporar abstracciones cuando aporten valor a los límites de la aplicación o a su capacidad de prueba.

## Comparación de proveedores y evidencia

La revisión se realizó el **2026-10-14**. “Declarado” se refiere a los metadatos o documentación del fabricante; no significa que las pruebas de integración de Talent Assistant ya se hayan ejecutado.

| Proveedor | Versión y compatibilidad publicada | Migraciones y uso | Mantenimiento, licencia y coste | Evaluación |
|---|---|---|---|---|
| Oracle `MySql.EntityFrameworkCore` | `10.0.9` estable. NuGet declara EF Core 8, 9 y 10, incluye `net10.0` y, para ese grupo, requiere EF Core `>= 10.0.9` y `MySql.Data >= 26.7.0`. La guía de Connector/NET declara MySQL Server 8.0 o posterior. | La guía documenta `DbContext`, `UseMySQL`, operaciones Code First y scaffolding; el ejemplo concreto usa el paquete 7.0.2 y Connector/NET 8.0.33, así que no valida el par seleccionado. La API del proveedor incluye extensiones de `MigrationBuilder`. La guía pública de compatibilidad sigue mostrando Connector/NET 9.4.0 con EF Core 10 “preview”, por lo que esa tabla está desactualizada respecto a NuGet 10.0.9. No hay una migración ejecutada aquí. | Connector/NET 26.7.0 publicó cambios en la línea EF Core 10 y correcciones de consultas el 2026-07-29. NuGet declara `GPL-2.0-only WITH Universal-FOSS-exception-1.0` y requiere aceptar la licencia; NuGet no muestra un precio separado del paquete. | **Recomendado** por la versión estable oficial y sus dependencias declaradas para EF Core 10/.NET 10. Mantener como gate de implementación una prueba real de migraciones y consultas contra 8.4.11. Revisar la licencia antes de distribuir el producto. |
| Pomelo `Pomelo.EntityFrameworkCore.MySql` | `9.0.0` estable (última publicación observada: 2025-08-17); su matriz publicada corresponde a EF Core 9.0.x. No se encontró una versión estable publicada para EF Core 10. | Soporta las funciones usuales del proveedor EF Core, pero no satisface la versión de EF aprobada sin bajar de EF Core 10. | Licencia MIT; sin coste de licencia del paquete. La versión estable publicada está rezagada respecto a EF Core 10. | **No seleccionado**: requeriría cambiar la línea base de EF Core o usar código no publicado. |
| Devart `Devart.Data.MySql.EFCore` | `10.2.223.10` estable (2026-10-08), target `net10.0`, EF Core 10; NuGet declara `Microsoft.EntityFrameworkCore.Relational >= 10.0.12 && < 11.0.0`. Documenta MySQL 8.0 o posterior. | Publica soporte EF Core y herramientas de migración; el detalle de una prueba específica con 8.4.11 no está acreditado en las fuentes consultadas. | La edición Developer se publica a USD 269.95 por licencia individual; las actualizaciones y el soporte incluidos cubren un año y la licencia de esa versión es perpetua. La edición Express gratuita no incluye Entity Framework. | Alternativa viable si la organización prefiere licencia comercial y soporte; no se elige para el MVP sin aprobación del coste. |

### Clasificación de la evidencia de compatibilidad

| Afirmación | Clasificación | Evidencia y límite |
|---|---|---|
| `MySql.EntityFrameworkCore` 10.0.9 es una versión estable con target `net10.0` y dependencia EF Core `>= 10.0.9`. | Declarada | Metadatos NuGet y paquete del propietario MySQL/Oracle. No demuestra por sí sola el comportamiento en Talent Assistant. |
| EF Core 10 requiere .NET 10; SDK/runtime `10.0.13` y SDK `10.0.402` son versiones estables vigentes. | Declarada | Documentación Microsoft y página oficial de descargas. |
| El proveedor configura `DbContext` y admite operaciones Code First/CRUD. | Documentada de forma general | Guía Oracle y ejemplo; el ejemplo concreto usa una versión anterior, no la combinación fijada. |
| MySQL Server 8.4.11 es compatible con el proveedor. | Ejecución observada; matriz del fabricante no exacta | Oracle declara MySQL 8.0 o posterior y el modelo MySQL sitúa 8.4 en la serie LTS. El probe real y la migración técnica de la aplicación se ejecutaron contra 8.4.11; no se halló una matriz Oracle que certifique específicamente esa revisión. |
| La combinación genera y aplica migraciones en MySQL 8.4.11. | Verificada para el gate y la migración técnica de aplicación | El probe aislado aplicó dos migraciones y comprobó escritura/lectura; la aplicación aplicó `InitialCreate` (charset e historial EF, sin tablas de negocio) y la repetición fue no-op. La migración del modelo IntegrationTests sigue pendiente. |
| La solución Talent Assistant restaura y compila con las versiones fijadas. | Verificada | `dotnet restore TalentAssistant.sln` y `dotnet build TalentAssistant.sln --configuration Debug --no-restore` finalizaron correctamente; el build reportó 0 advertencias y 0 errores. |
| La persistencia de negocio de Talent Assistant consulta y escribe con las versiones fijadas. | Pendiente por alcance | No hay entidades de negocio en SPEC-001. El probe aislado verificó escritura/lectura con el proveedor; no se atribuye esa evidencia a CRUD de la aplicación. |

### Evidencia observada en la implementación (2026-10-14)

- La restauración resolvió EF Core/Design `10.0.13`, `MySql.EntityFrameworkCore` `10.0.9` y `MySql.Data` `26.7.0`; la solución de siete proyectos compiló con SDK `10.0.402` y runtime `10.0.13`.
- El probe de compatibilidad aislado aplicó `InitialCreate` y `AddNotes`, repitió `database update` sin cambios y comprobó escritura/lectura en MySQL `8.4.11`: `Migrations=2; WriteRead=PASS`. Ese resultado no representa CRUD de entidades de negocio de la aplicación.
- En el repositorio, la migración de aplicación `InitialCreate` aplica charset `utf8mb4` e historial EF, sin tablas de negocio; se aplicó a `talentassistant_dev` y la repetición fue no-op. El `DbContext` de aplicación sigue sin entidades de negocio, según ADR-0002.
- Compose inició la imagen fijada por digest y la consulta de conexión confirmó MySQL `8.4.11` y la base separada `talentassistant_tests`. El modelo `PersistenceProbeContext` reside en IntegrationTests, pero su migración no se generó/aplicó y las pruebas xUnit no se ejecutaron por Application Control `FileLoadException 0x800711C7`.
- La comprobación de AC-006 con un centinela sintético devolvió HTTP 503 genérico en `/health/database`; el centinela y los stack traces estuvieron ausentes de la respuesta y de la salida capturada. No se guardó un secreto real ni un `.env` en el repositorio.

### Alcance de la evidencia y limitaciones

- La revisión documental original no instaló paquetes ni ejecutó compilaciones, pruebas, consultas o migraciones. La evidencia de implementación posterior se registra en la sección anterior; no convierte una prueba aislada en CRUD de negocio de la aplicación.
- El paquete Oracle 10.0.9 declara una dependencia mínima de EF Core 10.0.9, de modo que admite la resolución de EF Core 10.0.13; esto no es una demostración de ejecución de esa combinación en la aplicación.
- Oracle documenta servidor MySQL 8.0 o posterior y el modelo de releases de MySQL declara los conectores compatibles con las versiones de servidor soportadas. No se encontró una matriz de pruebas Oracle que nombre exactamente MySQL 8.4.11 para el proveedor EF Core 10.0.9.
- Oracle documenta operaciones Code First, pero sus ejemplos principales usan `EnsureCreated`. La migración técnica vacía de la aplicación y el probe aislado se ejecutaron; la migración del modelo de integración y sus operaciones siguen pendientes hasta que la política permita cargar IntegrationTests.
- La documentación de Oracle que aún limita EF Core 10 a “preview” contradice los metadatos y la publicación estable de NuGet 10.0.9. Se registra esta discrepancia y se conserva la prueba de integración del proyecto como condición de aceptación pendiente.
- La limitación publicada para el proveedor Oracle es que no soporta tablas `MEMORY` (Memory-Optimized Tables); el modelo de Talent Assistant no requiere esa funcionalidad.
- `MySql.EntityFrameworkCore` y `MySql.Data` declaran `GPL-2.0-only WITH Universal-FOSS-exception-1.0` y NuGet requiere aceptar la licencia. MySQL Community Server se ofrece bajo GPL y existe una opción comercial de Oracle. No se interpreta aquí el efecto legal de la excepción ni se determina si una distribución propietaria la satisface; revisar antes de distribuir el producto. NuGet no muestra un precio separado del proveedor Oracle.
- Devart publica una edición Developer individual por USD 269.95, con un año de actualizaciones/soporte y licencia perpetua para la versión adquirida; su edición Express gratuita no incluye Entity Framework Core. La elección Oracle evita adoptar ese coste de licencia, a cambio del pendiente de revisión legal anterior.

### Fuentes consultadas

- NuGet Oracle, versión y frameworks: <https://www.nuget.org/packages/MySql.EntityFrameworkCore/10.0.9> y <https://api.nuget.org/v3-flatcontainer/mysql.entityframeworkcore/10.0.9/mysql.entityframeworkcore.nuspec>.
- NuGet Oracle, versiones publicadas: <https://api.nuget.org/v3-flatcontainer/mysql.entityframeworkcore/index.json>.
- Oracle Connector/NET, soporte EF Core y requisito de MySQL 8.0+: <https://dev.mysql.com/doc/connector-net/en/connector-net-entityframework-core.html>; API de migraciones EF Core: <https://dev.mysql.com/doc/connector-net/en/connector-net-ref-efcore.html>.
- Oracle Connector/NET, ejemplo Code First y operaciones de lectura/escritura: <https://dev.mysql.com/doc/connector-net/en/connector-net-entityframework-core-example.html>.
- Oracle Connector/NET, release notes 26.7.0: <https://dev.mysql.com/doc/relnotes/connector-net/en/news-26-7-0.html>.
- EF Core 10, framework requerido: <https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew>.
- Microsoft EF Core 10.0.13 en NuGet: <https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/10.0.13>; herramientas `dotnet-ef` 10.0.13: <https://www.nuget.org/packages/dotnet-ef/10.0.13>.
- Descargas .NET 10 y SDK 10.0.402: <https://dotnet.microsoft.com/en-us/download/dotnet/10.0>.
- MySQL, modelo LTS/Innovation y soporte: <https://dev.mysql.com/doc/refman/8.4/en/mysql-releases.html>.
- MySQL 8.4 Reference Manual, documenta hasta la versión 8.4.11: <https://dev.mysql.com/doc/refman/8.4/en/>.
- MySQL Community, licencia y opción comercial: <https://dev.mysql.com/doc/refman/8.4/en/what-is-mysql.html> y <https://dev.mysql.com/doc/refman/8.4/en/introduction.html>.
- Etiqueta y digest oficial de Docker Hub: <https://hub.docker.com/v2/repositories/library/mysql/tags/8.4.11>.
- Pomelo, versiones, compatibilidad y licencia: <https://api.nuget.org/v3-flatcontainer/pomelo.entityframeworkcore.mysql/index.json> y <https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql>.
- Devart, paquete y dependencias: <https://www.nuget.org/packages/Devart.Data.MySql.EFCore/10.2.223.10>; ediciones y precio: <https://www.devart.com/dotconnect/mysql/editions.html>.

## Consecuencias

### Ventajas

- Separación clara entre las reglas de negocio y la persistencia.
- Evolución del esquema mediante cambios versionados.
- Entornos de desarrollo reproducibles.
- Base técnica adecuada para futuras integraciones y fuentes de datos reales.

### Compromisos

- Es necesario gestionar las migraciones y la configuración de la base de datos.
- El proveedor seleccionado de EF Core puede presentar limitaciones respecto a otros proveedores.
- Las pruebas de integración requieren una estrategia fiable para disponer de una base de datos de pruebas.

## Verificación

La decisión se considerará implementada cuando:

- La aplicación pueda conectarse a una instancia local de MySQL configurada para desarrollo.
- Las migraciones de EF Core permitan crear y actualizar el esquema.
- La aplicación pueda iniciarse sin credenciales codificadas directamente en el código fuente.
- Los datos sintéticos puedan cargarse de forma reproducible.
- Existan pruebas automatizadas para los comportamientos de persistencia que lo requieran, sin depender de servicios de producción.
- Los proyectos Domain y Application compilen sin depender de Infrastructure ni del proveedor de MySQL.

## Alternativas consideradas

- **Persistencia exclusivamente en memoria:** descartada como estrategia principal porque no permite validar el esquema de MySQL, las migraciones ni el comportamiento del proveedor.
- **SQL Server:** no seleccionado para este proyecto.
- **SQL directo sin ORM:** no seleccionado para la fundación inicial, ya que EF Core proporciona un flujo consistente para el acceso a datos y las migraciones.
