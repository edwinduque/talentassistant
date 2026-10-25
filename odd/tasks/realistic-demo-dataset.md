# Feature: dataset ficticio realista y consultable

Objetivo: sustituir el dataset ficticio degenerado por uno realista (nombres completos únicos,
área, género, cargo, antigüedad y jefe) y exponer esos campos al asistente sin plantillas nuevas.
Locator: `odd/tasks/realistic-demo-dataset.md`.
Rama / entrega: `feat/realistic-demo-dataset`; `ask-on-risk`; estrategia elegida: `single-pr`.
Test runner: `dotnet test TalentAssistant.sln --configuration Debug` (unidad + integración).

## Specs

- S1. Regenerar el dataset ficticio de `db/hcm-real` conservando los volúmenes verificados
  (`employees=209`, `active=197`, `timeoff>=2000`, `time_check>=20000` en 45 días, `tasks>=2000`)
  y los hechos de paridad de los fixtures 1..9 (identidad, cargo, apellido, fecha de alta,
  cumpleaños y estados de presencia/ausencia). Sus `department` pasan de `office-demo-*` a
  departamentos reales; la ubicación sigue viviendo en `hcm.attendance_incidents`.
- S2. Los 200 empleados generados tienen nombre completo único. Queda eliminada la correlación
  `nombre -> área` que producía diez «Pablo Molina» todos en Legal.
  Texto del usuario: «pero con datos que parezcan reales».
- S3. Cada empleado activo tiene género `F`/`M` coherente con su nombre, con distribución
  aproximada 50/50, expuesto como `gender` en el dataset `employees` para responder
  «cuantos hombres hay en la compañia, cuantas mujeres».
- S4. El dataset `employees` expone `area` (escalar, el `department` del HCM) además de
  `office_ids`, para poder agrupar y contar «cantidad por area».
- S5. Cada empleado activo salvo el máximo responsable tiene jefe directo. El dataset expone
  `manager_employee_id` y `manager_display_name`. Supuesto: «jefe» = `vacancy_supervisor`
  (supervisor del puesto). Los fixtures 1..9 reportan a Juan Pérez (id 1), que no tiene jefe;
  los responsables de área generados también reportan a Juan Pérez.
- S6. El dataset expone `years_in_company`, derivado en la vista desde `hire_date` igual que
  `age_on_next_birthday`, para responder «tiempo en la empresa» sin aritmética del modelo.
- S7. El cargo (`job_title`) es realista por área y mayoritariamente no nulo; los responsables
  de área se titulan `Responsable de <área>` y el máximo responsable `Director general`.
- S8. El prompt del asistente exige copiar literalmente los valores de cada fila devuelta por
  una herramienta y prohíbe mezclar o desplazar valores entre filas, para no repetir un
  `employee-229` inventado. Texto del usuario: «no employee-229».
- S9. `gender` se documenta como añadido local del dataset ficticio: no está en el shape real
  pegado (`db/hcm-real/upstream.sql`). El cambio de columnas de la vista exige recarga limpia
  (drop/recreate) de la base demo, autorizada por el usuario: «podemos borrar los actuales
  para evitar conflictos».
- S10. Verificación: inicialización limpia local con los volúmenes y guardas nuevas; consultas
  de ejemplo (cargo, área, antigüedad, cantidad por área, hombres/mujeres, jefe) contra
  Postgres; compilación y suite completa en verde.

## Tasks

| ID | Specs | Ruta | Estado | Evidencia |
|----|-------|------|--------|-----------|
| T1 | S1-S7, S9 | inline | Completada | `seed.sql`/`schema.sql`/`views.sql`/`verify-demo-data.sql`; init limpio 209/197/197 nombres/11 áreas/2000/20000/2000 |
| T2 | S3-S6 | inline | Completada | `HcmDataQuerySchema`, fuentes Postgres/sintética, `HcmEmployeeDirectoryEntry`; build 0 errores |
| T3 | S1, S3-S7 | inline | Completada | Fuente sintética + paridad; integración 202/202 |
| T4 | S8 | inline | Completada | Prompt + pin en `ChatOrchestratorTests` |
| T5 | S9 | inline | Completada | `local-setup.md`, SPEC-012 §11, AC-040 |
| T6 | S10 | inline | Completada | Muestras SQL verificadas; suite y fallo ambiental documentados |

## Log

