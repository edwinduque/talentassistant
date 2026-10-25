# SPEC-021 — Nuevo chat y presentación de respuestas del asistente

- **Estado:** Aprobada e implementada (2026-10-25) en `feat/assistant-chat-refresh`; commits `49a9dec` y `9fab71b`; sin push ni PR.
- **Fecha:** 2026-10-25.
- **Requisitos relacionados:** FR-029; NFR-019, NFR-023.
- **Criterios relacionados:** AC-060, AC-061.
- **Decisiones arquitectónicas:** ninguna nueva; extiende la interfaz de SPEC-009 sobre los tokens y patrones de SPEC-018.
- **Decisión de producto registrada:** el usuario pidió un botón para iniciar una conversación nueva y preguntó si las respuestas podían verse con formato o devolverse en texto plano; el 2026-10-25 eligió «Renderizar markdown seguro».
- **Versión:** 0.1.

## 1. Objetivo

Dar al usuario una forma deliberada de iniciar una conversación nueva en `/assistant`, sin recargar la página ni persistir nada, y presentar las respuestas del asistente con un formato legible y seguro que no muestre los marcadores markdown en crudo ni interprete el texto del modelo como HTML.

La conversación sigue viviendo solo en el cliente y se pierde al recargar (SPEC-009 §8). Esta especificación no cambia el contrato de `POST /chat`, el catálogo de herramientas, la autenticación ni los guardarraíles de SPEC-009/SPEC-010.

## 2. Problema y contexto actual

La página `/assistant` pinta cada respuesta del asistente como texto plano con `white-space: pre-wrap` y no procesa su contenido. El system prompt no restringe el formato, por lo que el proveedor devuelve negritas y listas con sintaxis markdown que el usuario ve como `**texto**`, guiones y encabezados sin formato. Al mismo tiempo, el historial es propiedad del cliente y no existe una acción para iniciar una conversación nueva sin recargar toda la página.

### Solicitud literal

```text
Ayudame con un nuevo requerimiento, me gustaría que el asistente tuviera en alguna parte un boton para crear un nuevo chat, aunque no se guarde pues como para limpiar todo, por otro lado tambien veo que el chat responde como en un formato md, es posible que cuando se muesttra se viera bonito y no con *** o mejor lo retornamos plano
```

Ante la alternativa de devolver texto plano, el usuario eligió renderizar un subconjunto seguro de markdown.

## 3. Alcance aprobado

### Incluido

- Botón «Nuevo chat» (EN «New chat») localizado en la cabecera de `/assistant`. Limpia en memoria la lista de mensajes y el borrador del campo de texto; no guarda, no recarga la página y no llama al backend. Se deshabilita cuando no hay nada que limpiar y mientras hay una respuesta en curso.
- Renderizado en la Web de un subconjunto de markdown como componentes Blazor: párrafos, saltos de línea simples, encabezados `#`/`##`/`###`, listas con viñetas, listas numeradas, negrita, cursiva y código inline.
- Emisión del texto del modelo como texto codificado. Ningún contenido del modelo se interpreta como HTML.
- Regla de formato en el system prompt: párrafos cortos, listas con `-` o numeradas para varios registros y negrita solo para etiquetas o valores clave; sin tablas, enlaces, imágenes, HTML ni bloques de código cercados.
- Pruebas deterministas del formateador y del componente renderizado, pin del prompt y documentación.

### Fuera de alcance

