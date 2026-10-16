# Alcance del producto

## MVP — Asistencia sobre un HCM existente (alcance propuesto)

### Incluido

- Aplicación web y API para presentar resultados de casos de uso de asistencia de RR. HH.
- Contratos de integración que representan datos y resultados suministrados por el HCM.
- Fuente HCM sintética y reproducible para desarrollar el MVP sin conexión productiva.
- Primera vertical aprobada en SPEC-002: mostrar empleados y la ubicación de sus oficinas (país y ciudad), explicar una incidencia de marcaje evaluada por la fuente, mostrar su evidencia y relacionar TimeOff cuando la fuente lo indique.
- Contratos de integración sustituibles para que un adaptador real pueda reemplazar la fuente sintética sin reescribir los casos de uso.
- La primera vertical de SPEC-002 conserva un fixture HCM sintético en memoria para host y pruebas. SPEC-004 está aprobada para planificar un perfil local opcional con una proyección técnica mínima y seed sintético en MySQL; aún no está implementado y no crea entidades ni módulos HCM completos.
- Documentación, trazabilidad SDD y ejecución local.

### Excluido

- Construir un HCM nuevo o módulos completos de empleados, jornadas, TimeCheck, TimeOff o Tasks.
- Gestionar en Talent Assistant el ciclo de vida de marcajes, vacaciones o tareas.
- Reproducir un motor de reglas de marcaje perteneciente al HCM.
- Conexión productiva con un HCM corporativo en el MVP.
- Uso de datos reales de empleados.
- Integración productiva con proveedores de identidad corporativa.
- Escrituras automáticas realizadas por IA.
- Despliegue productivo.
- Integración obligatoria con n8n o MCP.
- Implementación obligatoria de Google ADK.
- Arquitectura de microservicios.
- Sistema RAG productivo.
- Multiagente en la versión inicial.

## Evolución prevista

### Fase 1 — Aplicación funcional

Implementar casos de uso acotados que consulten contratos sintéticos del HCM y presenten resultados con evidencia. SPEC-002 está aprobada; SPEC-004 está aprobada para planificar un perfil local contenedorizado y una semilla sintética, pero aún no está implementada.

### Fase 2 — Asistente con LLM

Permitir preguntas en lenguaje natural sobre información de negocio mediante herramientas controladas. La respuesta deberá distinguir los datos recuperados de la interpretación generada.

### Fase 3 — RAG

Incorporar documentación sintética o pública de prueba, indexación, recuperación contextual y referencias a fuentes.

### Fase 4 — Herramientas e integraciones

Sustituir la fuente HCM sintética por un adaptador real solo tras aprobar proveedor, contrato, seguridad y autorización. Evaluar MCP, n8n u otras herramientas únicamente si un caso de uso lo justifica. Tasks y otras consultas quedan para especificaciones posteriores.

### Fase 5 — Agentes y patrón Supervisor

Evaluar si un supervisor y varios agentes especializados aportan mejoras respecto a una solución más sencilla.

Google ADK y un servicio Python son alternativas a validar, no requisitos confirmados.

## Condiciones para ampliar el alcance

Una fase solo comenzará cuando:

- La fase anterior tenga criterios de aceptación cumplidos.
- Exista un caso de uso concreto.
- Se identifiquen riesgos y dependencias.
- Se definan métricas de evaluación.
- La arquitectura propuesta esté documentada y aprobada.