- L1 (2026-10-25, usuario, verbatim): «Hay respuestas que me parecen inconsistentes en el asistente

  Ej:
  Pregunta: Pablo molina de que area es?

  Respuesta:
  He encontrado varios empleados con el nombre **Pablo Molina**. A continuación tienes sus datos resumidos para que me indiques cuál te interesa:

  | Id | Área (OfficeIds) | Puesto | Fecha de contratación |
  |----|------------------|--------|------------------------|
  | employee‑107 | Legal | (sin puesto) | 13 /05 /2016 |
  | employee‑127 | Legal | Agente de nivel 4 | 28 /05 /2017 |
  | employee‑147 | Legal | (sin puesto) | 12 /06 /2018 |
  | employee‑167 | Legal | (sin puesto) | 27 /06 /2019 |
  | employee‑187 | Legal | Agente de nivel 4 | 11 /07 /2020 |
  | employee‑207 | Legal | (sin puesto) | 26 /07 /2021 |
  | employee‑229 | Legal | Agente de nivel 4 | 10 /08 /2022 |
  | employee‑247 | Legal | (sin puesto) | 25 /08 /2023 |
  | employee‑267 | Legal | (sin puesto) | 08 /09 /2024 |
  | employee‑287 | Legal | (sin puesto) | 23 /09 /2025 |

  Todos pertenecen al área **Legal**. Por favor, indícame el **Id** del empleado al que te refieres para poder darte información más detallada.»

- L2 (2026-10-25) Diagnóstico reproducible contra el volumen local `talentassistant_hcm`
  (misma semilla recreada en la VPS en L34 de `hcm-contract-all-employees.md`):
  `197` activos y solo `29` nombres completos distintos. Generado por
  `db/hcm-real/seed.sql`: `name = (7n) % 20`, `last_name = f(7n % 10)` y `department = f(n % 10)`;
  el nombre completo determina el área y cada nombre se repite 10 veces. Además la respuesta
  imprimió `employee-229` (Iván Sánchez, Compras) con la fecha de alta de `employee-227` y el
  cargo de `employee-247`: error de transcripción del modelo, no de los datos.
- L3 (2026-10-25, usuario, verbatim): «regenerar los datos demo, podemos borrar los actuales
  para evitar conflictos, preguntaré cosas como cargo, area, tiempo en la empresa, cantidad por
  area, cuantos hombres hay en la compania, cuantas mujeres, quien es el jefe de xxxxxx. pero
  con datos que parezcan reales, no employee-229».
- L4 (2026-10-25) Implementación inline T1-T6. Generador: nombre `(7n)%20` (10 nombres
  femeninos + 10 masculinos), apellido `(7n + bloque)%10` (200 combinaciones únicas),
  área por permutación `(61n)%200` con 11 áreas ponderadas, cargo por área y género derivado
  del nombre. Jerarquía: el activo de menor id de cada área es su responsable; los responsables
  reportan a Juan Pérez (id 1, Dirección), que no tiene jefe. Fixtures 1..9 conservan identidad,
  cargo, apellido, alta y cumpleaños; su `department` pasa a Dirección/Soporte/Finanzas/
  Operaciones/Calidad/Compras. Contrato: `area`, `gender`, `years_in_company`,
  `manager_employee_id` y `manager_display_name` en la vista y en `query_hr_data`.
  `gender` queda documentado como añadido local del dataset ficticio.
- L5 (2026-10-25) Evidencia de inicialización limpia local (`docker compose rm -sf hcm-postgres`
  + volumen borrado + `up -d`): `verify-demo-data.sql` →
  `employees=209, active=197, distinct_names=197, areas=11, timeoff=2000,
  time_checks_45d=20000, tasks=2000`. Muestras: `Pablo Molina` → única fila, Operaciones,
  Planificador de producción, jefe Sergio Iglesias, 3 años; headcount por área 32/31/31/21/18/
  15/13/12/9/8/7; género F=96, M=101. Esas mismas cifras son datos ficticios, no personas reales.
- L6 (2026-10-25) Verificación: build 0 errores; integración **202/202**; unitarias **23/24**
  porque `ArchitectureBoundaryTests` falla por una directiva de Control de aplicaciones de
  Windows sobre `TalentAssistant.Domain.dll` (`FileLoadException`, 0x800711C7), reproducida
  en un worktree limpio de `main` `1639bb2` sin estos cambios: es ambiental y no una regresión.
- L7 (2026-10-25) La recarga de la VPS requiere recrear la base demo (columnas nuevas); la
  ejecuta el usuario. El bundle `vps-demo-upgrade.psql` no añade `gender` y no aplica sobre
  el shape anterior.
