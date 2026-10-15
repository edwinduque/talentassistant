# Flujo de Specification-Driven Development

## Objetivo

Usar especificaciones versionadas como fuente de verdad para planificar, implementar y verificar el producto. La IA podrá acelerar el proceso, pero no sustituirá la aprobación de requisitos ni la revisión de resultados.

## Etapas

### 1. Intake

Registrar la necesidad, el problema que resuelve, los usuarios afectados y el resultado esperado.

Salida: propuesta de cambio.

### 2. Requirements

Definir requisitos funcionales y no funcionales, supuestos, restricciones y criterios de aceptación.

Salida: especificación revisable.

### 3. Design

Describir los componentes afectados, contratos, flujos de datos, errores y consideraciones de seguridad.

Salida: diseño técnico.

### 4. Architecture decisions

Identificar decisiones relevantes y registrar ADR cuando corresponda.

Salida: decisiones aprobadas o pendientes explícitamente identificadas.

### 5. Planning

Descomponer el cambio en tareas pequeñas, ordenadas y verificables.

Cada tarea debe indicar:

- Objetivo.
- Archivos o componentes previsiblemente afectados.
- Dependencias.
- Criterios de aceptación.
- Pruebas necesarias.

### 6. Implementation

Implementar exclusivamente el alcance aprobado.

Los agentes pueden explorar, proponer alternativas, programar y ejecutar pruebas. No deben convertir silenciosamente una propuesta en requisito aprobado.

### 7. Verification

Comprobar:

- Cumplimiento de criterios de aceptación.
- Pruebas unitarias y de integración pertinentes.
- Compatibilidad con la arquitectura.
- Manejo de errores.
- Seguridad y exposición de datos.
- Actualización de la documentación.

### 8. Human review

Revisar el cambio, sus pruebas, sus riesgos y las decisiones tomadas.

### 9. Closeout

Actualizar el estado de la especificación y registrar:

- Archivos modificados.
- Pruebas ejecutadas y resultados.
- Limitaciones conocidas.
- Deuda técnica.
- Requisitos satisfechos o pendientes.

## Definition of Ready

Una tarea está lista cuando:

- El problema está definido.
- El alcance es comprensible.
- Existen criterios de aceptación verificables.
- Las dependencias están identificadas.
- Las decisiones necesarias están aprobadas o claramente señaladas.

## Definition of Done

Una tarea está terminada cuando:

- Se cumple el alcance aprobado.
- Las pruebas pertinentes pasan, o se documentan las limitaciones.
- No se introducen dependencias o decisiones no aprobadas.
- La documentación afectada está actualizada.
- La implementación puede revisarse con trazabilidad hacia sus requisitos.

## Uso de agentes

Un flujo inicial puede emplear agentes para:

- Explorar el repositorio.
- Revisar requisitos y ambigüedades.
- Proponer diseño.
- Implementar tareas aprobadas.
- Verificar pruebas y criterios de aceptación.

La separación en agentes especializados es una estrategia de trabajo, no una obligación arquitectónica del producto.

## Métricas de proceso

Registrar progresivamente:

- Requisitos aprobados frente a implementados.
- Criterios de aceptación superados.
- Defectos detectados en revisión o pruebas.
- Retrabajo por especificaciones ambiguas.
- Tiempo de ciclo por tarea.
- En fases de IA: latencia, coste, calidad, uso de herramientas y errores de recuperación.

No optimizar métricas aisladas a costa de la calidad del producto.