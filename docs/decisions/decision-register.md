# Registro de decisiones

Este documento resume las decisiones de producto y arquitectura que gobiernan Talent Assistant. Cada ADR es la fuente de referencia para el contexto, la justificación y las consecuencias de su decisión.

## Decisiones aceptadas

| ID | Decisión | Estado | Referencia |
|---|---|---|---|
| ADR-0001 | Utilizar un monolito modular para la solución inicial | Aceptado | `docs/architecture/adr/ADR-0001-modular-monolith.md` |
| ADR-0002 | Utilizar respuestas sintéticas del HCM en el MVP, sin replicar sus módulos | Aceptado | `docs/architecture/adr/ADR-0002-synthetic-data.md` |
| ADR-0003 | Mantener un límite arquitectónico para un posible servicio de IA futuro | Aceptado | `docs/architecture/adr/ADR-0003-ai-service-boundary.md` |
| ADR-0004 | Utilizar MySQL con Entity Framework Core | Sustituido (2026-10-21, por ADR-0013) | `docs/architecture/adr/ADR-0004-persistence.md` |
| ADR-0013 | Utilizar PostgreSQL como única base de datos relacional con Entity Framework Core; retira MySQL del stack | Aceptado; implementado y verificado según SPEC-013 (132/132 integración desde base vacía) | `docs/architecture/adr/ADR-0013-postgresql-persistence.md`; `specs/013-postgres-only-persistence/spec.md` |
| ADR-0005 | Utilizar CQRS ligero sin MediatR en la etapa inicial | Aceptado | `docs/architecture/adr/ADR-0005-lightweight-cqrs.md` |
| ADR-0006 | Añadir perfil local Compose para API/Web/MySQL con dataset sintético versionado y fuente MySQL detrás de `IHcmAttendanceSource` | Aceptado; perfil local implementado con limitaciones de verificación documentadas; la fuente de asistencia sobre la BD propia queda redefinida en PostgreSQL por ADR-0013 | `docs/architecture/adr/ADR-0006-containerized-local-development.md`; `specs/004-containerized-local-environment/spec.md` |
| ADR-0007 | Autenticación local con Identity y JWT | Aceptado; implementado con limitaciones documentadas | `docs/architecture/adr/ADR-0007-local-authentication.md`; `specs/005-local-authentication/spec.md` |
| ADR-0008 | Localización de la interfaz y perfil de usuario | Aceptado; implementado con limitaciones documentadas | `docs/architecture/adr/ADR-0008-ui-localization-and-user-profile.md`; `specs/006-user-profile-localization/spec.md` |
| ADR-0009 | Datos sintéticos del HCM bilingües servidos por el idioma del usuario | Aceptado; implementado con limitaciones documentadas | `docs/architecture/adr/ADR-0009-bilingual-hcm-data.md`; `specs/007-bilingual-hcm-data/spec.md` |
| ADR-0010 | Capacidades de consulta del asistente previas a la IA | Aceptado; implementado con limitaciones documentadas | `docs/architecture/adr/ADR-0010-assistant-query-capabilities.md`; `specs/008-assistant-query-capabilities/spec.md` |
| ADR-0011 | Asistente conversacional con proveedor Groq (API compatible OpenAI) | Aceptado; implementado con limitaciones documentadas; refuerzo de ámbito y anti-alucinación (SPEC-010) | `docs/architecture/adr/ADR-0011-groq-conversational-assistant.md`; `specs/009-conversational-assistant/spec.md` |
| ADR-0012 | Consumo directo de solo lectura del HCM (PostgreSQL) tras los puertos | Aceptado; implementado con limitaciones documentadas | `docs/architecture/adr/ADR-0012-read-only-hcm-consumption.md`; `specs/011-hcm-postgres-source/spec.md` |

## Línea base tecnológica confirmada

Las siguientes decisiones están aprobadas y deben guiar la implementación inicial:

- **Framework objetivo:** .NET 10 LTS (`net10.0`); SDK `10.0.402` y runtime `10.0.13` como versiones estables vigentes al 2026-10-14.
- **Frontend:** Blazor Web App.
- **API backend:** ASP.NET Core Web API.
- **Arquitectura:** Clean Architecture dentro de un monolito modular.
- **Persistencia:** PostgreSQL 17 como único motor relacional de la aplicación, con Entity Framework Core `10.0.13` y proveedor `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3` (licencia PostgreSQL, permisiva). La base propia se ejecuta como servicio Compose propio con bases `talentassistant_dev` y `talentassistant_tests`; el conector HCM sostiene su contenedor PostgreSQL de solo lectura (SPEC-011/012). La selección y sus límites están documentados en ADR-0013, que sustituye a ADR-0004.
- **Entorno local:** SPEC-001 conserva el flujo de fundación con API/Web en el host y la base de datos en Compose (PostgreSQL desde ADR-0013/SPEC-013). ADR-0006 añade un perfil Compose opcional para el stack local completo; ambos perfiles están documentados en `docs/development/local-setup.md`.
- **Pruebas automatizadas:** xUnit.
- **Patrón de aplicación:** Commands y Queries explícitos, sin MediatR en la primera versión.
- **Datos:** respuestas y contratos sintéticos del HCM para el MVP; el HCM sigue siendo el sistema fuente y Talent Assistant no recrea sus procesos.
- **IA:** no integrar un LLM ni un framework de agentes en la fundación técnica inicial.

## Aplazamientos vigentes

- **CI (NFR-009 y AC-009):** ya no está aplazada: la canalización se asignó e implementó según SPEC-014 (`.github/workflows/ci.yml`: compilación, suite completa contra PostgreSQL y barrido de secretos). La verificación del run en vivo queda pendiente del primer push, que es decisión del usuario.- **Autenticación y despliegue de producción:** quedan fuera de SPEC-001. La autenticación ya existe en el perfil local según ADR-0007 (SPEC-005: identidad local con ASP.NET Core Identity y JWT); el despliegue de producción sigue fuera de alcance y no se autoriza exposición a redes compartidas ni uso de datos reales.
- **CI (NFR-009 y AC-009):** ya no está aplazada: la canalización se asignó e implementó según SPEC-014 (`.github/workflows/ci.yml`: compilación, suite completa contra PostgreSQL y barrido de secretos). La verificación del run en vivo queda pendiente del primer push, que es decisión del usuario.
- **Distribución del proveedor de persistencia:** ya no aplica. La licencia del proveedor de EF Core para PostgreSQL es permisiva (`PostgreSQL`), y el paquete MySQL/Oracle quedó fuera del stack con ADR-0013; el registro conserva esta línea como historial.

## Reglas para modificar decisiones

1. No cambiar silenciosamente una decisión aceptada durante la implementación.
2. Toda propuesta de cambio debe explicar el problema, las alternativas, las consecuencias y el impacto de migración.
3. Los cambios arquitectónicos deben actualizar el ADR correspondiente y este registro.
4. Los cambios que afecten a los requisitos también deben actualizar la especificación y los criterios de aceptación correspondientes.
5. No comenzar una implementación cuando existan decisiones contradictorias o pendientes que afecten materialmente a la fundación técnica.

## Definición de estados

- **Propuesto:** en evaluación, todavía no aprobado.
- **Aceptado:** aprobado y aplicable a la implementación.
- **Sustituido:** reemplazado por una decisión posterior; se conserva el registro histórico.

## Mantenimiento

Actualizar este registro cada vez que se cree, acepte, sustituya o modifique sustancialmente un ADR. El estado registrado aquí debe coincidir con el estado del ADR correspondiente.
