# Seguimiento de diseño: integración HCM y primera vertical

Objetivo: alinear la documentación del producto con HCM como sistema fuente y entregar la vertical aprobada de explicación de incidencias de marcaje.
Rama: `feat/hcm-attendance-insight`; entrega: `feature-branch-chain`; sin PR/merge; runner: `dotnet build` y smoke HTTP; xUnit no se reintentará sin nueva autorización.

## Specs

### S1 — HCM como sistema fuente

Alcance: empleados, jornadas, reglas de negocio, marcajes, vacaciones y tareas son propiedad del HCM; Talent Assistant consulta, relaciona e interpreta esos datos y presenta asistencia al responsable de RR. HH. No construir módulos HCM completos.

Texto exacto del usuario: “Talent Assistant es un módulo de asistencia inteligente que se integra con un HCM existente. No es un HCM nuevo ni pretende reemplazarlo.” También: “HCM como sistema fuente: empleados, jornadas, reglas de negocio, marcajes, vacaciones y tareas pertenecen al HCM existente.”

Aceptación: visión, alcance, requisitos, arquitectura y criterios separan explícitamente al HCM como fuente de negocio de Talent Assistant como capa de asistencia; documentación coherente y lectura estructural.

### S2 — Integración sintética sustituible y sin duplicación

Alcance: el MVP simula contratos/respuestas HCM con datos sintéticos; los casos de uso dependen de abstracciones de integración sustituibles. No duplicar el motor de reglas del HCM ni introducir LLM, agentes o Python.

Texto exacto del usuario: “Datos sintéticos en el MVP: simular las respuestas y los contratos de integración del HCM para desarrollar y validar el asistente sin depender inicialmente de una conexión real.” También: “Integración sustituible: diseñar abstracciones que permitan reemplazar la fuente sintética por una integración real con el HCM sin reescribir los casos de uso.”

Aceptación: la propuesta documenta contratos, fuente sintética y frontera de sustitución; no altera decisiones técnicas aprobadas ni SPEC-001.

### S3 — Reglas de marcaje propiedad del HCM

Alcance: documentar los conceptos de reglas proporcionados por el usuario sin fijar semánticas no aprobadas. Talent Assistant presenta o explica resultados del HCM; no implementa un segundo motor.

Texto exacto del usuario: “Las reglas discutidas —margen de gracia de 0 a 30 minutos por jornada, ventana de almuerzo, aprobación de marcajes manuales y notificaciones de errores a las 09:00 del siguiente día hábil— deben documentarse como reglas que el HCM aplica o expone.”

Aceptación: cada regla figura como propiedad del HCM y las condiciones, valores, zona horaria, estados y calendario no definidos quedan pendientes de aprobación.

### S4 — Primera vertical aprobada para implementación limitada

Alcance: implementar solo la consulta de empleados, su asociación con oficinas (país, ciudad y zona horaria IANA), jornadas y marcajes desde fuente HCM sintética; presentar una incidencia evaluada por esa fuente, relacionar TimeOff pertinente y mostrar evidencia y datos que requieren revisión. El rango `yyyy-MM-dd` usa `from` inclusivo y `to` exclusivo en la zona horaria de cada oficina y exige `from < to`. Tasks queda diferido.

Texto exacto del usuario en la solicitud original: “Este caso de uso es una propuesta para revisar, no una autorización para implementarlo.” La autorización posterior limita esta propuesta a la vertical aprobada, sin ampliar su alcance. La oficina asociada debe aportar país y ciudad.

Aceptación: SPEC-002 define la vertical autorizada; implementación solo dentro de su alcance. Las oficinas sintéticas muestran país, ciudad y zona horaria IANA; el rango respeta `from` inclusivo y `to` exclusivo por oficina y rechaza `from >= to`. Las decisiones de negocio pendientes permanecen sin fijar.

### S5 — Preservar la fundación y los límites aprobados

