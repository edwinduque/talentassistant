# ADR-0002: Uso exclusivo de datos sintéticos en el MVP

- Estado: Aceptado
- Fecha: 2026-10-14
- Aclaración de alcance: 2026-10-15; se mantiene el uso de datos sintéticos y se precisa que representan contratos/respuestas del HCM.

## Contexto

El HCM existente es el sistema fuente de empleados, jornadas, marcajes, TimeOff, Tasks y reglas de negocio. Talent Assistant necesita desarrollar y evaluar sus casos de asistencia sin depender inicialmente de una conexión corporativa real. La redacción anterior, que decía que Talent Assistant simulaba funcionalidades de un HCM, invertía la propiedad de esos procesos y se aclara mediante esta decisión.

## Decisión

El MVP utilizará exclusivamente datos sintéticos que representen los contratos y respuestas que Talent Assistant consumiría del HCM. La fuente sintética simulará la integración, no un HCM completo ni sus flujos operativos. No se conectará a fuentes de empleados reales ni se incorporarán exportaciones de datos productivos.

Las reglas de marcaje pertenecen al HCM. La fuente sintética expondrá los resultados y la evidencia necesarios para los casos de uso; Talent Assistant no replicará un segundo motor de reglas. Los casos de uso persistirán únicamente las proyecciones o fixtures mínimos que requieran, sin convertir a Talent Assistant en sistema de registro.

Los datos de demostración se introducirán junto con la primera especificación funcional que defina entidades de negocio persistidas. La fundación técnica no inventará entidades ni datos de negocio solo para poblar una base vacía.

## Consecuencias positivas

- Menor riesgo de privacidad.
- Ejecución local reproducible.
- Pruebas controladas.
- Libertad para generar casos límite.
- Contratos sintéticos que permiten validar casos de asistencia antes de disponer de integración real.

## Consecuencias negativas

- Los datos no representan necesariamente toda la complejidad de un entorno corporativo real.
- Las integraciones reales requerirán validación posterior.

## Criterios de cumplimiento

- Cuando existan entidades de negocio persistidas, el repositorio incluye un mecanismo explícito para crear datos de demostración sintéticos.
- La inicialización puede repetirse sin duplicar registros y conserva las relaciones entre entidades.
- Mientras la fundación no tenga entidades de negocio, el aplazamiento de la inicialización queda documentado en SPEC-001; no se marca como un mecanismo ya implementado.
- La configuración no requiere credenciales de sistemas corporativos.
- Los datos de prueba no contienen información personal real.
