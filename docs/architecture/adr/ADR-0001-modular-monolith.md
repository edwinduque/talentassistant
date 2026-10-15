# ADR-0001: Monolito modular para el MVP

- Estado: Aceptado
- Fecha: 2026-10-14

## Contexto

El proyecto requiere frontend, API, lógica de negocio y persistencia, pero todavía no tiene necesidades demostradas que justifiquen múltiples servicios desplegables.

## Decisión

Organizar la solución como un monolito modular con separación de responsabilidades inspirada en Clean Architecture.

## Alternativas

1. Monolito modular.
2. Microservicios desde el inicio.
3. Backend mínimo sin límites arquitectónicos explícitos.

## Consecuencias positivas

- Menor complejidad operativa.
- Depuración y pruebas más sencillas.
- Menor coste inicial.
- Posibilidad de extraer servicios cuando exista una razón concreta.

## Consecuencias negativas

- Exige disciplina para mantener límites entre módulos.
- No ofrece despliegue independiente por módulo.

## Criterio de revisión

Revisar la decisión cuando exista una necesidad demostrable de escalabilidad, aislamiento, despliegue independiente o autonomía de equipos.