Alcance: SPEC-001 y su fundación no se modifican. Se permite únicamente el código necesario para SPEC-002; no se cambia infraestructura de entorno, dependencias, configuración de seguridad, políticas de Windows ni runner. No conectar un HCM real. El usuario autorizó un commit inicial y push al destino especificado, excluyendo `.atl/`, mediante la sesión GitHub CLI autorizada para esta publicación.

Texto exacto del usuario: “No reabras SPEC-001 ni modifiques la fundación técnica ya validada.” También: “No implementes nada hasta que yo revise y apruebe la propuesta.” La autorización posterior de commit/push solo aplica al primer envío explícito y no permite usar credenciales ambientales sin autorización.

Aceptación: cambios de aplicación y documentación limitados a SPEC-002; SPEC-001, la seguridad del equipo y las decisiones técnicas aprobadas permanecen intactas.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
|---|---|---|---|---|
| T1 | S1-S5 | Inline | Completada: documentación HCM-first y SPEC-002 revisable | `b37c7c5aa6e2270ad354cab91006b4d1a36ab47f` (snapshot inicial) |
| T2 | S1-S5 | Inline | Needs your decision: implementación y smoke HTTP completados; xUnit sin ejecutar | `b37c7c5aa6e2270ad354cab91006b4d1a36ab47f` (bootstrap inicial) |

## Log

### L1 — Solicitud original (2026-10-15)

# Corrección del enfoque funcional — Talent Assistant

## Contexto fundamental

Talent Assistant es un **módulo de asistencia inteligente que se integra con un HCM existente**. No es un HCM nuevo ni pretende reemplazarlo.

El HCM existente ya gestiona los procesos de negocio, como empleados, jornadas, TimeCheck, TimeOff y Tasks. Talent Assistant debe consumir y relacionar esa información para ayudar a los responsables de RR. HH. a consultar datos, detectar incidencias, comprender situaciones y obtener análisis útiles.

## Objetivo de esta tarea

Revisar y corregir la propuesta funcional de Talent Assistant para reflejar esta arquitectura de producto, reutilizando la fundación técnica ya creada.

**Esta tarea es exclusivamente de diseño y documentación. No implementes funcionalidades todavía.**

## Decisiones de diseño

1. **HCM como sistema fuente:** empleados, jornadas, reglas de negocio, marcajes, vacaciones y tareas pertenecen al HCM existente.
2. **Talent Assistant como capa inteligente:** su responsabilidad es consultar, relacionar e interpretar la información disponible y presentar resultados útiles al responsable de RR. HH.
3. **Datos sintéticos en el MVP:** simular las respuestas y los contratos de integración del HCM para desarrollar y validar el asistente sin depender inicialmente de una conexión real.
4. **Sin duplicación de negocio:** no construir módulos completos de empleados, TimeCheck, TimeOff ni Tasks. Modelar únicamente los datos, contratos e interfaces necesarios para los casos de uso del asistente.
5. **Reglas de negocio:** las reglas de marcaje pertenecen al HCM. El asistente debe consumirlas o recibir sus resultados a través de la integración simulada. No crear un segundo motor de reglas independiente sin una necesidad justificada.
6. **Integración sustituible:** diseñar abstracciones que permitan reemplazar la fuente sintética por una integración real con el HCM sin reescribir los casos de uso.
7. **IA progresiva:** mantener la fundación técnica aprobada. No introducir todavía un LLM, framework de agentes ni servicio Python. La arquitectura debe permitir incorporarlos posteriormente cuando exista un caso de uso justificado.

## Revisión de las reglas de TimeCheck

Las reglas discutidas —margen de gracia de 0 a 30 minutos por jornada, ventana de almuerzo, aprobación de marcajes manuales y notificaciones de errores a las 09:00 del siguiente día hábil— deben documentarse como reglas que el HCM aplica o expone.

No implementes un módulo de fichaje ni reproduzcas toda su lógica. Define cómo Talent Assistant obtendría la información necesaria para explicar una incidencia y qué información necesitaría de la integración simulada.

