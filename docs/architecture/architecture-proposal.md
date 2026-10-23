# Propuesta de arquitectura

## 1. Objetivo

Desarrollar una aplicación web mantenible que ayude a responsables de RR. HH. a consultar, relacionar e interpretar información de un HCM existente. El HCM conserva los datos y reglas operativas; Talent Assistant presenta asistencia verificable y podrá incorporar IA después sin acoplar el dominio a proveedores de modelos.

## 2. Estilo arquitectónico aprobado

Monolito modular con Clean Architecture, de acuerdo con la línea base confirmada y ADR-0001.

El MVP no necesita microservicios: todavía no existen requisitos demostrados de escalabilidad independiente, despliegue separado o aislamiento operativo que justifiquen su coste.

## 3. Componentes

### Frontend

Blazor Web App, aprobada en la línea base tecnológica del registro de decisiones. Para la fundación se utilizará el modo `InteractiveServer`.

Responsabilidades:

- Presentar explicaciones, resultados y evidencia de casos de asistencia sobre datos del HCM.
- Gestionar filtros e interacción.
- Consumir contratos HTTP.
- Presentar errores de forma controlada.
- Mostrar respuestas y fuentes del asistente en futuras versiones.

En `InteractiveServer`, los componentes interactivos y su estado se mantienen en el servidor mediante circuitos SignalR. Se selecciona para simplificar la fundación local y evitar un cliente WebAssembly que llame directamente a la API; el alcance no define objetivos de concurrencia ni permite concluir que la modalidad cubra carga de producción. Cada circuito consume recursos del servidor y depende de conectividad persistente; la fundación no incluye escalado horizontal ni despliegue de producción.

El componente Web llamará a ASP.NET Core Web API desde el servidor mediante un cliente HTTP tipado registrado con `IHttpClientFactory`. La dirección se obtiene de `BackendApi:BaseUrl` y se puede sobrescribir por entorno con `BackendApi__BaseUrl`; no se codifican URLs en componentes. Los errores HTTP, de red y de tiempo de espera se traducen a estados controlados de interfaz; los logs no incluyen credenciales ni cadenas de conexión.

Esta comunicación servidor-servidor no requiere CORS. CORS solo se configurará si una especificación posterior introduce llamadas directas desde el navegador, por ejemplo, con un modo WebAssembly. La autenticación queda fuera de SPEC-001: API y Web se ejecutan localmente con datos sintéticos y no deben exponerse a una red compartida.

### Backend API

ASP.NET Core Web API, aprobada en la línea base tecnológica del registro de decisiones.

Responsabilidades:

- Exponer endpoints.
- Validar solicitudes.
- Aplicar autorización cuando los requisitos aprobados la incluyan; SPEC-001 la excluye y limita la ejecución a desarrollo local con datos sintéticos.
- Invocar los casos de uso de Application; la API no contiene reglas de negocio.
- Coordinar acceso a datos mediante interfaces.
- Gestionar errores y observabilidad.

### Domain

Responsabilidades:

- Entidades y objetos de valor.
- Reglas e invariantes de negocio.
- Estados y transiciones.
- Comportamientos independientes de frameworks externos.

El dominio modelará únicamente conceptos propios de los casos de asistencia. No será dueño de empleados, jornadas, TimeCheck, TimeOff o Tasks ni replicará las reglas del HCM. No dependerá de la base de datos, la API, el frontend, la implementación concreta del HCM ni los SDK de IA.

### Application

Responsabilidades:

- Casos de uso.
- Consultas y comandos.
- CQRS ligero mediante Commands y Queries explícitos, sin MediatR ni bus de mensajes en la fundación técnica inicial.
- Validación de casos de uso.
- Contratos de lectura para los datos y resultados que expone el HCM.
- Casos de uso de asistencia que relacionan fuentes y preparan explicaciones con evidencia.
- No evaluará de nuevo reglas de marcaje pertenecientes al HCM.

### Infrastructure

Responsabilidades:

