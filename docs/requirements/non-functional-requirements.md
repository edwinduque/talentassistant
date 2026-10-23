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
| NFR-017 | Reproducibilidad del entorno local | API, Web y la base de datos propia (PostgreSQL según ADR-0013/SPEC-013) podrán construirse y ejecutarse localmente mediante Docker Compose y Dockerfiles versionados; el conjunto incluirá una semilla sintética idempotente y documentación completa de configuración, ejecución y persistencia. |
| NFR-018 | Seguridad de credenciales | Las contraseñas se almacenarán solo como hash; las claves de firma y las credenciales de siembra se suministrarán exclusivamente por configuración externa; los fallos de autenticación devolverán respuestas genéricas y la API y la interfaz no registrarán secretos ni tokens. |
| NFR-019 | Localización | Las cadenas visibles vivirán en recursos de localización por componente con español como recurso neutral e inglés como satélite; el cambio de idioma se aplicará mediante cookie de cultura; no se detectará el idioma del navegador. |
| NFR-020 | Localización de datos | Los textos de presentación del dataset sintético existirán en español e inglés sobre un único payload versionado; los identificadores y valores temporales serán idénticos entre variantes; una versión de payload desconocida fallará de forma controlada. |
| NFR-021 | Proveedor externo de IA | Las claves del proveedor de IA vivirán solo en configuración externa al repositorio; las llamadas al proveedor tendrán tiempo de espera y la degradación ante fallos será controlada, sin que el resto de la aplicación dependa del proveedor. |
| NFR-022 | Seguridad de la base del HCM | El rol de la aplicación tendrá permisos de solo SELECT sobre las vistas del contrato, ninguna petición podrá escribir (transacción de solo lectura con *statement timeout*), las consultas serán fijas y parametrizadas sin SQL dinámico, TLS estará exigido para la conexión con la base real, las credenciales vivirán solo en configuración externa al repositorio y los logs no registrarán valores de personas. |
| NFR-023 | Consistencia visual | Las pantallas públicas y autenticadas de Web compartirán los tokens y patrones visuales definidos a partir de la landing de Talent Assistant, manteniendo localización, accesibilidad y comportamiento existentes. |

## Objetivos que quedan por definir

Antes de cerrar los requisitos no funcionales se deberán concretar:

- Tiempo de respuesta esperado para las consultas principales.
- Volumen de datos sintéticos.
- Navegadores y dispositivos objetivo.
- ~~Estrategia de autenticación~~: definida por NFR-018 y SPEC-005 (Identity local + JWT).
- Umbrales de calidad y latencia para IA.
- Objetivos de cobertura de pruebas, si se considera necesario establecer un porcentaje.
- Estrategia de despliegue y operación.

NFR-017 fue aprobado mediante SPEC-004 y ADR-0006 y está implementado y verificado: el stack Compose, la semilla idempotente y la guía local quedaron comprobados.

NFR-023 fue aprobado mediante SPEC-018; la consistencia visual se verifica sobre las páginas Web públicas y autenticadas sin cambiar sus flujos ni contratos.

No se asignan cifras arbitrarias hasta disponer de un escenario de referencia.

## Asignación de integración continua

NFR-009 se mantiene vigente para el MVP. Su implementación quedó asignada a SPEC-014 (canalización de CI).
