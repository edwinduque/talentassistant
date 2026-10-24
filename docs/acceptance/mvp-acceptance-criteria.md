# Criterios de aceptación del MVP

## AC-001 — Ejecución local

Dado un entorno que cumple los prerrequisitos documentados, el desarrollador puede iniciar la aplicación siguiendo las instrucciones del repositorio, sin credenciales corporativas ni servicios productivos.

## AC-002 — Datos sintéticos

El adaptador sintético puede devolver respuestas HCM reproducibles para los casos de uso aprobados. La regeneración sigue un procedimiento documentado; los datos no son reales y no modelan un HCM completo.

## AC-003 — Dashboard

Los resúmenes del asistente se derivan de información suministrada por el HCM. Se distingue qué resultado fue calculado por el HCM y cuál es una presentación o relación hecha por Talent Assistant; las fuentes y definiciones relevantes son trazables.

## AC-004 — TimeCheck

En el caso de uso aprobado, el responsable de RR. HH. puede consultar una incidencia de marcaje reportada por el HCM, ver el resultado de su regla, los datos y la evidencia que lo sustentan, la información que requiere revisión y el país y ciudad de la oficina asociada al empleado. El rango incluye `from` y excluye `to` según la zona horaria de esa oficina. Talent Assistant no recalcula la regla de marcaje.

## AC-005 — TimeOff

La información de TimeOff del HCM se presenta como contexto de un caso de asistencia cuando la fuente la relacione como pertinente. Talent Assistant no crea, modifica ni aprueba solicitudes.

## AC-006 — Task Control

Tasks pertenece al HCM. No se requiere un caso de uso de Task Control para aceptar la primera vertical; cualquier consulta de tareas necesita una especificación posterior y no implica gestionar su ciclo de vida.

## AC-007 — Límites arquitectónicos

El dominio no depende de Infrastructure, API, frontend, esquema HCM ni SDK específicos de IA. Application define el contrato de lectura; Infrastructure implementa la fuente sintética sustituible. Talent Assistant no contiene un segundo motor de reglas HCM.

## AC-008 — Errores de API

Las entradas inválidas y los fallos de la fuente de integración producen respuestas controladas. Las respuestas y logs no exponen stack traces, credenciales ni datos sensibles.

## AC-009 — Integración continua

La canalización ejecuta compilación y pruebas automatizadas. Un fallo bloquea la validación satisfactoria del cambio.

Este criterio permanece vigente para el MVP; su implementación se asignó a SPEC-014 (canalización `.github/workflows/ci.yml`: build, suite completa contra PostgreSQL, gitleaks).

## AC-010 — Trazabilidad SDD

Cada funcionalidad entregada está vinculada con requisitos y criterios de aceptación. Las pruebas pertinentes están identificadas y sus resultados se documentan.

## AC-011 — Ausencia de dependencias de IA en el MVP base

El MVP puede ejecutar sus funcionalidades operativas sin claves de proveedores LLM, frameworks de agentes, n8n o MCP.

## AC-012 — Stack local contenedorizado (propuesto en SPEC-004)

Desde un checkout limpio y con Docker Desktop disponible, el desarrollador puede construir y arrancar API, Web y la base de datos propia (PostgreSQL, ADR-0013/SPEC-013) usando sus Dockerfiles y Docker Compose; los servicios quedan saludables y la interfaz Web es accesible localmente.

## AC-013 — Semilla sintética reproducible (propuesto en SPEC-004)

La base local contiene una semilla determinista de fixtures sintéticos que consume la fuente HCM de la API. Reaplicar la inicialización no duplica registros ni altera las reglas/resultados HCM; no se utilizan datos reales.

## AC-014 — Guía de ejecución local contenedorizada (propuesto en SPEC-004)

La documentación explica prerrequisitos, configuración local de secretos, construcción, inicio, URLs, estado de salud, parada y conservación/reinicio de la base, incluyendo advertencias para operaciones que eliminan el volumen.

## AC-015 — Límites del entorno contenedorizado local (propuesto en SPEC-004)

Los puertos publicados se limitan a loopback; credenciales y valores locales no se incorporan a imágenes ni al repositorio. La propuesta no habilita exposición en redes compartidas ni despliegue productivo.

AC-012..AC-015 fueron aprobados como alcance de SPEC-004 el 2026-10-15. Siguen pendientes de implementación y verificación; no se consideran satisfechos.