Conserva las decisiones pendientes identificadas; no inventes comportamientos definitivos.

## Primer caso de uso vertical propuesto

Propón un caso de uso pequeño y verificable:

- Consultar un conjunto de empleados, sus jornadas y sus marcajes desde una fuente sintética que representa al HCM.
- Identificar una incidencia a partir de información y reglas suministradas por esa fuente.
- Relacionar la incidencia con información de TimeOff cuando sea pertinente.
- Presentar al responsable de RR. HH. qué ocurre, qué datos sustentan la conclusión y qué información adicional necesita revisar.

Tasks puede quedar representado en el modelo de integración y reservarse para un caso de uso posterior. No es necesario implementar los tres dominios de una vez.

Este caso de uso es una propuesta para revisar, no una autorización para implementarlo.

## Documentación que debes revisar

Examina los documentos existentes y actualiza únicamente los necesarios, manteniendo coherencia entre:

- Visión y alcance del producto.
- Requisitos funcionales y no funcionales.
- Propuesta de arquitectura.
- ADR relacionados, solo si el enfoque actual contradice una decisión aprobada.
- Criterios de aceptación del MVP.
- Especificación SDD correspondiente al primer caso de uso, si procede.

No reabras SPEC-001 ni modifiques la fundación técnica ya validada.

## Entregables

1. Resumen de las correcciones realizadas.
2. Documentos modificados y motivo de cada cambio.
3. Frontera explícita entre responsabilidades del HCM y Talent Assistant.
4. Contratos e interfaces de integración que necesitaría el primer caso de uso.
5. Criterios de aceptación verificables para el caso de uso propuesto.
6. Decisiones pendientes que requieran mi aprobación.
7. Riesgos o contradicciones detectados en la documentación actual.

## Restricciones

- No escribas código de aplicación.
- No crees módulos funcionales completos del HCM.
- No introduzcas nuevas dependencias.
- No cambies las decisiones técnicas aprobadas sin justificarlo y solicitar revisión.
- No modifiques políticas de Windows ni investigues nuevamente el bloqueo de xUnit.
- No ejecutes cambios de infraestructura.
- No hagas commits ni push.
- No implementes nada hasta que yo revise y apruebe la propuesta.

Al terminar, presenta los resultados para revisión y espera mi aprobación.

### L2 — Corrección HCM-first y propuesta SPEC-002 (2026-10-15)

Se alineó la visión, el alcance y el README con Talent Assistant como capa de asistencia y el HCM como sistema fuente. Los requisitos generales siguen en estado Propuesto; FR-002/003 describen la asistencia y el contexto de TimeOff, FR-004 queda para casos posteriores, FR-008 asigna las reglas al HCM y FR-018/019 expresan sustitución de fuente y trazabilidad. NFR-002/005 y AC-002..008 quedaron coherentes con esa frontera.

Se aclaró ADR-0002 sin cambiar la decisión aceptada de usar datos sintéticos: estos representan contratos/respuestas del HCM, no funcionalidades de un HCM paralelo. El registro de decisiones mantiene ADR-0002 Aceptado y actualizado. La propuesta de arquitectura documenta el puerto conceptual `IHcmAttendanceSource`, la fuente sintética en Infrastructure y la sustitución futura; no se eligieron transporte, proveedor ni almacenamiento.

Se creó `specs/002-attendance-insight/spec.md` como propuesta no aprobada. La primera vertical presenta una incidencia evaluada por el HCM, evidencia/procedencia, información para revisar y TimeOff cuando la fuente lo relacione; Tasks queda diferido. Las reglas comunicadas (gracia de 0–30 minutos, almuerzo, aprobación de marcajes manuales y notificación a las 09:00 del siguiente día hábil) se atribuyen al HCM y permanecen semánticamente pendientes. No se encontró una fuente documental previa para esas reglas.

