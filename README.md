# Talent Assistant

Módulo de asistencia inteligente para responsables de Recursos Humanos, integrado con un HCM existente y desarrollado mediante Specification-Driven Development (SDD).

## Visión

Talent Assistant consulta, relaciona e interpreta información de un HCM existente para ayudar a responsables de RR. HH. a comprender situaciones y revisar evidencia. El HCM conserva la propiedad de empleados, jornadas, TimeCheck, TimeOff, Tasks y sus reglas de negocio; Talent Assistant no lo reemplaza ni crea esos módulos.

El proyecto evolucionará desde casos de uso verificables con contratos y respuestas sintéticas del HCM. La IA podrá incorporarse más adelante, cuando exista un caso justificado y aprobado.

## Objetivos del proyecto

- Desarrollar una aplicación web completa con frontend y backend.
- Aplicar Clean Architecture y separación de responsabilidades.
- Utilizar especificaciones versionadas como base del desarrollo.
- Incorporar pruebas automatizadas y trazabilidad entre requisitos e implementación.
- Introducir LLM, RAG, herramientas y agentes de forma incremental.
- Evaluar experimentalmente el valor del patrón Supervisor frente a alternativas más sencillas.

## Alcance inicial

El MVP propuesto utilizará datos sintéticos que representan contratos y respuestas del HCM, sin conexión corporativa real ni datos reales. La primera vertical aprobada para implementación en SPEC-002 es:

- Consultar empleados, sus oficinas (país y ciudad), jornadas y marcajes mediante una fuente HCM sintética.
- Presentar una incidencia evaluada por esa fuente, la evidencia que la sustenta y la información que requiere revisión.
- Relacionar información de TimeOff cuando la fuente la indique como pertinente; no gestionar solicitudes.
- Mantener Tasks como información posible de integración para casos de uso posteriores.
- Exponer los casos de uso mediante la API y el cliente HTTP tipado existentes.
- Pruebas automatizadas y documentación de ejecución local.

## Tecnologías

**Línea base confirmada:** .NET 10 LTS, Blazor Web App, ASP.NET Core Web API, Clean Architecture en un monolito modular, MySQL con Entity Framework Core 10 y pruebas automatizadas con xUnit. La aplicación usará Commands y Queries explícitos, sin MediatR inicialmente. Los datos sintéticos representan respuestas del HCM; no convierten a Talent Assistant en un HCM.

La fundación fija .NET SDK `10.0.402`, EF Core `10.0.13`, el proveedor Oracle `MySql.EntityFrameworkCore` `10.0.9` y MySQL Server `8.4.11` para desarrollo local mediante Docker Compose. El detalle de compatibilidad, configuración y límites se mantiene en [SPEC-001](specs/001-project-foundation/spec.md) y [ADR-0004](docs/architecture/adr/ADR-0004-persistence.md).

## Ejecución local

El perfil local completo construye y ejecuta API, Web y MySQL con Docker Compose, y carga los fixtures sintéticos en `talentassistant_dev`. Desde SPEC-005 el perfil local exige iniciar sesión en Web y API con un usuario y una contraseña configurados en `.env`; el flujo de login, las variables y las limitaciones están documentados en la [guía de desarrollo local](docs/development/local-setup.md), junto con las comprobaciones HTTP, la persistencia del volumen y la alternativa host. La base de integración `talentassistant_tests` permanece separada y no recibe la semilla demo. El alcance aprobado está en [SPEC-004](specs/004-containerized-local-environment/spec.md) y [ADR-0006](docs/architecture/adr/ADR-0006-containerized-local-development.md), y la autenticación en [SPEC-005](specs/005-local-authentication/spec.md) y [ADR-0007](docs/architecture/adr/ADR-0007-local-authentication.md).

La canalización de CI sigue pendiente y se asignará a una especificación posterior (NFR-009 y AC-009); la estrategia de despliegue de producción queda fuera de la fundación técnica.

La incorporación de Python, Google ADK, MCP, n8n, RAG y agentes se evaluará en fases posteriores.

## Principios de ingeniería

1. Los requisitos preceden a la implementación.
2. Las decisiones arquitectónicas se documentan.
3. El dominio no depende de infraestructura ni de proveedores de IA.
4. La IA no accede directamente a la base de datos.
5. Los permisos se validan en el backend.
6. El MVP no utiliza datos reales de empleados.
7. Las funcionalidades deben cumplir criterios de aceptación verificables.
8. Las decisiones importantes requieren revisión humana.

## Metodología

El flujo de trabajo comprende:

1. Definición del problema.
2. Especificación de requisitos.
3. Diseño y decisiones arquitectónicas.
4. Planificación de tareas.
5. Implementación.
6. Verificación automatizada.
7. Revisión humana y actualización de la documentación.

## Estado actual

La fundación técnica de SPEC-001 está implementada; su cierre permanece pausado. La primera vertical aprobada de SPEC-002 está implementada: build y smoke HTTP pasan, y la suite xUnit completa obtiene 12/12 pruebas (5 unitarias y 7 de integración, incluida `MySqlPersistenceTests` contra `talentassistant_tests`). SPEC-001 no se declara completa.

De [SPEC-005](specs/005-local-authentication/spec.md) (autenticación local), las tareas T1 y T2 están implementadas y verificadas: 16/16 pruebas de integración y UnitTests con un único fallo ambiental de Application Control documentado en el registro ODD (no es defecto del código). T3 aporta la documentación de la especificación.