## AC-016 — Autenticación de sesión (propuesto en SPEC-005)

Dado el stack local arrancado con el usuario sembrado, cuando el desarrollador abre la interfaz sin sesión entonces es redirigido a `/login`; con credenciales correctas accede a las páginas protegidas, ve los insights como antes y puede cerrar la sesión. Sin sesión válida, la API responde HTTP 401 controlado en `/attendance/insights`, mientras que `/health` y `/health/database` siguen públicas. Nota (SPEC-019): la página `/attendance-insights` se retiró de la interfaz Web (FR-035); los insights siguen disponibles en el asistente y en `GET /attendance/insights`, que conserva su 401 sin token.

## AC-017 — Seguridad de credenciales locales (propuesto en SPEC-005)

Dado el flujo de login local, cuando una autenticación falla entonces la API devuelve una única respuesta 401 genérica, idéntica para usuario inexistente y contraseña errónea, sin enumerar usuarios ni exponer detalles internos, y tras intentos repetidos la cuenta queda bloqueada temporalmente. Las contraseñas se almacenan solo como hash; la clave de firma y las credenciales de siembra provienen exclusivamente de configuración externa; ni la API ni la interfaz registran secretos ni tokens.

## AC-018 — Compatibilidad con lo existente (propuesto en SPEC-005)

Dado el perfil host y el perfil Compose, cuando se aplica la autenticación entonces ambos siguen operativos, la suite completa permanece verde, el dataset de asistencia y su semilla no cambian, la base de pruebas separada no recibe usuarios de demostración y no se introduce ningún servicio externo ni dato real.

AC-016..AC-018 fueron propuestos por SPEC-005; T1/T2 aportan evidencia y T3 completa la documentación.

## AC-019 — Selector de idioma anónimo (propuesto en SPEC-006)

La persona sin sesión puede cambiar el idioma del login entre español e inglés; la elección se aplica de inmediato y persiste tras recargar.

## AC-020 — Perfil de usuario (propuesto en SPEC-006)

El usuario autenticado gestiona su perfil: el nombre visible aparece en el layout, el idioma elegido localiza home, asistencia y textos de navegación/error, y el cambio de contraseña exige la actual y respeta la política de Identity. Nota (SPEC-019): la localización de la página de asistencia cesó con su retiro; home, perfil, asistente y textos de navegación/error conservan ambos idiomas.

## AC-021 — Localización sin cambiar datos ni contratos (propuesto en SPEC-006)

La localización no altera los identificadores ni el contrato: el dataset sintético y los endpoints de asistencia conservan ids, fechas, zonas horarias y campos idénticos en cualquier idioma, mientras que los textos de presentación se sirven en el idioma del usuario, la migración es aditiva, la suite permanece verde y no se introducen servicios externos ni datos reales.

AC-019..AC-021 fueron propuestos por SPEC-006; T1/T2 aportan evidencia de implementación y T3 completa la documentación.

Nota: SPEC-007 enmienda AC-021; la igualdad en cualquier idioma se mantiene para identificadores y contrato, y cambia para los textos de presentación, que pasan a servirse por idioma.

## AC-023 — Contenido por idioma del usuario (propuesto en SPEC-007)

Con el usuario en español, la API devuelve reglas, evidencias, tipos y estados de TimeOff en español; en inglés, la variante inglesa; los identificadores y fechas son idénticos entre ambas respuestas. El cambio de idioma en el perfil se refleja en la siguiente consulta, sin volver a iniciar sesión.

## AC-024 — Semilla bilingüe idempotente (propuesto en SPEC-007)

La semilla es idempotente en la versión 2, un payload de versión desconocida sigue fallando de forma controlada, la suite permanece verde y `talentassistant_tests` no recibe la semilla.

## AC-025 — Presencia por fecha (propuesto en SPEC-008)

La consulta de presencia para una fecha devuelve cada empleado con el estado informado por la fuente; «trabajando», «no ha llegado» y «de vacaciones» responden las preguntas 1–3 con los fixtures. La presencia de hoy se ancla a la fecha local más dos días; para cualquier otra fecha la respuesta es vacía, nunca inventada.

## AC-026 — TimeOff por empleado (propuesto en SPEC-008)

