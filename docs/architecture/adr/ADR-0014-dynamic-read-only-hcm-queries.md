# ADR-0014 — Consultas dinámicas seguras de solo lectura sobre HCM

- **Estado:** Aceptado (2026-10-25).
- **Fecha:** 2026-10-25.
- **Aprobación de arquitectura:** aprobado explícitamente por el usuario el 2026-10-25 para continuar con la implementación.
- **Especificación relacionada:** [SPEC-020 — Consulta flexible y controlada de datos de RR. HH.](../../../specs/020-flexible-hr-data-query/spec.md).
- **Decisiones relacionadas:** modifica las restricciones de catálogo de ADR-0011 y de SQL fijo de ADR-0012; ADR-0003 permanece vigente.

## Contexto

SPEC-009 implementa un catálogo fijo de herramientas que cubre las consultas conocidas. SPEC-020 amplía el objetivo: el asistente debe combinar consultas sobre campos no sensibles que expone la fuente HCM, sin crear una herramienta por pregunta. El usuario confirmó que el asistente no debe modificar datos, que salario y número de cuenta quedan excluidos y que los demás datos disponibles pueden consultarse. Se conservan las exclusiones explícitas previas, incluido el año de nacimiento crudo.

ADR-0011 establece un catálogo cerrado de herramientas. ADR-0012 exige comandos SQL fijos y parametrizados y prohíbe por completo SQL dinámico. Ambas decisiones son más restrictivas que la consulta dinámica solicitada y no se pueden cambiar silenciosamente. El modelo tampoco puede conectarse directamente a la base: ADR-0003 y SPEC-010 requieren que los datos pasen por herramientas y autorización del backend.

## Decisión aceptada

Adoptar una capacidad genérica de consulta declarativa en el backend, expuesta al asistente como una sola herramienta (`query_hr_data`, nombre tentativo). La herramienta recibe un plan estructurado —nunca una cadena SQL ni código— y permite seleccionar campos no sensibles, combinar filtros y relaciones definidas por el contrato HCM, y aplicar operaciones de consulta como conteo, agrupación, ordenación, ranking y extremos.

La API valida el plan con metadatos y reglas controladas por la aplicación. Infrastructure compone consultas SQL dinámicas exclusivamente a partir de identificadores de tablas, campos y operadores resueltos en esos metadatos; todos los valores variables se envían como parámetros. Esta excepción acotada reemplaza la prohibición absoluta de SQL dinámico de ADR-0012: el SQL libre del usuario o del modelo continúa prohibido.

La autorización, la selección de fuente (`Sintética` o `Postgres`) y la identidad del usuario son responsabilidad del backend. La herramienta no expone escritura ni permite ampliar permisos. Se mantienen las operaciones especializadas que la fuente debe resolver con sus reglas —por ejemplo, `upcoming_birthday`—, pero no se crean operaciones para frases o preguntas nuevas.

## Invariantes de seguridad

1. **Solo lectura:** la gramática de consulta admite únicamente lectura y agregación. PostgreSQL conserva grants de `SELECT` sobre vistas autorizadas y transacciones `READ ONLY`; ninguna operación del plan puede insertar, actualizar, borrar, crear objetos ni cambiar permisos.
2. **SQL construido por el backend:** el modelo no envía SQL, identificadores SQL, fragmentos, expresiones ejecutables ni scripts. Los identificadores se resuelven contra el contrato de consultas conocido; los valores se parametrizan. Un campo u operador desconocido se rechaza.
3. **Datos sensibles:** salario y número de cuenta se excluyen del plan y del resultado. No pueden seleccionarse, filtrarse, agruparse, ordenarse, agregarse ni inferirse. También siguen excluidos los atributos protegidos por decisiones previas, incluido el año de nacimiento crudo.
4. **Alcance de datos:** la consulta opera solo sobre los conjuntos de datos y relaciones HCM definidos por el contrato de la aplicación. No inspecciona tablas físicas arbitrarias ni obtiene datos ausentes del contrato. Otros campos no sensibles disponibles en el contrato no requieren una herramienta o aprobación individual.
5. **Autorización:** la API aplica la sesión y los permisos vigentes antes de ejecutar. El historial, el contenido del usuario y la salida del modelo no alteran la autorización.
6. **Límites y confidencialidad:** plan máximo de 12 campos, 8 filtros, 3 campos de agrupación, 250 filas y valores de filtro de 256 caracteres; Infrastructure conserva el timeout configurado (15 segundos por defecto) y el asistente limita cada payload de resultados a 64 KiB. El sistema devuelve solo campos necesarios y no registra valores personales ni parámetros con datos de empleados.
7. **Egreso al proveedor:** se conserva la política de ADR-0011/ADR-0012; los resultados enviados al modelo siguen sujetos a la decisión de egreso de datos, que debe reabrirse antes del uso productivo con datos reales.

