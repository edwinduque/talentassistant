# Feature: consultas dinámicas de RR. HH./HCM
Objetivo: implementar SPEC-020 y ADR-0014 para responder preguntas dinámicas con consultas de solo lectura sobre datos HCM no sensibles.
Locator: `odd/tasks/flexible-hcm-query.md`.
Rama / entrega: `feat/flexible-hcm-query`; `ask-on-risk`; estrategia elegida: `feature-branch-chain` (2026-10-25); previsión inicial: ~800 líneas; estimación actual: ~3.143 líneas de cambios (additions + deletions), sin archivos generados.
Runner: `dotnet test tests/TalentAssistant.UnitTests/TalentAssistant.UnitTests.csproj --configuration Debug --no-restore`; `dotnet test tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --configuration Debug --no-restore`; `dotnet test TalentAssistant.sln --configuration Debug --no-restore`.

## Specs

### S1 — Consultas sin plantillas por pregunta

El usuario pidió: «Quiero que sea mas libre de responder, pero terminamos teniend unas plantillas predeterminadas y si se pregunta algo diferente no sabe responder» y «Ojo, no quiero que ahora cree una plantilla para resolver eso. quiero que el chatbot sea mas libre en obtener y consultar esos datos.»

**Alcance:** exponer una capacidad declarativa reutilizable que permita combinar consultas sobre los campos no sensibles disponibles en el contrato HCM, sin crear una herramienta o plantilla por redacción.

**Aceptación:** contar la compañía y resolver combinaciones no predefinidas con resultados provenientes de la fuente, vía sesión autorizada. Pruebas de contrato y conversación deterministas.

### S2 — Solo lectura y datos sensibles

El usuario confirmó: «No modificacion de datos, consultas dinamicas pero que no contengan datos sensibles como el salario o numero de cuenta. lo demas si».

**Alcance:** el plan no puede modificar datos. Salario, número de cuenta y exclusiones previas explícitas —incluido el año de nacimiento crudo— no se seleccionan, filtran, agrupan, ordenan, agregan ni infieren. Los otros campos no sensibles disponibles en las vistas HCM no requieren aprobación individual.

**Aceptación:** plan validado en Application; SQL generado únicamente en backend, identificadores de metadatos confiables, valores parametrizados, grants y transacción de solo lectura, límites de ejecución. Pruebas negativas demuestran rechazo y hash de datos sin cambios.

### S3 — Conteos y consultas de nombres

El usuario dio estos ejemplos literales: «si pregunto cual es la  proxima persona que cumple años: Probablemente lo sepa porque tiene una plantilla upcoming_birthday», «pero si le pregunto cuantos empleados tiene la compañia: No se pudo obtener una respuesta válida.» y «si mañana pregunto cuantos mauricios hay en la compañia lo pueda resolver o No se pudo obtener una respuesta válida tambien lo sepa».

**Alcance:** el conteo de empleados y el conteo de todas las coincidencias de “Mauricio” usan la consulta general; el cumpleaños existente continúa funcionando.

**Aceptación:** conteos coinciden con el resultado independiente de ambas fuentes; cero resultados se distingue de consulta inválida; la consulta de cumpleaños conserva su resultado existente.

### S4 — Guardarraíl de solicitudes ajenas

El usuario indicó: «que no se dañen los guarda rieles que ya se han creado como por ejemplo que pregunten: creame una funcion en python para retornar el numero mayor.»

**Alcance:** mantener rechazo de peticiones fuera de RR. HH./HCM y no invocar la herramienta dinámica para generar código.

**Aceptación:** prueba del endpoint `/chat` con modelo determinista verifica respuesta de rechazo y `toolsUsed` vacío.

### S5 — Fuentes y contratos

**Alcance:** ejecutar en fuentes sintética y PostgreSQL con la misma semántica; mantener usuario, idioma, fuente configurada, error controlado, protección de egreso, vistas HCM, reglas de dominio y operaciones especializadas como `upcoming_birthday`.