La búsqueda por nombre localiza a Juan Pérez y Pedro Rojas **y es insensible a acentos («Juan Perez» sin acento localiza a Juan Pérez); una búsqueda corta compartida («Juan») devuelve a los dos Juans sin elegir en silencio**; sus respuestas de TimeOff informan días y fecha de regreso tal como los declara la fuente, sin cálculos de Talent Assistant. El filtro por identificador o nombre devuelve la colección completa sin filtro y, ante varios nombres coincidentes, los elementos de todos, sin selección silenciosa. *(Enmienda SPEC-008, 2026-10-18.)*

## AC-027 — Tareas por empleado (propuesto en SPEC-008)

Las tareas de Mauricio Cardenas se consultan por nombre y la fuente determina qué significa «pendiente»; FR-004 queda implementado. Los estados de tarea se presentan tal como los declara la fuente.

## AC-028 — Banco de preguntas y verificación (propuesto en SPEC-008)

El banco de preguntas automatizado cubre las diez preguntas de referencia, la suite completa permanece verde, el home muestra «Presencia de hoy», la página de asistencia ya no pide id manual y todo el contenido nuevo existe en español e inglés. El banco no depende de IA ni de red externa y sirve de referencia para la evaluación futura del asistente.

## AC-029 — Banco de preguntas del chat (propuesto en SPEC-009)

Las diez preguntas de SPEC-008, formuladas en el chat, producen respuestas que contienen los valores esperados de los fixtures y citan la herramienta usada.

## AC-030 — Catálogo fijo, sesión y sin escrituras (propuesto en SPEC-009)

El modelo solo invoca el catálogo fijo, las invocaciones usan la sesión e idioma del usuario, no hay escrituras y una pregunta sin datos recibe una respuesta de «sin información» del propio asistente.

## AC-031 — Degradación y verificación (propuesto en SPEC-009)

Sin clave o sin red la aplicación degrada con mensajes localizados controlados; secretos solo en `.env`; límites de tiempo/rondas activos; suite completa en verde y recorrido de navegador del chat en ES y EN.

## AC-032 — Desambigüación de nombres en el chat (propuesto en SPEC-009)

Una pregunta por una persona con nombre compartido («Juan») recibe una respuesta del asistente que lista los candidatos y pregunta cuál antes de responder; tras la elección responde con el dato del fixture, y un nombre sin acento («Juan Perez») resuelve al empleado correcto. *(Enmienda 2026-10-18.)*

## AC-033 — Rechazo de peticiones ajenas (propuesto en SPEC-010)

Las preguntas 1, 2 y 5 del banco producen respuestas sin artefactos de código ni contenido pedido, con marcador de rechazo/redirección a RR. HH./HCM en el idioma del usuario, `toolsUsed` vacío y la conversación continúa operativa.

## AC-034 — Datos no disponibles sin fabricar (propuesto en SPEC-010)

Las preguntas 3 y 4 no llevan a ningún dato fabricado: sin cifras de salario ni cuentas bancarias y sin el empleado inventado, con la declaración explícita de información no disponible.

## AC-035 — Regresión de las siete preguntas (propuesto en SPEC-010)

Tras el cambio, las siete preguntas de referencia siguen respondiendo con los datos correctos y la suite completa permanece en verde.

AC-033..AC-035 fueron propuestos por SPEC-010; T1/T2 aportan evidencia y T3 completa la documentación.

## AC-036 — Modos de fuente sin mezcla (propuesto en SPEC-011)

Con `Hcm__Source=Postgres`, las seis capacidades del asistente responden con los datos de la PostgreSQL local sembrada en el esquema del contrato (mismos formatos de antes), y con `Sintética` responden con los fixtures — nunca se mezclan orígenes en una misma respuesta.

## AC-037 — Contrato de solo lectura verificable (propuesto en SPEC-011)

El rol de la aplicación no puede escribir (grants de solo lectura aplicados), toda petición corre en transacción `READ ONLY` con *statement timeout*, las consultas son fijas y parametrizadas (grep de SQL dinámico vacío), el transporte exige TLS verificado para hosts no-loopback (`Ssl Mode=VerifyFull` validado en el arranque; los bucles locales quedan exentos por la base de pruebas local sin TLS) y los logs no contienen valores de personas. *(Reforzado el 2026-10-19 por SEC-001.)*

## AC-038 — Degradación honesta con la base apagada (propuesto en SPEC-011)

Si la base del HCM no responde en modo Postgres, los endpoints devuelven el error localizado de fuente no disponible sin datos sintéticos; la suite completa permanece en verde sin tocar ninguna base real.