## Cambios a decisiones vigentes

- **ADR-0003:** sin cambio. El modelo pide una herramienta y nunca accede a la base o red directamente.
- **ADR-0011:** reemplazar el catálogo especializado fijo como única forma de consulta por un catálogo cerrado que incluya la herramienta genérica. Mantener las operaciones de dominio que la fuente resuelve y retirar rutas de consulta redundantes solo tras probar compatibilidad.
- **ADR-0012:** reemplazar «sin SQL dinámico jamás» por la excepción de compilación del plan validado descrita aquí. Se mantienen Npgsql, parámetros para valores, contrato de vistas, transacción `READ ONLY`, grants de solo lectura, TLS, timeout, logs sin datos personales, fuente única y sin fallback silencioso.
- **SPEC-010:** sin cambio de ámbito. Peticiones como generar código Python siguen rechazadas y no invocan herramientas; una consulta declarativa válida no habilita respuestas generales fuera de RR. HH./HCM.

## Consecuencias

### Positivas

- Preguntas nuevas de conteo, filtro, agrupación y ordenación no requieren un caso, prompt o herramienta por redacción.
- Los agregados se calculan en la fuente de consulta y pueden devolver al modelo solo el resultado necesario.
- Las garantías de autenticación, fuente única y solo lectura permanecen en el backend y en la base.

### Riesgos y costos

- Un compilador de consultas amplía la superficie de seguridad; la validación de identificadores, operadores, relaciones, límites y atributos sensibles es crítica.
- La nueva capacidad cambia contratos de Application, Infrastructure y del catálogo del asistente; debe implementarse de forma compatible con las preguntas existentes.
- La consulta de campos no sensibles adicionales puede ampliar la cantidad de información enviada al proveedor. Se mantiene la política de egreso existente y no se habilita automáticamente el uso productivo con personas reales.

## Alternativas consideradas

- **Conservar solo las seis herramientas especializadas:** mantiene el SQL sencillo, pero las preguntas no previstas siguen dependiendo de la cobertura de cada herramienta y de datos devueltos para agregación. No satisface SPEC-020.
- **Permitir SQL generado por el LLM o conexión directa del modelo:** rechazado. Permitiría manipular tablas, permisos, fuentes o atributos y contradiría ADR-0003, ADR-0012 y SPEC-010.
- **Herramienta declarativa validada y SQL parametrizado generado por el backend:** adoptada y aprobada. Aporta flexibilidad sin transferir al modelo la autoridad de construir o ejecutar SQL arbitrario; requiere las garantías y pruebas de esta decisión.

## Verificación requerida para declarar implementada la decisión

- Probar que consultas válidas sobre ambas fuentes producen resultados equivalentes para filtros y agregados cubiertos.
- Rechazar campos sensibles en selección, filtro, agrupación, ordenación y agregación, y verificar que ningún resultado o log los contiene.
- Rechazar nombres de campo, relaciones, operadores y estructuras no admitidas, así como cualquier intento de escritura o SQL/código libre.
- Demostrar que consultas rechazadas no cambian datos y que las válidas usan la sesión, permisos, fuente y límites configurados.
- Conservar las regresiones de SPEC-008, SPEC-009 y SPEC-010, incluidos cumpleaños, conteos, búsqueda de nombres y rechazo de solicitudes de Python.

## Aprobación y vigencia

El usuario aprobó explícitamente ADR-0014 el 2026-10-25. La decisión quedó implementada en `f9592f4`, sujeta a los invariantes, pruebas negativas y límites definidos en este ADR y SPEC-020; no autoriza SQL libre, acceso directo del modelo a la base ni escritura de datos.