**Aceptación:** paridad de operaciones soportadas y regresión de preguntas existentes de SPEC-008/009/010. Campos ausentes se informan como no disponibles; no se inventan.

## Tasks

- T1 — S1–S5: implementar de extremo a extremo el plan declarativo, validación, ejecución en ambas fuentes, herramienta de chat, pruebas y documentación; ruta inline; completo; commit `f9592f4`; `Risk: item 2 (consulta dinámica, campos sensibles y solo lectura), item 3 (contratos Application y catálogo del asistente)`.

## Log

### L1 — Solicitud original (2026-10-25)

> Ayudame con un nuevo requerimiento. La idea inicial del proyecto es que una persona de recursos humanos tenga una herramienta tipo chatbot para consultar toda la información de empleados marcajes, ausencias, tareas, informacion de empleados. Inicialmente el proyecto se fue llevando a unas consultas predeterminadas pero eso no es lo que yo quiero. Quiero que sea mas libre de responder, pero terminamos teniend unas plantillas predeterminadas y si se pregunta algo diferente no sabe responder
>
> Te doy ejemplos:
> si pregunto cual es la  proxima persona que cumple años: Probablemente lo sepa porque tiene una plantilla upcoming_birthday
>
> pero si le pregunto cuantos empleados tiene la compañia: No se pudo obtener una respuesta válida.
>
> Ojo, no quiero que ahora cree una plantilla para resolver eso. quiero que el chatbot sea mas libre en obtener y consultar esos datos.
>
> si mañana pregunto cuantos mauricios hay en la compañia lo pueda resolver o No se pudo obtener una respuesta válida tambien lo sepa
>
> Por eso quiero cambiar la estrategia, si es el caso que pueda consultar de bd de una manera diferente o tener una herramienta que haga las consultas con las restricciones pues necesarias y que no se dañen los guarda rieles que ya se han creado como por ejemplo que pregunten: creame una funcion en python para retornar el numero mayor.
>
> Hagamos esto como una especificacion nueva

### L2 — Alcance funcional confirmado (2026-10-25)

> Manejemoslo de la siguiente manera, No modificacion de datos, consultas dinamicas pero que no contengan datos sensibles como el salario o numero de cuenta. lo demas si

### L3 — Aprobación de ADR-0014 (2026-10-25)

> procede
>
> Si

El usuario aprobó explícitamente ADR-0014 para continuar con la implementación. Se autoriza el plan declarativo validado por backend y la compilación de SQL dinámico parametrizado, con invariantes de solo lectura, autorización y exclusión de datos sensibles.

### L4 — Progreso y evidencia

- Exploración de código completada en `feat/flexible-hcm-query`; el mapa señaló dos puertos HCM, fuentes sintética/PostgreSQL y vistas de contrato.
- Baseline de código: `184758fc653e26c3758e5242b2a246e655f255b7`.
- `dotnet test tests/TalentAssistant.IntegrationTests/TalentAssistant.IntegrationTests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~ChatOrchestratorTests"`: 13/13 correctas en 89 ms; build mostró dos advertencias nullable preexistentes en `src/TalentAssistant.Api/Program.cs:362,397`.
- La falla conversacional reportada no se reprodujo con el modelo remoto; no se hizo una llamada externa. Se verificará el comportamiento nuevo con el modelo determinista y las pruebas de contrato.
- T1 queda como un solo work unit porque separar la capacidad entre fuentes y chat dejaría commits intermedios no utilizables.
- RED→GREEN observado: faltaba `query_hr_data` en catálogo; el validador rechazaba birthday fields, agregación ambigua y comparación escalar de arrays; el filtro «Juan Perez» no encontraba «Juan Pérez»; y los payloads grandes no tenían límite. Las pruebas correspondientes ahora pasan.
- Verificación enfocada: UnitTests `HcmDataQueryValidatorTests` 12/12; integración de Chat/catalog/sintético/PostgreSQL/paridad/seguridad/modo 72/72. `dotnet format whitespace ... --verify-no-changes --no-restore` y `git diff --check` pasan; sin whitespace al final.
- Los tests PostgreSQL usan únicamente los servicios locales sintéticos `hcm-postgres` y `app-postgres`; no se llamó a Groq ni a una base real. La suite completa y el verificador independiente siguen pendientes.
- RDD está desactivado por preferencia global; entrega de revisión será `disabled/unmanaged`. Ninguna tarea se marca completa hasta obtener la prueba pendiente y la verificación independiente.