AC-036..AC-038 fueron propuestos por SPEC-011; T1/T2 aportan evidencia y T3 completa la documentación. Evidencia observada en la rama `feat/hcm-postgres-source`: suite completa en verde (127/127: 10 unitarias + 117 de integración) con las dos bases de prueba activas, compilación sin errores, paridad exacta entre el contrato sembrado y las fuentes sintéticas, y sondas de solo lectura (lectura de vistas permitida, escritura sobre la vista denegada, lectura de tablas físicas denegada). Queda pendiente la validación contra la base real del HCM (requisitos previos de SPEC-011 §10): rol de solo lectura sobre las vistas, credenciales locales, TLS con `Ssl Mode=VerifyFull` (desde el 2026-10-19 el arranque lo exige para hosts no-loopback; SEC-001) y la decisión de egreso §10, resuelta el 2026-10-19 (Groq para la fase de prueba de concepto, reabre antes de fase productiva).

## AC-039 — Contrato intacto sobre el shape real (propuesto en SPEC-012)

Con las vistas recableadas sobre el esquema real local, **cero cambios** en adaptadores, Application, Web y puertos, y la suite completa permanece verde (integración ≥132 con ambas bases)

## AC-040 — Seed dummy robusto y determinista (propuesto en SPEC-012)

El seed dummy cumple los volúmenes y casos límite de §6 con integridad referencial (FK) y es determinista (misma init → mismos conteos: `employees≥200`, `timeoff≥600`, `time_check≥3000`…)

## AC-041 — Presencia derivada sobre el shape real (propuesto en SPEC-012)

La presencia derivada (§5) reproduce los estados esperados para hoy: los empleados sin registros de hoy figuran como `not-arrived`, los que tienen entrada sin fin de jornada como `working`, los de jornada finalizada como `absent` y los con `timeoff` aprobado como `vacation`; las preguntas del banco responden igual

AC-039..AC-041 fueron propuestos por SPEC-012 (aprobada el 2026-10-20) y quedan implementados en la rama `feat/hcm-real-schema-data`. Evidencia observada: compilación sin errores (0/0) y suite completa en verde (`SUITE_EXIT=0`) con integridad de integración 132/132 sobre ambas bases; volúmenes sembrados de 209 empleados, 7 `timeoff_type`, 620 `timeoff`, 7 `working_day`, 206 asignaciones, 18.651 `time_check` (~45 días incluyendo hoy) y 60 retrasos; huellas md5 por tabla idénticas tras dos reinicializaciones completas y 12 sondas de integridad (FK y enlaces) a cero; los nueve identificadores exactos del contrato servidos desde `public.employee`; mapa de presencia de hoy de 9 estados alineado con el fuente sintético corregido (`ends_before` 16/17/20 con duraciones derivadas 3/1/5); tareas=4 e insights=1 servidos desde la sustitución legada; el rol de aplicación no lee las tablas físicas (`permission denied for table employee`) ni escribe a través de las vistas (códigos `42501` o `55000`). AC-041 queda verificado sobre la vista; la pasada en vivo opcional de las siete preguntas con la clave sigue pendiente (opcional según el criterio).

## AC-042 — Landing pública (propuesto en SPEC-017)

Una persona sin sesión puede abrir `/` y entender que Talent Assistant ayuda a responsables de RR. HH. a revisar información y evidencia de asistencia suministradas por el HCM. La landing presenta navegación superior hacia sus secciones y hacia `/login`, y sus textos se localizan en español e inglés; esta iteración no incluye video ni un reproductor de placeholder.

## AC-043 — Acceso autenticado conservado (propuesto en SPEC-017)

El resumen operativo se presenta en `/overview` y continúa protegido: una persona sin sesión que lo abre es enviada a `/login`. Un login sin `ReturnUrl` explícito dirige a `/overview`; `/login` permanece accesible sin sesión.

## AC-044 — Identidad visual unificada (propuesto en SPEC-018)

La landing, el login, las pantallas operativas (`/overview`, `/assistant`, `/profile` y `/admin/users`), las páginas de error y el diálogo de reconexión comparten la paleta, tipografía, superficies, controles y acciones del lenguaje visual de la landing. La interfaz conserva su contenido localizado, jerarquía propia y comportamiento existente; se mantiene usable en escritorio y móvil, con navegación por teclado, foco visible y modos claro/oscuro y de alto contraste.

## AC-045 — Retiro de la página de asistencia y perfil modernizado (propuesto en SPEC-019)

