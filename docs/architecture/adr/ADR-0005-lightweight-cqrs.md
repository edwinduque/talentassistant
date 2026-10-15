# ADR-0005: CQRS ligero sin mediator

- Estado: Aceptado
- Fecha: 2026-10-14

## Contexto

Talent Assistant requiere separar la recuperación de información de las operaciones que modifican el estado. La aplicación seguirá Clean Architecture y comenzará con un conjunto limitado de casos de uso.

## Decisión

Adoptar CQRS ligero desde el inicio.

Las consultas y los comandos se implementarán como casos de uso explícitos dentro de Application. No se incorporará MediatR ni un bus de mensajes en el MVP inicial.

## Alternativas consideradas

### Alternativa A: Servicios de aplicación sin separación explícita

Menor complejidad inicial, pero puede mezclar consultas y operaciones de escritura a medida que crezcan los módulos.

### Alternativa B: CQRS ligero

Distingue las intenciones de lectura y escritura sin imponer infraestructura adicional.

### Alternativa C: CQRS con mediator

Introduce despacho de solicitudes, pipelines y convenciones adicionales. Podría resultar útil si el número de casos de uso y las necesidades transversales lo justifican.

### Alternativa D: CQRS distribuido con mensajería

Aporta posibilidades de escalabilidad y procesamiento asíncrono, pero resulta excesivo para el MVP actual.

## Consecuencias positivas

- Casos de uso explícitos y fáciles de probar.
- Separación clara entre lectura y escritura.
- Menor dependencia de librerías.
- Posibilidad de incorporar validación y autorización de manera explícita.

## Consecuencias negativas

- Puede existir código repetitivo en los casos de uso.
- Si aumenta considerablemente la complejidad, deberá revisarse la conveniencia de un mediator.

## Restricciones

- No introducir abstracciones genéricas sin necesidad.
- Las consultas no deben modificar el estado.
- Los comandos deben respetar las invariantes del dominio.
- No asumir que CQRS requiere bases de datos separadas.

## Criterio de revisión

Revisar esta decisión cuando los casos de uso, los pipelines transversales o las necesidades de coordinación justifiquen una librería adicional.