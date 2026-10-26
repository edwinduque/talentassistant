# Visión del producto

## 1. Problema

Los responsables de Recursos Humanos necesitan comprender situaciones a partir de información distribuida en el HCM que ya administra los procesos de negocio.

Talent Assistant debe relacionar esa información y presentar conclusiones comprensibles y verificables sin convertirse en otro sistema de registro ni duplicar las reglas del HCM.

## 2. Visión

Talent Assistant será un módulo de asistencia inteligente integrado con un HCM existente. Consultará, relacionará e interpretará los datos que el HCM exponga para ayudar al responsable de RR. HH. a entender qué ocurre, qué evidencia sustenta la conclusión y qué información debe revisar.

El HCM seguirá siendo el sistema fuente de empleados, jornadas, marcajes, TimeOff, Tasks y reglas de negocio. Talent Assistant no lo reemplazará, no gestionará esos procesos y no reproducirá su motor de reglas. En el MVP, una fuente sintética representará sus contratos y respuestas; la conexión real queda para una fase posterior aprobada.

## 3. Usuarios objetivo

Usuario principal: líder o responsable de Recursos Humanos.

Necesidades principales:

- Consultar información que el HCM exponga.
- Comprender incidencias mediante sus resultados de reglas y evidencia asociada.
- Relacionar información pertinente de otros datos del HCM, como TimeOff.
- Revisar qué información sustenta una explicación y qué falta por comprobar.
- En futuras versiones, formular preguntas en lenguaje natural y recibir respuestas respaldadas por fuentes verificables.

## 4. Propuesta de valor

Una capa de asistencia que presenta información del HCM con contexto, procedencia y evidencia, manteniendo al HCM como fuente de verdad y dejando los procesos operativos en su sistema propietario.

## 5. Principios del producto

1. Utilidad antes que complejidad tecnológica.
2. Datos verificables antes que respuestas generativas.
3. El HCM conserva la propiedad de los datos y reglas de negocio.
4. No duplicar módulos ni motores de reglas del HCM.
5. Integraciones mediante contratos sustituibles.
6. Autorización en el backend.
7. IA incorporada progresivamente y con revisión humana.

## 6. Indicadores de éxito propuestos

Para el MVP:

- Los usuarios pueden completar los principales casos de consulta.
- Las explicaciones muestran evidencia y procedencia de la fuente.
- Los resultados de reglas del HCM no se recalculan con un motor paralelo.
- El proyecto puede ejecutarse localmente siguiendo la documentación.
- Los requisitos están vinculados a criterios de aceptación.

Para las fases de IA:

- Porcentaje de respuestas correctas en un conjunto de evaluación.
- Porcentaje de respuestas respaldadas por fuentes válidas.
- Tasa de respuestas sin evidencia suficiente.
- Latencia y coste por consulta.
- Porcentaje de llamadas a herramientas autorizadas correctamente.
- Tasa de ejecución correcta de tareas de varios pasos.

Los objetivos numéricos deberán definirse antes de cada evaluación; no se considerarán cumplidos por impresión subjetiva.

## 7. Estado respecto a la visión (2026-10-19)

El norte no cambia: Talent Assistant debe convertirse en un asistente conversacional que responda preguntas en lenguaje natural sobre los datos del HCM.

La fase de LLM arrancó con SPEC-009 y ADR-0011: la página `/assistant` (ES/EN, con sesión) envía la pregunta y el historial a `POST /chat`, y la API responde mediante herramientas ejecutadas con la sesión e idioma del usuario, con Groq como proveedor por API compatible OpenAI. SPEC-010 añade los guardarraíles de ámbito y anti-alucinación: el asistente se limita a RR. HH./HCM y no inventa datos. Las preguntas de SPEC-008 y sus enmiendas se verifican con escenarios deterministas. El origen entrega el próximo cumpleaños y salario de empleados y datos bancarios permanecen restringidos. Se admite redactar un borrador de oferta laboral con requisitos escritos por RR. HH.; el borrador queda solo en el chat. Sin clave de proveedor la aplicación degrada con mensajes controlados y sigue funcionando sin IA. Quedan pendientes de fases posteriores el streaming de respuestas, RAG sobre documentos (FR-014), agentes con roles (FR-017) y las decisiones de política de datos y de límites de tasa que exige un despliegue productivo con proveedor externo.

Hoy están entregados el vertical de revisión de evidencias y la plataforma local. SPEC-008 añadió las capacidades de consulta que el asistente usa como herramientas —directorio, presencia, TimeOff y tareas— y sus enmiendas de 2026-10-23 incorporan cargo, fecha de contratación, búsqueda por apellido y próximo cumpleaños. El banco automatizado cubre diez preguntas; la consulta de cumpleaños se ordena en la fuente HCM y no expone el año de nacimiento. SPEC-009 añade el borrador conversacional de ofertas laborales sin persistencia ni publicación.

SPEC-011 (decisión en ADR-0012) añade un conector PostgreSQL del HCM detrás de vistas de solo lectura, grants y transacción `READ ONLY`, con degradación honesta y sin fallback sintético silencioso. ADR-0014, aprobado el 2026-10-25, mantiene esos controles y permite que Infrastructure compile consultas dinámicas parametrizadas desde planes validados. La fuente por defecto sigue siendo sintética; la validación contra la base real queda sujeta a los prerrequisitos de SPEC-011 §10, y MCP permanece como opción futura de exposición.

SPEC-020 añade consultas dinámicas sin una plantilla por pregunta. El catálogo del asistente sigue cerrado, ahora con `query_hr_data`; puede consultar campos no sensibles disponibles en el contrato HCM y calcular agregados en la fuente. No modifica datos, no acepta SQL libre del modelo y mantiene excluidos salario, número de cuenta y año de nacimiento crudo. SPEC-021 añade el botón «Nuevo chat» y la presentación segura de las respuestas con un subconjunto de markdown renderizado como componentes, sin interpretar HTML. SPEC-022 añade Gemini 3.8 Flash como segundo proveedor, con reparto pegajoso por conversación, failover y cortacircuitos; sin la segunda clave el asistente sigue funcionando con Groq.

SPEC-012 (aprobada e implementada en la rama `feat/hcm-real-schema-data`) hace que el conector local sirva el contrato desde tablas con la forma real del HCM y con presencia derivada; los dos gaps documentados —sin tabla de tareas ni de hallazgos de regla evaluados, servidos aún desde la sustitución legada— son el camino documentado hacia la validación contra la base real, y el alcance de los nueve empleados de fixture se amplió el 2026-10-25 a todos los empleados activos («Ampliar a los 209 empleados reales»), manteniendo la paridad campo a campo sobre los nueve fixtures.