- Persistencia de conversaciones, historial en el servidor, múltiples hilos o un selector de conversaciones; recuperar la conversación tras recargar.
- Markdown completo: enlaces, imágenes, tablas, HTML, bloques de código cercados, listas anidadas, escapes con `\` y énfasis con `_`/`__`.
- Streaming de respuestas, cambios al contrato de `POST /chat`, al catálogo de herramientas, a la autenticación o a los guardarraíles.
- Nuevas dependencias de renderizado o cambios de proveedor.
- Que el formato de la respuesta altere los datos: la presentación no puede crear, combinar ni inferir valores.

## 4. Comportamiento esperado

| Situación | Resultado esperado |
|---|---|
| La página abre sin conversación | El botón «Nuevo chat» está deshabilitado y se muestra el estado vacío existente. |
| Hay mensajes o borrador | El botón se habilita; al activarlo desaparecen los mensajes y el borrador, vuelve el estado vacío y el botón se deshabilita. La página no se recarga y no se hace ninguna llamada al backend. |
| Hay una respuesta en curso | El botón permanece deshabilitado y el manejador no limpia el hilo; la respuesta pendiente aterriza en la conversación actual. |
| La respuesta contiene `**negrita**`, listas o encabezados | Se presentan con negrita, listas y encabezados reales; el usuario no ve los marcadores. |
| La respuesta contiene `<script>` u otro HTML | Se muestra como texto literal codificado, sin ejecutarse ni interpretarse. |
| La respuesta contiene enlaces, tablas o bloques de código cercados | Permanecen literales; no se convierten en controles ni contenido activo. |
| El usuario envía un mensaje, un error o se muestran fuentes | Siguen en texto plano. |

## 5. Requisitos y límites

1. El botón limpia solo estado en memoria de esa conversación. El historial sigue siendo del cliente y se pierde al recargar (SPEC-009 §8); no se añade persistencia.
2. Mientras hay una solicitud en curso, el botón está deshabilitado y el manejador ignora la acción; una respuesta pendiente nunca aparece en una conversación nueva.
3. La Web renderiza exclusivamente el subconjunto aprobado. La sintaxis no reconocida o con marcadores sin cerrar permanece literal; no se oculta ni se pierde texto.
4. El renderizado no usa `MarkupString` ni ninguna API que interprete el texto como HTML. Todo el contenido del modelo se emite como texto codificado por Blazor.
5. Los mensajes del usuario, los avisos de error y la línea de fuentes siguen en texto plano.
6. El system prompt pide el formato del subconjunto y prohíbe las estructuras que el renderizador no admite. La instrucción no relaja los guardarraíles de ámbito ni de datos restringidos.
7. Los textos nuevos están localizados en español (recurso neutral) e inglés (recurso satélite), conforme a NFR-019.
8. No cambian el contrato de `POST /chat`, el catálogo cerrado, la autorización, los límites del proveedor ni el comportamiento de degradación.
9. La presentación conserva la estructura accesible: el hilo mantiene `aria-live`, los encabezados de respuesta usan niveles `h2`–`h4` bajo el `h1` de la página y las listas usan elementos semánticos.

## 6. Diseño aprobado

La decisión es de presentación y vive en la Web. Un formateador puro convierte el texto en un modelo de bloques y spans; dos componentes Blazor lo renderizan con elementos semánticos y codificación automática. No se añaden paquetes ni se renderiza HTML del modelo. El botón se coloca en la cabecera de la página y reutiliza los tokens visuales de SPEC-018.

El system prompt incorpora una regla de formato para que el modelo produzca el subconjunto con mayor frecuencia, sin que el renderizador dependa de que la cumpla: lo no soportado queda literal.

## 7. Criterios de aceptación aprobados

| ID | Criterio verificable aprobado | Evidencia requerida |
|---|---|---|
| AC-060 | `/assistant` ofrece un botón «Nuevo chat»/«New chat» localizado que limpia en memoria los mensajes y el borrador sin recargar ni llamar al backend. Está deshabilitado sin conversación y durante una respuesta en curso; una respuesta pendiente no aterriza en una conversación nueva. | Pruebas deterministas de la lógica de estado y recorrido de navegador con conversación y con solicitud en curso. |
| AC-061 | Las respuestas se presentan con el subconjunto seguro (párrafos, saltos, encabezados, listas, negrita, cursiva y código inline) y sin marcadores visibles. Ningún HTML del modelo se interpreta: se emite codificado. La sintaxis no reconocida o sin cerrar permanece literal. Los mensajes del usuario, los avisos y las fuentes siguen en texto plano. | Pruebas del formateador y del componente real renderizado, incluida la codificación de etiquetas y scripts, más inspección del HTML producido. |

## 8. Decisiones y parámetros de implementación

- Subconjunto soportado: párrafos, saltos de línea simples dentro de un párrafo, encabezados `#` a `###`, viñetas `-`, `*`, `+`, listas numeradas `1.`/`1)`, negrita `**texto**`, cursiva `*texto*` y código inline `` `texto` ``.
- No soportado y deliberadamente literal: HTML, enlaces, imágenes, tablas, bloques cercados, listas anidadas, escapes con `\`, énfasis con `_`/`__` y marcadores sin cerrar.
- Los encabezados del subconjunto se mapean a `h2`–`h4` para conservar la jerarquía de la página.
- El botón se ubica en la cabecera y usa el patrón de acción secundaria de los tokens de SPEC-018.
- Cualquier ampliación del subconjunto, persistencia de conversaciones o cambio del contrato requiere una nueva aprobación de alcance.

## 9. Plan SDD propuesto

1. T1: formateador, componentes de renderizado, estilos y regla de formato del prompt, con pruebas deterministas.
2. T2: botón «Nuevo chat», localización y guardia de respuesta en curso.
3. T3: documentación, criterios y verificación final.

## 10. Estado y aprobación

El usuario aprobó el comportamiento el 2026-10-25 y eligió renderizar markdown seguro frente a devolver texto plano. La implementación quedó en los commits `49a9dec` (renderizado seguro) y `9fab71b` (botón, localización y documentación) de `feat/assistant-chat-refresh`.

Evidencia observada: build con 0 errores y 0 avisos; suite completa 24/24 unitarias y 231/231 de integración; 19 casos del formateador, 2 del componente real renderizado (incluida la codificación de HTML) y 1 pin del prompt; recorrido de navegador con una respuesta formateada, sin `**` visibles, y con el botón limpiando mensajes y borrador. La pasada en vivo contra Groq devolvió 429 (límite de tasa) y quedó pendiente; la presentación se verificó con un stub local compatible con OpenAI. El detalle y las limitaciones están en `odd/tasks/assistant-chat-refresh.md`.