### L5 — Corrección y verificación independiente (2026-10-25)

- El verificador ejecutó la suite completa en scratch: 195/196; el único bloqueo fue la aserción exacta de catálogo de `OpenAiCompatibleLanguageModelTests`, que aún esperaba seis herramientas. No se detectó una falla de implementación.
- Un lote de corrección actualizó la lista esperada con `query_hr_data` y añadió `ChatOutOfScopeGuardrailTests` para el rechazo de la solicitud literal de Python por `POST /chat` con un modelo determinista, comprobando `toolsUsed` y `sources` vacíos.
- Recheck limitado a los dos bloqueos: `OpenAiCompatibleLanguageModelTests` 8/8 y `ChatOutOfScopeGuardrailTests` 1/1 (9/9 total). No se repitió la suite completa después del lote, conforme al límite de recheck.
- El verificador clasificó como advisory la falta de pruebas de frontera exacta para todos los máximos (12 campos, 8 filtros, 3 grupos, 250 filas); los límites están validados en el código y hay pruebas negativas de valores/limit.

### L6 — Estimación de entrega (2026-10-25)

- El conteo observado de cambios propios es aproximadamente 3.143 líneas (additions + deletions; archivos generados excluidos), por encima de la heurística de 400. La solución se conserva como un work unit coherente para no dejar commits intermedios incompletos. El usuario eligió `feature-branch-chain`; no se abrirá PR ni se hará push sin autorización adicional.

### L7 — Revisión de seguridad (2026-10-25)

- Revisión read-only del diff: no se confirmaron vulnerabilidades en el compilador, whitelist de campos, autenticación, consultas parametrizadas o permisos de solo lectura.
- Riesgos potenciales pendientes de validación live: el rechazo de peticiones ajenas depende del cumplimiento del prompt; y la consulta genérica aumenta los campos no sensibles que podrían salir a Groq. No se hizo una llamada live a Groq ni se usaron datos reales.
- La aprobación de egreso permanece limitada a la PoC; el uso productivo con datos reales debe reabrir ADR-0011/0012 conforme a SPEC-011.

### L8 — Estrategia de entrega (2026-10-25)

> feature-branch-chain (Recommended)

El usuario eligió la cadena de ramas de feature. Se autoriza el work-unit commit local; no se crearán PRs, tracker ni se hará push sin autorización separada. La división en PR slices queda pendiente de esa autorización.

### L9 — Cierre de T1 (2026-10-25)

- Commit del work unit: `f9592f4` (`feat(assistant): add validated dynamic HCM queries`), en `feat/flexible-hcm-query`.
- El commit contiene 3.014 adiciones y 129 eliminaciones (3.143 líneas de cambio). La implementación, documentación y pruebas forman una sola capacidad de extremo a extremo; no se forzó una división funcional solo para encajar la heurística de 400 líneas.
- Suite completa del verificador: 195/196 antes de corregir una aserción obsoleta del catálogo; tras la corrección y el test HTTP de S4, el recheck acotado pasó 9/9. No se volvió a ejecutar una suite completa tras la corrección por el límite de recheck.
- Revisión de seguridad read-only: sin vulnerabilidades confirmadas; queda el riesgo prompt-only y la política de egreso de datos reales descritos en L7. RDD permanece `disabled/unmanaged`; no hubo PR, push ni llamadas a Groq/base real.