Documentos actualizados: `README.md`, `docs/product/vision.md`, `docs/product/scope.md`, `docs/requirements/functional-requirements.md`, `docs/requirements/non-functional-requirements.md`, `docs/architecture/architecture-proposal.md`, `docs/architecture/adr/ADR-0002-synthetic-data.md`, `docs/decisions/decision-register.md` y `docs/acceptance/mvp-acceptance-criteria.md`; nuevo `specs/002-attendance-insight/spec.md`. No se modificó SPEC-001 ni su logbook.

Pendiente de aprobación: SPEC-002 y sus criterios; campos/códigos del contrato; si el HCM entrega siempre el resultado de reglas; límites y zona horaria; relación de TimeOff; almacenamiento de la fuente sintética; proveedor/protocolo/autenticación de una integración futura. No se escribió código, no se ejecutaron pruebas ni cambios de infraestructura, no se añadieron dependencias y no hubo commits o push.

### L3 — Aclaración del objetivo del producto (2026-10-15)

Texto exacto del usuario:

recuerda que el fin de este proyecto no es un hcm sino un assistant de personas de rrhh de un hcm existente. como no puedo integrarlo con el hcm real los datos son sinteticos y si tenemos que crear scripts dummys con muchos datos lo podemos hacer, hay mas reglas de timeoff que podemos ver en su momento. te doy la aprobación que necesites

### L4 — Aprobación de SPEC-002 (2026-10-15)

Texto exacto del usuario: “Si”.

El usuario confirmó la aprobación de SPEC-002 tal como está y autorizó implementar únicamente la primera vertical. Aclaró que el HCM existente sigue siendo el sistema fuente, que los datos son sintéticos, que se permiten scripts de datos dummy si hacen falta y que las reglas adicionales de TimeOff pueden tratarse después. T2 queda autorizado dentro de ese límite. No se autoriza conectar con un HCM real, crear un HCM paralelo, codificar reglas de marcaje independientes, añadir IA, modificar seguridad o infraestructura de entorno, ni hacer commits o push.

### L5 — Oficinas y ubicación en el contexto sintético (2026-10-15)

Texto exacto del usuario: “la compañia tiene oficinas a las cuales tambien pertenecen los empleados. entonces indica pais y ciudad por oficina”.

Se añade a SPEC-002 que cada oficina sintética exponga país y ciudad y que los datos del empleado incluyan su asociación con la(s) oficina(s) indicada(s) por la fuente. No se fija cardinalidad ni taxonomía canónica; T2 permanece en curso y SPEC-002-AC-009 verifica el dato requerido.

### L6 — Semántica de filtros temporales por oficina (2026-10-15)

Texto exacto del usuario: “si”.

La respuesta aprobó `from` inclusivo y `to` exclusivo, ambos `yyyy-MM-dd`, en la zona horaria IANA suministrada por cada oficina. La zona no se inferirá desde país o ciudad y se rechazará `from >= to`. T2 sigue en curso hasta implementar y verificar el comportamiento.

### L7 — Fixture sintético en memoria (2026-10-15)

Decisión de implementación: `SyntheticHcmAttendanceSource` usa un fixture determinista en memoria dentro de Infrastructure, sin tablas HCM ni migraciones. Esto mantiene la frontera sustituible y evita crear un sistema de registro. Los scripts generadores se reconsiderarán solo si el dataset crece.

### L8 — Implementación y verificación parcial de T2 (2026-10-15)

Se implementó el puerto Application `IHcmAttendanceSource`, el caso de uso `GetAttendanceInsights`, el adaptador sintético en memoria, `GET /attendance/insights`, el cliente HTTP tipado y la página Blazor `/attendance-insights`. La fuente devuelve incidencias evaluadas por el HCM sintético, oficinas con país/ciudad/zona IANA, evidencia y contexto TimeOff; no calcula reglas locales ni persiste datos HCM.

