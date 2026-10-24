# SPEC-019 — Retiro de la página de asistencia y rediseño del perfil

- **Estado:** Implementación completada; verificación con navegador y verificador independiente (2026-10-23).
- **Requisitos relacionados:** FR-002 (sin cambio de alcance), FR-035 (nuevo), NFR-023.
- **Solicitud del usuario (verbatim):** “Nuevo requerimiento. Quita la pagina de Asistencia, no me da valor agregado, mejora el diseño de perfil porque se ve poco moderna”

## 1. Objetivo

Retirar de la interfaz Web la página `/attendance-insights`, que la persona usuaria consideró sin valor agregado, manteniendo intactos el endpoint, el caso de uso y la herramienta del asistente; y presentar el perfil con el lenguaje visual unificado de SPEC-018, conservando por completo su comportamiento.

## 2. Alcance

- Retirar la página `/attendance-insights`, su CSS aislado, sus recursos ES/EN, el enlace de la navegación principal y los claves `NavAttendance`.
- Retirar el cliente Web específico de asistencia (`BackendApiClient.GetAttendanceInsightsAsync` y `AttendanceInsightsClientContracts.cs`), que solo servía a esa página.
- Conservar sin cambios `GET /attendance/insights`, el caso de uso `GetAttendanceInsights`, las fuentes HCM, la herramienta `attendance_insights` del asistente, los datos de asistencia y sus pruebas de dominio.
- Rediseñar `/profile` con el lenguaje visual de SPEC-018: cabecera de identidad con iniciales, tarjetas en rejilla responsive, tipografía, superficies, controles y acciones sobre los tokens compartidos; añadir la clave `SecurityEyebrow` (ES/EN); suprimir el anillo de foco de la UA sobre el h1 tras `FocusOnNavigate`, con el mismo patrón que la landing de SPEC-017.
- Conservar sin cambios formularios, campos, names, endpoints `/session/profile` y `/session/password`, mensajes, estados de error/éxito, autorización y localización.
- Actualizar documentación viva: FR-035, AC-045, notas de herencia en AC-016/AC-020, lista de AC-044 y la comprobación de humo de `local-setup.md`.

## 3. Fuera de alcance

- La API, Application e Infrastructure: endpoints, casos de uso, fuentes, herramientas del chat y sus pruebas permanecen intactos.
- La semilla y los datos de asistencia.
- Renombrar identificadores internos compartidos (por ejemplo, las clases `.attendance-page`/`.attendance-heading`, hoy usadas también por `/admin/users` y `/profile`).
- Cambios de autenticación, autorización o contratos de sesión.

## 4. Criterio de aceptación

- **AC-045:** La navegación y la interfaz Web ya no incluyen `/attendance-insights`: el componente fue retirado y la ruta dejó de servir la página sin exponerla de forma anónima, mientras que `GET /attendance/insights` y la herramienta `attendance_insights` del asistente siguen operativos con token. La página `/profile` presenta su contenido con el lenguaje visual unificado (cabecera de identidad con iniciales, tarjetas en rejilla responsive, modos claro/oscuro) conservando formularios, endpoints, campos, mensajes localizados ES/EN y estados de error y éxito sin cambios de comportamiento.

## 5. Verificación

- RED observado: `dotnet test ... --filter FullyQualifiedName~Attendance_page_is_removed` falla con `Assert.Null() Failure: Value is not null — typeof(...AttendanceInsights)` mientras el componente existe.
- GREEN: pruebas focalizadas `PublicLandingRouteTests` — 5/5 (incluye la prueba de retiro, la protección de `/overview` y el `ReturnUrl` ahora ejemplificado con `/assistant`).
- Unitarias: 11/11.
- Suite completa local con las dos bases configuradas (`ConnectionStrings__TalentAssistantTests` + `HcmPostgres__ConnectionString`): integración 141/142 y unitarias 11/11. La única falla, `PostgresWorkforceSourceParityTests.TodaysPresenceStatesMatchTheSyntheticSource`, se reproduce aislada y es un artefacto horario del entorno local, no del cambio: a las 19:11 (-05:00) el contenedor ya estaba en UTC 2026-10-24 mientras la semilla HCM pertenece al día UTC anterior, así que Postgres no ve marcajes "hoy" y el sintético (hora local) sí. La ventana local es 19:00–24:00 (-05:00); CI es inmune (runner y contenedores en UTC con semilla fresca por ejecución).
- Navegador sobre el contenedor local con sesión real:
  - Login → 302 → `/overview`; navegación sin "Asistencia": `Resumen | Asistente | NavAdmin | Perfil`.
  - `/profile`: cabecera de identidad brand-deep con iniciales ("DE"), eyebrow `Security`/`Seguridad`, dos tarjetas en rejilla (516px + 516px a 1200; una columna a 390), sin overflow horizontal (scrollWidth ≤ innerWidth) y sin errores JS.
  - Modo oscuro: fondos, avatar ámbar y acciones claras correctos; EN verificado vía `/session/culture` (Profile, Display name and language, Security, Change password, Save, navegación `Overview | Assistant | NavAdmin | Profile`).
  - Formulario de perfil con valores sin cambio: POST `/session/profile` → 302 a `/profile`, sin `profileError`; el campo conserva el valor.
  - `/attendance-insights`: anónimo → 302 a `/login`; con sesión → 404 (página retirada). API `GET /attendance/insights` sin token → 401.
  - El anillo de foco de la UA sobre el h1 (por `FocusOnNavigate` de Routes.razor) se suprime en el perfil con el mismo patrón ya usado por la landing en SPEC-017.
- El rediseño del perfil es presentación: no existen pruebas automatizadas de regresión visual, por lo que se verifica con inspección renderizada (Risk item 6).

### Observación fuera de alcance

La clave `NavAdmin` no existe en ningún recurso ES/EN: la navegación muestra la clave cruda `NavAdmin` para usuarios administradores. Es un defecto preexistente de SPEC-016, ajeno a este requerimiento; queda reportado al usuario sin modificar aquí.

## 6. Riesgo

Risk: item 6 (el rediseño del perfil requiere inspección renderizada; las pruebas automatizadas no son pruebas de regresión visual).