- Persistencia.
- Implementaciones de repositorios.
- Implementación de los contratos de persistencia definidos hacia dentro.
- Configuración y uso de DbContext y EF Core.
- Integraciones externas.
- Adaptadores concretos para la fuente HCM sintética y, en una fase posterior aprobada, para un HCM real.
- Servicios técnicos.
- Configuración de proveedores concretos.

### Integración HCM

El HCM existente permanece fuera de Talent Assistant y es el sistema de registro para empleados, jornadas, marcajes, TimeOff, Tasks y reglas de negocio. En el MVP no habrá conexión corporativa real: un adaptador sintético devolverá datos, evaluaciones de reglas y evidencia con los mismos contratos que consumirían los casos de uso.

Para la primera vertical aprobada en SPEC-002, Application define un puerto de lectura conceptual `IHcmAttendanceSource`; Infrastructure aporta `SyntheticHcmAttendanceSource`. El puerto devolverá un contexto limitado al caso de uso: referencias sintéticas de empleados y su asociación con oficinas, país, ciudad e identificador de zona horaria IANA por oficina, jornadas, marcajes, resultados de reglas del HCM, evidencia/procedencia y TimeOff relacionado cuando aplique. La cardinalidad de la asociación empleado-oficina y los tipos/códigos canónicos de país quedan pendientes; los valores de demostración serán sintéticos.

La fuente debe devolver la evaluación del HCM (referencia/versión de regla, resultado, evidencia e indicaciones de revisión). Talent Assistant puede resumir y relacionar esos datos, pero no determinar por sí misma si un marcaje incumple una regla HCM. Los filtros de fecha usan `from` inclusivo y `to` exclusivo por fecha local de la oficina; se convierten usando la zona IANA recibida, sin inferirla del país o la ciudad. No se define todavía transporte, proveedor, autenticación ni contrato productivo.

### Reglas de dependencias

- Domain no depende de Application, Infrastructure, API, Web, EF Core ni otros frameworks de persistencia.
- Application depende de Domain y define los contratos que necesitan sus casos de uso; no accede directamente a DbContext ni depende de Infrastructure.
- Application define puertos de lectura HCM y depende de modelos de transporte internos, no de SDK, API ni esquema de un proveedor concreto.
- Infrastructure implementa los contratos de Application, contiene el adaptador sintético, y concentra DbContext, EF Core y los detalles del proveedor de la persistencia (PostgreSQL, ADR-0013) cuando se requiera persistencia local.
- API invoca casos de uso de Application. El punto de composición registra las implementaciones de Infrastructure; la API no contiene reglas de negocio.
- Web se comunica con API mediante HTTP y contratos; no referencia Infrastructure ni accede directamente a DbContext o a PostgreSQL.
- Las pruebas unitarias de Domain y Application pueden ejecutarse sin base de datos. Las pruebas que necesitan EF Core y PostgreSQL son pruebas de integración.

### Servicio de IA futuro

Si las capacidades de agentes justifican un servicio independiente, se evaluará un componente Python que utilice Google ADK u otra alternativa.

El servicio de IA no tendrá acceso irrestricto a la base de datos. Deberá invocar herramientas o contratos de negocio expuestos por el backend y sujetos a autorización.

## 4. Diagrama lógico

    Usuario
       |
       v
    Frontend web
       |
       v
    ASP.NET Core API
       |
       v
    Application -----> Domain
       |
       v
    Interfaces de infraestructura
       |
       v
    Persistencia

    Evolución opcional:

    API de negocio
       |
       v
    Orquestador / servicio de IA
       |                 |
       v                 v
    Herramientas      Recuperación RAG
    autorizadas       y fuentes documentales

El diagrama representa límites lógicos; no implica que cada bloque deba desplegarse como proceso independiente.

La fuente HCM real es externa. Durante el MVP, el adaptador sintético de Infrastructure representa ese límite; la persistencia local, si se usa para fixtures, no convierte los datos en propiedad de Talent Assistant.

