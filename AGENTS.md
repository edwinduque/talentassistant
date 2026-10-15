# AGENTS.md — Talent Assistant

## Contexto

Este repositorio implementa Talent Assistant. El desarrollo se guía por especificaciones versionadas y decisiones arquitectónicas explícitas.

## Antes de trabajar

1. Lee `README.md`.
2. Identifica la especificación relacionada con la tarea.
3. Revisa los criterios de aceptación.
4. Consulta los ADR y las decisiones pendientes.
5. Inspecciona el código existente antes de proponer cambios.

## Reglas de desarrollo

- No inventes requisitos ni amplíes el alcance sin aprobación.
- No consideres aprobadas las tecnologías que figuran como propuestas.
- Mantén la trazabilidad entre especificaciones, implementación y pruebas.
- Prioriza cambios pequeños, verificables y fáciles de revisar.
- Evita dependencias innecesarias y abstracciones prematuras.
- No introduzcas microservicios sin una necesidad demostrable.

## Arquitectura

- El dominio no debe depender de API, frontend, infraestructura ni SDK de IA.
- Los casos de uso deben residir en Application.
- Infrastructure implementa los contratos de acceso a datos y servicios externos.
- La API y la interfaz no deben contener reglas de negocio que correspondan al dominio.
- La interfaz obtiene los datos a través de contratos de backend.

## Inteligencia artificial

- No incorpores un proveedor LLM hasta que exista una especificación aprobada.
- No permitas consultas SQL arbitrarias generadas por un modelo.
- Expón herramientas de negocio mediante contratos controlados.
- Aplica autorización en el backend, no en el prompt.
- No permitas escrituras autónomas de IA en el MVP.
- No incluyas secretos ni datos personales reales en prompts, pruebas o logs.
- Distingue los datos recuperados de las inferencias del modelo.

## Pruebas y calidad

- Añade pruebas para los comportamientos nuevos o modificados.
- Verifica compilación y pruebas antes de declarar una tarea terminada.
- No afirmes haber ejecutado verificaciones que no se realizaron.
- Documenta fallos, pruebas pendientes y limitaciones conocidas.
- No expongas stack traces ni secretos en errores dirigidos al usuario.

## Gestión de especificaciones

- Trata las especificaciones aprobadas como fuente de verdad.
- Registra las decisiones arquitectónicas relevantes mediante ADR.
- Señala las ambigüedades antes de implementar.
- Actualiza la documentación cuando cambie el comportamiento aprobado.
- Solicita revisión humana para cambios de alcance, decisiones arquitectónicas importantes y acciones externas de riesgo.

## Datos

El MVP utilizará exclusivamente datos sintéticos y no requerirá acceso a sistemas corporativos reales.

## Idioma de la documentación

- Toda la documentación del proyecto debe redactarse en español, incluidos ADR, especificaciones SDD, requisitos, criterios de aceptación e informes.
- Mantener en inglés los nombres de tecnologías, clases, métodos, identificadores de código, rutas de archivos y términos técnicos cuyo uso en inglés sea la convención habitual.
- Los identificadores técnicos y los nombres de archivos existentes no deben traducirse ni renombrarse sin una razón justificada.
- Los prompts de trabajo para los agentes deben redactarse en español, salvo que una herramienta requiera explícitamente otro idioma.
- Antes de crear o modificar documentación, comprobar el idioma de los documentos relacionados y mantener la coherencia.