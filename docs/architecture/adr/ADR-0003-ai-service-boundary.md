# ADR-0003: Límite arquitectónico para capacidades de IA

- Estado: Aceptado
- Fecha: 2026-10-14

## Contexto

El producto podría incorporar LLM, RAG, herramientas externas y agentes. Acoplar el dominio a un proveedor o framework de IA dificultaría su mantenimiento y evaluación.

## Decisión

Mantener los casos de uso de negocio independientes de los proveedores de IA. Introducir capacidades de IA a través de contratos explícitos y autorizados.

Un servicio Python separado se incorporará solo si los requisitos del orquestador o framework elegido lo justifican.

## Alternativas

1. Integrar directamente un SDK de IA en el dominio.
2. Mantener interfaces de aplicación y adaptadores de infraestructura.
3. Crear un servicio de IA independiente desde el inicio.

## Consecuencias positivas

- Mayor facilidad para sustituir proveedores.
- Pruebas más sencillas.
- Límites claros de seguridad.
- Posibilidad de evaluar diferentes estrategias de orquestación.

## Consecuencias negativas

- Requiere definir contratos adicionales.
- Una abstracción excesiva puede aumentar complejidad si solo existe una integración trivial.

## Restricciones

- La IA no tendrá acceso SQL arbitrario.
- El backend conservará la autorización.
- Las operaciones de escritura deberán pasar por casos de uso explícitos.
- La salida del modelo no se considerará automáticamente verdadera ni autorizada.
