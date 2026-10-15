# Requisitos no funcionales

| ID | Categoría | Requisito |
|---|---|---|
| NFR-001 | Arquitectura | El dominio no dependerá de tecnologías de presentación, persistencia o IA. |
| NFR-002 | Mantenibilidad e interoperabilidad | Los casos de uso dependerán de contratos explícitos; el adaptador HCM sintético podrá sustituirse por otro proveedor sin reescribir Application. |
| NFR-003 | Calidad | Las reglas de negocio críticas tendrán pruebas automatizadas. |
| NFR-004 | Seguridad | No se incluirán secretos ni credenciales en el repositorio. |
| NFR-005 | Privacidad | El MVP utilizará exclusivamente datos sintéticos que representen contratos y respuestas HCM; no incluirá datos reales ni requerirá una conexión productiva. |
| NFR-006 | Validación | La API validará sus entradas y devolverá errores controlados. |
| NFR-007 | Observabilidad | Los errores relevantes podrán diagnosticarse mediante logs estructurados sin registrar información sensible. |
| NFR-008 | Reproducibilidad | La configuración local y los datos de prueba estarán documentados. |
| NFR-009 | Integración continua | La canalización de CI ejecutará compilación y pruebas automatizadas. |
| NFR-010 | Interoperabilidad | El frontend consumirá contratos de API documentados. |
| NFR-011 | Rendimiento | Los objetivos de latencia se establecerán tras definir dataset, consultas y entorno de ejecución. |
| NFR-012 | Accesibilidad | La interfaz deberá considerar navegación por teclado, etiquetas y contraste adecuados. |
| NFR-013 | Resiliencia de IA | Las futuras llamadas a modelos tendrán límites de tiempo, tratamiento de errores y límites de consumo. |
| NFR-014 | Seguridad de IA | Ningún modelo podrá decidir por sí solo qué datos o herramientas tiene permitido utilizar. |
| NFR-015 | Trazabilidad | Los requisitos aprobados tendrán correspondencia con criterios de aceptación y pruebas. |
| NFR-016 | Evaluación | Las futuras respuestas de IA se evaluarán con casos reproducibles, no solo mediante revisión anecdótica. |

## Objetivos que quedan por definir

Antes de cerrar los requisitos no funcionales se deberán concretar:

- Tiempo de respuesta esperado para las consultas principales.
- Volumen de datos sintéticos.
- Navegadores y dispositivos objetivo.
- Estrategia de autenticación para la versión demostrable.
- Umbrales de calidad y latencia para IA.
- Objetivos de cobertura de pruebas, si se considera necesario establecer un porcentaje.
- Estrategia de despliegue y operación.

No se asignan cifras arbitrarias hasta disponer de un escenario de referencia.

## Asignación de integración continua

NFR-009 se mantiene vigente para el MVP. Su implementación no pertenece a SPEC-001 y debe asignarse a una especificación posterior de CI antes de aceptar el MVP. El ID de esa especificación aún no está asignado; el requisito no se considera cumplido por compilaciones o pruebas locales.