RED no observado: las pruebas xUnit no se ejecutaron por la restricción conocida y la instrucción previa de no reintentar/investigar el bloqueo. Se añadieron pruebas de Application y del adaptador; `dotnet build` compiló los proyectos de pruebas.

Comprobaciones observadas:

- SDK `10.0.402`: `dotnet build TalentAssistant.sln --configuration Debug --no-restore --verbosity minimal` — PASS, 0 warnings y 0 errors.
- API `GET /health` — HTTP 200; `GET /attendance/insights?from=2026-10-15&to=2026-10-16` — HTTP 200 con dos registros; `from=to` — HTTP 400.
- Web `/` y `/attendance-insights` — HTTP 200. Playwright llenó el rango y confirmó dos resultados con oficina, evidencia y TimeOff; el rango inválido mostró error controlado. La consola del smoke activo (antes de cerrar los procesos) no mostró warnings ni errores.
- El primer arnés completó las solicitudes API y falló al asignar la respuesta Web a `$home`, variable reservada de PowerShell; el arnés corregido pasó las comprobaciones Web. Los procesos/logs temporales propios se limpiaron y los puertos 5101/5102 quedaron libres.
- No se ejecutó `dotnet test`, migración, acceso MySQL ni conexión HCM real. No hubo cambios de seguridad/entorno, dependencias nuevas, commits o push.

Risk: none

Needs your decision: la implementación está disponible, pero la verificación automatizada xUnit permanece pendiente. No se reintentará ni se cambiará la política/runner sin nueva instrucción explícita. SPEC-002 no se declara completamente aceptada hasta contar con esa prueba.

### L9 — Autorización de commit inicial y destino (2026-10-15)

Texto exacto del usuario:

podemos ir subiendo cambios al repositorio

si

`https://github.com/edwinduque/talentassistant`

El usuario autorizó crear un commit inicial del proyecto y subirlo a ese destino. Confirmó excluir `.atl/` porque el índice generado contiene rutas absolutas del perfil local. En ese momento el repositorio no tenía commits y los archivos estaban sin seguimiento.

### L10 — Preparación local de publicación (2026-10-15)

La rama local `main` no tenía commits y `origin/main` no estaba disponible. El inventario inicial excluyendo `.atl/` estimó 74 archivos de texto y 3,851 líneas; el estado superaba el umbral aproximado de 400 líneas de ask-on-risk. La búsqueda local encontró solo placeholders vacíos y el centinela sintético del script, no credenciales reales.

### L11 — Estrategia de publicación inicial (2026-10-15)

Texto exacto del usuario: “feature-branch-chain (recomendado)”.

Se creó la rama `feat/hcm-attendance-insight`. El snapshot inicial elegible excluyendo `.atl/` contenía 78 archivos y 5,001 líneas insertadas; `git diff --cached --check` pasó. No se autorizó PR ni merge.

### L12 — Autorización de sesión para push (2026-10-15)

Texto exacto del usuario: “Sesión GitHub CLI”.

`gh auth status --active --hostname github.com` confirmó la sesión activa `edwinduque`. Se autorizó usar únicamente esa sesión y el helper de Git configurado solo para este comando y `github.com`.

### L13 — Commit inicial y push (2026-10-15)

- Commit raíz `b37c7c5aa6e2270ad354cab91006b4d1a36ab47f`: `feat: bootstrap Talent Assistant with HCM attendance insights` (78 archivos, 5,001 inserciones, `.atl/` excluido).
- `a1ab7e0 docs: record initial bootstrap commit`.
- `099aa7f docs: record HCM design work-unit commit`.
- `e06fb94 docs: record authorized GitHub push session`.
- Push explícito de `feat/hcm-attendance-insight` a `https://github.com/edwinduque/talentassistant` devolvió `Everything up-to-date`; no se transfirieron objetos nuevos. No se usó `--force`, no se abrió PR ni se hizo merge. `.atl/` permanece local y sin seguimiento.
