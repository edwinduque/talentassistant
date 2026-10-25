# Feature: nuevo chat y presentación de respuestas

Objetivo: añadir un botón «Nuevo chat» que limpie la conversación local en `/assistant` y presentar las respuestas del asistente con un subconjunto seguro de markdown renderizado como componentes.
Rama: `feat/assistant-chat-refresh`.
Entrega: un solo PR: dos commits de feature y un commit documental con SPEC-021 (decisión del usuario, 2026-10-25); push y PR los decide el usuario.
Runner: `dotnet test` (UnitTests e IntegrationTests); la integración usa PostgreSQL local (`docker compose`).

## Specs

S1. La página `/assistant` muestra un botón «Nuevo chat» (EN «New chat») en la cabecera. Al activarlo limpia la lista de mensajes y el borrador del campo de texto de esa conversación. No guarda, no recarga la página y no llama al backend.

S2. El botón se deshabilita mientras hay una respuesta en curso y el manejador no limpia nada en ese estado, de modo que una respuesta pendiente nunca aparece en la conversación nueva.

S3. Las respuestas del asistente se presentan con un subconjunto de markdown renderizado como componentes Blazor: párrafos, saltos de línea simples, encabezados `#`, `##` y `###`, listas con viñetas (`-`, `*`, `+`), listas numeradas (`1.`, `1)`), negrita `**texto**`, cursiva `*texto*` y código inline `` `texto` ``.

S4. El renderizado nunca emite HTML crudo: no se usa `MarkupString` ni ninguna API que interprete el texto del modelo como HTML. Cualquier etiqueta, atributo o script que devuelva el modelo se muestra como texto codificado.

S5. La sintaxis no reconocida o con marcadores sin cerrar permanece literal; no se oculta ni se pierde texto. Deliberadamente no se soportan HTML crudo, enlaces, imágenes, tablas, bloques de código cercados, escape con `\` ni énfasis con `__`/`_` (colisionan con identificadores).

S6. El system prompt pide un formato apto para chat: párrafos cortos, listas con `-` o numeradas para varios registros y negrita solo para etiquetas o valores clave; sin tablas, enlaces, imágenes, HTML ni bloques de código cercados.

S7. Los mensajes del usuario, los avisos de error y la línea de fuentes siguen en texto plano.

S8. Los textos nuevos están localizados en `Assistant.resx` (español) y `Assistant.en.resx` (inglés).

S9. La documentación registra el requerimiento en la SPEC-021 independiente, la matriz de aceptación (AC-060, AC-061), el párrafo del asistente en `README.md` y la visión de producto.

S10. Verificación: pruebas deterministas del formateador y del pin del prompt, build sin errores ni avisos, y suite completa; la comprobación de interfaz se registra con su evidencia o su limitación si el entorno no permite levantar el perfil local.

## Tasks

- T1 — Formateador seguro, componentes de renderizado, estilos y regla de formato del prompt (S3–S7). Ruta: inline. Estado: completo; commit `49a9dec`.
- T2 — Botón «Nuevo chat», localización y guardia de respuesta en curso (S1, S2, S8). Ruta: inline. Estado: completo; commit `9fab71b`.
- T3 — SPEC-021, matriz de aceptación, README, visión y verificación final (S9, S10). Ruta: inline. Estado: completo; commit documental de promoción a SPEC-021.

## Log

L1 (2026-10-25, verbatim): «Ayudame con un nuevo requerimiento, me gustaría que el asistente tuviera en alguna parte un boton para crear un nuevo chat, aunque no se guarde pues como para limpiar todo, por otro lado tambien veo que el chat responde como en un formato md, es posible que cuando se muesttra se viera bonito y no con *** o mejor lo retornamos plano»

L2 (2026-10-25): decisión del usuario para el formato de respuesta: «Renderizar markdown seguro (Recomendado)»; se descarta devolver texto plano.

L3 (2026-10-25, exploración): `Assistant.razor` pinta la respuesta con `white-space: pre-wrap` y sin procesar; `ChatOrchestrator.BuildSystemPrompt` no restringe markdown, por lo que el proveedor devuelve `**negrita**` y listas literales. El historial vive solo en el cliente y se pierde al recargar (SPEC-009 §8), así que limpiar la conversación no contradice la persistencia aprobada.

L4 (2026-10-25, comprobación real): el primer recorrido de navegador destapó un defecto real: `<AssistantMarkdown Text="item.Text" />` pasaba la cadena literal `item.Text` porque un parámetro `string` de componente requiere `@`. Corregido a `Text="@item.Text"` y verificado en la página real (la respuesta pasa a mostrar el texto del modelo con `<strong>`, `<ul>/<li>` y `<code>`, sin `**` visibles).

L5 (2026-10-25, proveedor): el turno en vivo contra Groq devolvió 429 (límite de tasa conocido) y la interfaz mostró el mensaje controlado. Para no consumir el proveedor ni depender de su disponibilidad, la presentación se verificó de extremo a extremo con un stub local compatible con OpenAI en `AI__BaseUrl` (proceso temporal apagado al terminar; sin cambios en el repositorio). El botón y el renderizado se comprobaron en el navegador: estado deshabilitado sin conversación, limpieza de mensajes y borrador, y respuesta formateada.

L6 (2026-10-25, verificación): `dotnet build` 0 errores y 0 avisos; suite completa 24/24 unitarias y 231/231 de integración en verde con `ConnectionStrings__TalentAssistantTests` y `HcmPostgres__ConnectionString` apuntando a los contenedores locales. Pruebas nuevas: 19 casos del formateador, 2 del renderizado real del componente (incluye codificación de HTML) y 1 pin del prompt. RDD: desactivado por decisión global del usuario; no se inició ciclo de revisión nativo.

L7 (2026-10-25, entrega): el conteo propio es ~795 líneas autoradas, por encima de la heurística de 400. El usuario eligió un solo PR con dos commits: renderizado seguro y botón nuevo chat con documentación. No se hará push ni se abrirá PR sin autorización adicional.

L8 (2026-10-25, pendiente): la pasada en vivo contra Groq queda pendiente por el 429; no bloquea la funcionalidad y se puede repetir cuando el proveedor esté disponible.

L9 (2026-10-25, formalización): el usuario pidió que el requerimiento quedara como spec independiente. Se creó `specs/021-assistant-new-chat-and-answer-format/spec.md` (SPEC-021), se retiró la enmienda §15 de SPEC-009 —que ahora remite a SPEC-021— y se añadieron AC-060/AC-061 a la matriz de aceptación. La implementación no cambia; el commit documental cierra la promoción.