### Frontera de integración HCM

    Caso de uso de asistencia (Application)
                  |
                  v
        IHcmAttendanceSource
             /          \
            v            v
    Adaptador sintético   Adaptador real (futuro)
            |            |
      fixtures HCM       HCM existente

Los dos adaptadores implementan el mismo puerto. El contrato no expone el esquema interno de un proveedor ni traslada al dominio las reglas de marcaje.

## 5. Flujo de una consulta convencional

1. El usuario aplica filtros en la interfaz.
2. El frontend solicita información a la API.
3. La API valida los parámetros.
4. Application ejecuta el caso de uso y consulta el puerto HCM.
5. La fuente devuelve datos, resultado de reglas del HCM, evidencia y procedencia.
6. Application relaciona la información pertinente y prepara una explicación sin recalcular las reglas del HCM.
7. La API devuelve una respuesta contractual.
8. El frontend presenta el resultado y sus evidencias.

La primera vertical aprobada muestra TimeOff relacionado solo cuando la fuente lo indica como pertinente. Tasks no es necesario para esta especificación y queda reservado para un caso posterior.

## 6. Flujo futuro con IA

1. El usuario envía una pregunta.
2. El backend establece el contexto autorizado.
3. El orquestador determina si se requiere una herramienta, conocimiento documental o ambos.
4. Las herramientas consultan datos mediante contratos de negocio.
5. RAG recupera fragmentos relevantes de fuentes permitidas.
6. El modelo redacta una respuesta con evidencia disponible.
7. El backend valida la respuesta, aplica políticas y registra la información operativa necesaria.
8. La interfaz muestra el resultado y sus fuentes cuando corresponda.

La autorización se aplica en el backend en cada operación; no se delega al prompt ni al criterio del modelo.

## 7. Persistencia

PostgreSQL y Entity Framework Core están aprobados por ADR-0013 (que sustituyó a ADR-0004/MySQL el 2026-10-21). La selección vigente es EF Core `10.0.13` con el proveedor `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3` y PostgreSQL Server 17 en desarrollo/pruebas. La combinación está respaldada por los metadatos estables de NuGet; la migración regenerada y las operaciones contra el servidor exacto quedaron verificadas por la suite de integración (SPEC-013).

En Windows, la aplicación .NET se ejecuta en el equipo y se conecta a `127.0.0.1:5434`, publicado por el servicio `app-postgres` (PostgreSQL 17) de `compose.yaml`; el conector HCM sostiene su propio servicio `hcm-postgres` en `127.0.0.1:5433`. Los servicios de datos se ejecutan en Docker. La gestión local de secretos, el volumen persistente y el ciclo de vida se especifican en SPEC-013. No se selecciona una versión de servidor para producción.

La fuente sintética no convierte fixtures locales en el sistema de registro de empleados ni de TimeCheck. SPEC-002 usa un fixture determinista en memoria en Infrastructure; no agrega tablas HCM, migraciones ni cambia el uso aprobado de PostgreSQL para otros casos.

## 8. Estrategia de pruebas

- Pruebas unitarias del dominio y de Application con xUnit, ejecutables sin base de datos ni servicios externos.
- Pruebas de integración con un servidor PostgreSQL real 17 para conexión, operaciones de persistencia y migraciones.
- Pruebas HTTP para el endpoint de salud y la llamada Web → API.
- Pruebas de contrato entre frontend y API cuando aporten valor.
- Pruebas de aceptación vinculadas con requisitos.
- En fases de IA, evaluación de respuestas, recuperación documental y uso de herramientas.

## 9. Evolución tecnológica

No se incorporarán ADK, MCP, n8n, un vector store ni un servicio Python al MVP por defecto.

Cada componente deberá responder a un caso de uso y tener un criterio verificable de utilidad.

## 10. Decisiones y detalles fuera de SPEC-001

- Estrategia de autenticación.
- Canalización de CI y estrategia de despliegue; NFR-009 y AC-009 permanecen vigentes y se asignarán a una especificación posterior.
- Proveedor de LLM.
- Modelo de embeddings y almacenamiento vectorial.
- Framework de agentes.
- Necesidad de MCP o n8n.