La navegación y la interfaz Web ya no incluyen `/attendance-insights`: el componente fue retirado y la ruta dejó de servir la página sin exponerla de forma anónima, mientras que `GET /attendance/insights` y la herramienta `attendance_insights` del asistente siguen operativos con token. La página `/profile` presenta su contenido con el lenguaje visual unificado (cabecera de identidad con iniciales, tarjetas en rejilla responsive, modos claro/oscuro) conservando formularios, endpoints, campos, mensajes localizados ES/EN y estados de error y éxito sin cambios de comportamiento.

## AC-046 — Cargo del empleado en el asistente (enmienda de SPEC-008/009)

La consulta «Que cargo tiene mauricio» devuelve el valor de `"job:title"` desde la fuente HCM. El payload de `search_employees` contiene únicamente identificador, nombre, oficinas, cargo, apellido y fecha de contratación (campos aprobados en AC-049/AC-050); no contiene sueldo, cuenta bancaria ni otros atributos no aprobados.

## AC-047 — Próximo cumpleaños desde la fuente HCM (enmienda de SPEC-008/009)

La consulta «Quien es la proxima persona que cumple años» devuelve todos los empleados empatados en la fecha más próxima, incluida hoy, y la fecha del próximo cumpleaños calculada por la fuente a partir del mes y día. El payload no contiene la fecha de nacimiento original ni su año.

## AC-048 — Exclusión de sueldo y datos bancarios (enmienda de SPEC-010)

El catálogo fijo y los payloads del asistente no exponen sueldos de empleados ni datos bancarios; si se solicitan, las instrucciones del asistente los clasifican como restringidos y la respuesta declara que la información no está disponible, sin inventar valores. Un salario explícitamente proporcionado por RR. HH. puede aparecer solo como término de un borrador de oferta (AC-051/052); otros atributos requieren aprobación explícita.

## AC-049 — Fecha de contratación desde el HCM (enmienda de SPEC-008/009)

Al consultar la fecha de contratación de un empleado, `search_employees` devuelve el `HireDate` informado por la fuente HCM, sin exponer campos adicionales ni cambiar la respuesta HTTP existente de `GET /employees`.

## AC-050 — Búsqueda por fragmento de apellido (enmienda de SPEC-008/009)

La pregunta literal «hay alguien con apellido que tenga z» activa `search_employees` con el filtro de apellido; la búsqueda consulta `last_name` en la fuente y devuelve todos los empleados coincidentes sin elegir uno en silencio.

## AC-051 — Borrador de oferta laboral en el chat (enmienda de SPEC-009)

Ante la petición «Por ejemplo: quiero que crees una oferta de vigilante para la ciudad de Bogotá, horarios rotativos, salario 2.500.000 pesos colombianos y la ia me de un texto de aproximadamente unos 500 caracteres con ese requerimiento», el asistente entrega un borrador solo en el chat, usa los requisitos proporcionados, no invoca herramientas y no lo guarda ni publica. Si falta un dato esencial, hace una pregunta breve; no inventa condiciones opcionales.

## AC-052 — Salario de oferta frente a compensación restringida (enmienda de SPEC-010)

El asistente puede repetir en un borrador el salario que RR. HH. proporcionó como término de la oferta; no revela ni infiere el sueldo de empleados ni datos bancarios.

## Condiciones generales

Los criterios deberán verificarse con pruebas automatizadas cuando sea razonable y con pruebas de aceptación documentadas cuando la automatización no aporte suficiente valor.

No se declarará el MVP aceptado si existen criterios obligatorios sin verificar.

## Trazabilidad con requisitos generales

Los identificadores de esta tabla pertenecen al catálogo general y no se reutilizan como IDs de requisitos de SPEC-001.

