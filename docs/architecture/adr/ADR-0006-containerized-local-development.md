# ADR-0006 — Entorno local contenedorizado

- **Estado:** Aceptado; perfil local implementado con evidencia y limitaciones registradas en SPEC-004 (2026-10-16). Nota de herencia (2026-10-21): el servidor MySQL del perfil local se sustituyó por PostgreSQL al migrar la persistencia de la fundación ([ADR-0013](ADR-0013-postgresql-persistence.md)/[SPEC-013](../../specs/013-postgres-only-persistence/spec.md)); las menciones históricas a MySQL que siguen describen la decisión en su momento y no el estado actual.
- **Fecha:** 2026-10-15.
- **Aprobación de arquitectura:** usuario, 2026-10-15. La implementación se autorizó mediante las tareas T1-T3 del plan ODD.
- **Especificación relacionada:** SPEC-004 — Entorno local contenedorizado y semilla sintética.

## Contexto

ADR-0004 fija MySQL 8.4.11 en Docker Compose, pero API y Web se ejecutan directamente en Windows. La aplicación todavía usa una fuente HCM sintética en memoria y MySQL no tiene entidades de negocio.

Se propone poder construir y ejecutar localmente API, Web y MySQL en Docker, cargar una semilla sintética y actualizar la guía de desarrollo. La API debe consultar esos fixtures mediante `IHcmAttendanceSource`; la aplicación no se convierte en HCM ni recrea sus módulos.

## Decisión aceptada para el perfil local

- Construir API, Web y MySQL con un Dockerfile por servicio y coordinarlos con Docker Compose.
- Mantener los pins aprobados: .NET SDK `10.0.402`, runtime `10.0.13`, MySQL `8.4.11` y su digest oficial de ADR-0004.
- Persistir un único dataset JSON versionado en una tabla técnica Infrastructure: clave estable `DatasetId`, versión de payload y JSON serializado de la colección completa. No crear tablas ni relaciones de módulos HCM.
- Mantener el catálogo de fixtures como única fuente; la fuente en memoria del perfil host/tests y el seed del perfil Compose consumen ese catálogo.
- En Compose, seleccionar un adaptador MySQL scoped que implementa `IHcmAttendanceSource`, carga el dataset pequeño, deserializa y aplica el mismo filtro local de empleado/fecha. No filtrar JSON desde MySQL.
- Ejecutar migración y seed mediante un servicio one-shot `db-init` que reutiliza la imagen API con `--initialize-local-data`; aplica migraciones y reemplaza transaccionalmente la fila del dataset por clave. No borra filas ajenas ni el volumen, y la API normal no migra/siembra al arrancar.
- Mantener la base de integración separada, los secretos fuera de imágenes/Git y las publicaciones de puertos limitadas a loopback.
- Limitar el cambio a desarrollo local; no autorizar autenticación, red compartida ni despliegue productivo.

La fila tendrá una versión de payload que el lector validará antes de deserializar. El upsert transaccional reemplaza el snapshot completo, por lo que retirar una incidencia del catálogo no deja una fila huérfana. Las migraciones seguirán versionadas y no se ejecutarán de forma implícita desde el arranque normal de API.

## Impacto en decisiones vigentes

Esta decisión añade un perfil local de Compose para SPEC-004; no cambia el hecho histórico de que SPEC-001 ejecutó API/Web en el host ni elimina esa forma de desarrollo. SPEC-002 conserva su fuente sintética en memoria para el perfil host/tests; el nuevo perfil Compose selecciona el adaptador MySQL a través de `IHcmAttendanceSource`. No se modifican reglas, contratos HCM ni módulos de negocio. El cambio se limita al perfil local adicional; producción sigue fuera de alcance.

ADR-0007 (2026-10-16) sustituye la restricción «sin autenticación» para el perfil local: el stack local pasa a exigir inicio de sesión.

ADR-0002 permite introducir datos de demostración junto con la especificación que define persistencia, siempre que sean fixtures/proyecciones mínimas, sintéticas e idempotentes. SPEC-004 define ese alcance para el perfil Compose.

## Consecuencias esperadas

### Positivas

- Un flujo local reproducible construye los tres servicios.
- La API consume los mismos datos sintéticos sembrados en la base local.
- La guía documenta el ciclo de vida del stack y del volumen.

### Compromisos

- Se requieren un esquema mínimo, migraciones y un proceso de seed.
- La representación JSON es compacta y evita recrear tablas HCM, pero las consultas de empleado/fecha se ejecutan después de deserializar; es adecuada solo mientras el dataset siga siendo pequeño.
- Cambios incompatibles del formato requieren incrementar la versión de payload y una migración/seed compatible.
- Docker pasa a ser requisito para ejecutar el stack completo.
- Se deben evitar operaciones de reset destructivas implícitas y proteger los secretos locales.

## Alternativas consideradas

- **Mantener API/Web en el host:** conserva la decisión actual, pero no satisface la nueva solicitud.
- **Poblar MySQL sin que API lea la semilla:** deja dos fuentes sintéticas distintas y no ofrece la demostración solicitada.
- **Crear módulos HCM completos:** se descarta por el límite de producto; solo se persistirá el fixture mínimo de lectura.

## Verificación de la decisión

La decisión se considerará implementada cuando los criterios AC-012..AC-015 de SPEC-004 tengan evidencia observada y el local runbook se haya ejecutado desde un entorno limpio. La aprobación de ADR-0006 autoriza planificar, no ejecutar código; la compilación de imágenes por sí sola no acredita que seed, API y Web funcionen juntos.