| Criterio | Requisitos generales relacionados | Alcance respecto a SPEC-001 |
|---|---|---|
| AC-001 | FR-010, NFR-004, NFR-008 | La fundación documenta su flujo local; no implica que ya se haya implementado. |
| AC-002 | FR-006, NFR-005, NFR-008 | La estrategia de datos sintéticos se aplica cuando existan entidades; SPEC-001 permite aplazar la carga si no hay modelo de negocio. |
| AC-003 | FR-001, FR-005, FR-008, FR-011, NFR-003 | Resúmenes y cálculos del asistente requieren una especificación funcional sobre datos y resultados del HCM. |
| AC-004 | FR-002, FR-005, FR-006, FR-007, FR-008, FR-019, NFR-010 | SPEC-002 aprobada define la explicación de incidencias; sus criterios aún no están verificados y las reglas las evalúa el HCM. |
| AC-005 | FR-003, FR-006, FR-007, FR-019, NFR-010 | TimeOff es contexto suministrado por el HCM; no es un flujo de gestión de Talent Assistant. |
| AC-006 | FR-004, FR-006, FR-007, NFR-010 | Tasks sigue siendo propiedad del HCM; una consulta de tareas se reserva para una especificación posterior. |
| AC-007 | FR-018, NFR-001, NFR-002 | Los límites de Clean Architecture y el puerto/adaptador HCM se verifican en cada especificación funcional. |
| AC-008 | FR-009, NFR-004, NFR-006, NFR-007 | Los errores controlados incluyen fallos de la fuente HCM, sin exponer datos internos. |
| AC-009 | NFR-009 | Canalización implementada según SPEC-014; el run en vivo del CI queda pendiente del primer push del usuario. |
| AC-010 | FR-011, NFR-015 | SPEC-001 incorpora IDs únicos y una matriz de criterio a requisito y verificación. |
| AC-011 | FR-012–FR-017, NFR-013, NFR-014, NFR-016 | Las capacidades de IA permanecen fuera del alcance de la fundación. |
| AC-012 | NFR-017, FR-010 | Requiere la aprobación de SPEC-004 y de la propuesta ADR-0006; afecta el supuesto actual de ejecución local en el host. |
| AC-013 | FR-006, FR-020, NFR-005, NFR-017 | Solo persistirá el fixture mínimo necesario para representar respuestas sintéticas del HCM; no crea módulos HCM. |
| AC-014 | FR-010, NFR-008, NFR-017 | La guía actual se actualiza cuando la implementación Docker sea aprobada y verificada. |
| AC-015 | NFR-004, NFR-005, NFR-017 | Desarrollo local solamente; no sustituye autenticación ni despliegue productivo. |
| AC-016 | FR-021, FR-022, NFR-018 | Pertenece a SPEC-005: login y cierre de sesión en Web, token exigido en endpoints de negocio y sondas de salud públicas. |
| AC-017 | NFR-004, NFR-018, NFR-006 | Respuestas de login genéricas, hash de contraseñas y secretos solo en configuración externa, según SPEC-005 y ADR-0007. |
| AC-018 | FR-006, FR-007, NFR-005, NFR-017 | La autenticación no cambia el dataset sintético, la fuente HCM ni añade servicios externos ni datos reales. |
| AC-019 | FR-024, NFR-019 | Selector anónimo en el login que fija la cookie de cultura; SPEC-006 y ADR-0008. |
| AC-020 | FR-023, NFR-018 | Perfil con nombre visible, idioma preferido y cambio de contraseña exigiendo la actual; SPEC-006 y ADR-0008. |
| AC-021 | FR-024, NFR-005, NFR-017, NFR-019 | La localización es solo de interfaz: datos y contratos intactos, migración aditiva y sin servicios externos ni datos reales. Enmienda SPEC-007: los textos de presentación se sirven por idioma. |
| AC-023 | FR-025, NFR-020 | Contenido sintético por idioma del usuario autenticado con identificadores idénticos; SPEC-007. |
| AC-024 | FR-025, NFR-006, NFR-017 | Semilla bilingüe idempotente en versión 2 y fallo controlado ante versión desconocida; SPEC-007. |
| AC-025 | FR-026, FR-006 | Presencia por fecha con estados informados por la fuente; SPEC-008. |
| AC-026 | FR-027, FR-008 | TimeOff por empleado con valores literales de la fuente, sin cálculos; SPEC-008. Enmienda SPEC-008 (2026-10-18): búsqueda insensible a acentos y doble «Juan» sin selección silenciosa. |
| AC-027 | FR-004, FR-027 | Tareas por empleado; FR-004 queda activo; SPEC-008. |
| AC-028 | FR-011, NFR-015 | Banco de las diez preguntas de referencia como pruebas automatizadas; SPEC-008. |
| AC-029 | FR-029 | Banco de las diez preguntas en el chat con valores del fixture y herramienta citada; SPEC-009. |
| AC-030 | FR-029, FR-030 | Catálogo fijo con sesión e idioma del usuario y sin escrituras; SPEC-009. |
| AC-031 | FR-030, NFR-021 | Degradación controlada, secretos solo en `.env`, límites activos y E2E del chat en ES y EN; SPEC-009. |
| AC-032 | FR-027, FR-029 | Desambigüación de nombres en el chat; SPEC-009. Enmienda 2026-10-18. |
| AC-033 | FR-031, FR-029 | Rechazo localizado de peticiones ajenas con `toolsUsed` vacío y conversación continúa operativa; SPEC-010. |
| AC-034 | FR-031, FR-029 | Datos no disponibles declarados explícitamente, sin cifras de salario, datos bancarios ni empleados inventados; SPEC-010. |
| AC-035 | FR-029 | Las siete preguntas de referencia siguen verdes y la suite completa permanece en verde tras el cambio; SPEC-010. |
| AC-036 | FR-032, FR-033 | Modos Postgres y Sintético sin mezcla de orígenes; T1/T2 de la rama `feat/hcm-postgres-source`; SPEC-011. |
| AC-037 | NFR-022, FR-032 | Grants de solo lectura, transacción `READ ONLY`, SQL fijo, TLS `VerifyFull` exigido en el arranque para hosts no-loopback (bucles exentos) y logs sin valores de personas; validación contra la base real pendiente; SPEC-011. |
| AC-038 | FR-033 | Degradación localizada de fuente no disponible con la base apagada, sin fallback sintético; SPEC-011. |
| AC-039 | FR-032, FR-033 | Contrato intacto sobre el esquema real local con la suite en verde; T3 de la rama `feat/hcm-real-schema-data`; SPEC-012. |
| AC-040 | NFR-022 | Seed dummy con los volúmenes de §6, FK íntegras y determinismo entre reinicializaciones; T2 de la rama `feat/hcm-real-schema-data`; SPEC-012. |
| AC-041 | FR-026, FR-029 | Presencia derivada con los estados de hoy correctos y el banco de preguntas intacto; T3 de la rama `feat/hcm-real-schema-data`; SPEC-012. |
| AC-042 | FR-024, NFR-012 | Landing pública localizada en español e inglés, con enlaces a secciones y login; SPEC-017. |
| AC-043 | FR-021, FR-034 | `/overview` protegido, `/login` público y destino por defecto tras login; SPEC-017. |
| AC-044 | NFR-012, NFR-019, NFR-023 | Identidad visual común en login y pantallas operativas, sin alterar localización ni comportamiento; SPEC-018. |
| AC-045 | FR-035, NFR-023 | Página de asistencia retirada de la Web con API y asistente intactos; perfil rediseñado sobre los tokens compartidos sin cambios funcionales; SPEC-019. |
| AC-046 | FR-036, FR-026, FR-029 | Cargo del empleado incluido mediante el campo HCM confirmado y payload limitado a la lista aprobada; SPEC-008/009, enmienda 2026-10-23. |
| AC-047 | FR-036, FR-029 | Próximo cumpleaños ordenado en la fuente HCM, con fecha inclusiva y sin año de nacimiento en el payload; SPEC-008/009, enmienda 2026-10-23. |
| AC-048 | FR-031, FR-036, FR-029 | Sueldo y datos bancarios excluidos de herramientas y respuestas; sin habilitar atributos no aprobados; SPEC-010, enmienda 2026-10-23. |
| AC-049 | FR-037, FR-029 | La herramienta expone la fecha de contratación aprobada desde HCM y el endpoint `GET /employees` mantiene su forma original; SPEC-008/009. |
| AC-050 | FR-037, FR-029 | Búsqueda por `last_name` con filtro `surname_contains`, incluyendo todos los candidatos y sin selección silenciosa; SPEC-008/009. |
| AC-051 | FR-038, FR-029 | Borrador de oferta en chat, con requisitos user-supplied, sin tool calls ni persistencia; SPEC-009. |
| AC-052 | FR-036, FR-038, FR-031 | Salario de oferta solo si el usuario lo proporcionó; compensación de empleados y datos bancarios siguen restringidos; SPEC-010. |

Los AC-003 a AC-006 no se consideran satisfechos por la estructura arquitectónica creada en SPEC-001. Cada caso de asistencia necesita requisitos, pruebas y verificación propios; Tasks no forma parte de la primera vertical. NFR-011 no tiene todavía un umbral medible y deberá concretarse cuando se definan dataset y entorno. NFR-012 se verifica en cada especificación de interfaz; estos criterios no inventan un umbral cuantitativo de contraste.
