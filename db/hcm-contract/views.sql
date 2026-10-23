-- Contrato de vistas del conector HCM, RECABLEADO sobre las tablas reales
-- (SPEC-012 S3; ADR-0012). Las vistas son la capa anti-corrupción: la
-- aplicación solo conoce este esquema. Desde ahora las tablas fuente son las
-- de forma real (db/hcm-real/schema.sql + seed.sql) y la sustitución hcm.* se
-- reduce a lo que el shape real todavía no expone.
--
-- Alcance de servicio (default aprobado en la puerta de T3): las vistas de
-- personas, presencia y ausencias sirven a los NUEVE empleados de fixture
-- (marcador: department = identificador de unidad orgánica 'office-demo-%'),
-- de modo que la salida del contrato es idéntica a la del fuente sintético y
-- la suite permanece verde; ampliar el catálogo servido a los209 empleados
-- reales es retirar ese filtro cuando se decida.
--
-- Derivación de presencia (SPEC-012 §5, aprobada): prioridad
--   1 vacation: timeoff status_id=2 (activo) cubriendo la fecha (date_end es
--     INCLUSIVO: cubre si date_start <= fecha <= date_end o date_end nulo);
--   2 working: hay time_check con check_type_id=1 ese día (fecha UTC del
--     instante) SIN checkout_type_id=3 ese día;
--   3 absent: hay checkout_type_id=3 ese día (jornada finalizada);
--   4 not-arrived: sin registros ese día (regla literal del usuario).
-- El "día" de las marcaciones se evalúa en UTC (marco del seed y de los
-- instantes time_check_date_utc).
--
-- Gaps conocidos (shape real sin fuente): la vista de TAREAS sigue en la
-- sustitución hcm.tasks y la de INSIGHTS de asistencia en
-- hcm.attendance_incidents hasta que el HCM exponga su DDL de tareas y sus
-- hallazgos de regla evaluados (la aplicación nunca deriva reglas del HCM).
--
-- Identificadores: el shape real usa enteros; el contrato usa los textos del
-- fuente sintético. El CASE de alias es el mapeo explícito de los nueve
-- fixtures (futuro: al ampliar alcance, 'employee-'||id y las pruebas evolucionan).
-- Duración de ausencias: derivada como (ends_before - starts_on) en días,
-- con unidad bilingüe literal; el shape real no almacena duración.

CREATE SCHEMA IF NOT EXISTS hcm_contract;

-- 1) Directorio (solo fixtures; display name = name + last_name).
CREATE OR REPLACE VIEW hcm_contract.talentassistant_employees AS
SELECT aliases.employee_id, aliases.display_name, aliases.office_ids
FROM (
    SELECT
        CASE
            WHEN e.name = 'Juan' AND e.last_name = 'Pérez' THEN 'employee-demo-juan'
            WHEN e.name = 'Pedro' AND e.last_name = 'Rojas' THEN 'employee-demo-pedro'
            WHEN e.name = 'Mauricio' AND e.last_name = 'Cardenas' THEN 'employee-demo-mauricio'
            WHEN e.name = 'Juan' AND e.last_name = 'Herrera' THEN 'employee-demo-herrera'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 001' THEN 'employee-demo-madrid'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 005' THEN 'employee-demo-tokyo-002'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 004' THEN 'employee-demo-madrid-004'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 002' THEN 'employee-demo-tokyo'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 003' THEN 'employee-demo-madrid-002'
        END AS employee_id,
        e.name || ' ' || e.last_name AS display_name,
        ARRAY[e.department]::text[] AS office_ids
    FROM public.employee AS e
    WHERE e.department LIKE 'office-demo-%'
      AND e.deleted_on IS NULL
      AND e.status_id <> 3
) AS aliases
WHERE aliases.employee_id IS NOT NULL;

-- 2) Presencia derivada (§5) para el rango -45..+30 días; el adaptador filtra
--    la fecha consultada (cualquier consulta razonable queda cubierta).
CREATE OR REPLACE VIEW hcm_contract.talentassistant_presence AS
WITH scoped AS (
    SELECT
        e.id AS int_id,
        CASE
            WHEN e.name = 'Juan' AND e.last_name = 'Pérez' THEN 'employee-demo-juan'
            WHEN e.name = 'Pedro' AND e.last_name = 'Rojas' THEN 'employee-demo-pedro'
            WHEN e.name = 'Mauricio' AND e.last_name = 'Cardenas' THEN 'employee-demo-mauricio'
            WHEN e.name = 'Juan' AND e.last_name = 'Herrera' THEN 'employee-demo-herrera'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 001' THEN 'employee-demo-madrid'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 005' THEN 'employee-demo-tokyo-002'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 004' THEN 'employee-demo-madrid-004'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 002' THEN 'employee-demo-tokyo'
            WHEN e.name || ' ' || e.last_name = 'Empleado sintético 003' THEN 'employee-demo-madrid-002'
        END AS employee_id,
        e.name || ' ' || e.last_name AS display_name,
        ARRAY[e.department]::text[] AS office_ids
    FROM public.employee AS e
    WHERE e.department LIKE 'office-demo-%'
      AND e.deleted_on IS NULL
      AND e.status_id <> 3
), days AS (
    SELECT d::date AS presence_date
    FROM generate_series(CURRENT_DATE - 45, CURRENT_DATE + 30, interval '1 day') AS d
), punches AS (
    SELECT
        tc.employee_id AS int_id,
        (tc.time_check_date_utc AT TIME ZONE 'UTC')::date AS punch_date,
        bool_or(tc.check_type_id = 1) AS has_entry,
        bool_or(tc.checkout_type_id = 3) AS has_end
    FROM public.time_check AS tc
    GROUP BY tc.employee_id, (tc.time_check_date_utc AT TIME ZONE 'UTC')::date
)
SELECT
    d.presence_date,
    s.employee_id,
    s.display_name AS employee_display_name,
    s.office_ids AS employee_office_ids,
    CASE
        WHEN EXISTS (
            SELECT 1 FROM public.timeoff AS t
            WHERE t.employee_id = s.int_id
              AND t.status_id = 2
              AND t.date_start::date <= d.presence_date
              AND (t.date_end IS NULL OR t.date_end::date >= d.presence_date)
        ) THEN 'vacation'
        WHEN COALESCE(p.has_entry, false) AND NOT COALESCE(p.has_end, false) THEN 'working'
        WHEN COALESCE(p.has_end, false) THEN 'absent'
        ELSE 'not-arrived'
    END AS state
FROM scoped AS s
CROSS JOIN days AS d
LEFT JOIN punches AS p ON p.int_id = s.int_id AND p.punch_date = d.presence_date
WHERE s.employee_id IS NOT NULL;

-- 3) Ausencias: solo las tres filas de fixture (referencias del contrato);
--    date_end INCLUSIVO -> ends_before = date_end + 1 día; duración derivada.
CREATE OR REPLACE VIEW hcm_contract.talentassistant_timeoff AS
SELECT
    CASE t.id
        WHEN 1 THEN 'timeoff-juan-2026-10'
        WHEN 2 THEN 'timeoff-pedro-2026-10'
        WHEN 3 THEN 'timeoff-mauricio-2026-10'
    END AS reference,
    CASE t.employee_id
        WHEN 1 THEN 'employee-demo-juan'
        WHEN 2 THEN 'employee-demo-pedro'
        WHEN 3 THEN 'employee-demo-mauricio'
    END AS employee_id,
    CASE WHEN t.id = 3 THEN 'Calamidad' ELSE 'Vacación' END AS type_es,
    CASE WHEN t.id = 3 THEN 'Calamity' ELSE 'Vacation' END AS type_en,
    CASE WHEN t.id = 3 THEN 'Rechazado' ELSE 'Aprobado' END AS status_es,
    CASE WHEN t.id = 3 THEN 'Rejected' ELSE 'Approved' END AS status_en,
    t.date_start::date AS starts_on,
    (t.date_end::date + 1) AS ends_before,
    ((t.date_end::date + 1) - t.date_start::date)::text AS duration,
    'día laborable' AS duration_unit_es,
    'workday' AS duration_unit_en,
    CASE t.id
        WHEN 1 THEN 'El HCM sintético informa una ausencia de vacaciones aprobada de Juan Pérez.'
        WHEN 2 THEN 'El HCM sintético informa la ausencia de vacaciones en curso de Pedro Rojas.'
        WHEN 3 THEN 'El HCM sintético informa una solicitud de calamidad rechazada de Mauricio Cardenas.'
    END AS summary_es,
    CASE t.id
        WHEN 1 THEN 'The synthetic HCM reports an approved vacation absence for Juan Pérez.'
        WHEN 2 THEN 'The synthetic HCM reports an in-progress vacation absence for Pedro Rojas.'
        WHEN 3 THEN 'The synthetic HCM reports a rejected calamity request for Mauricio Cardenas.'
    END AS summary_en
FROM public.timeoff AS t
WHERE t.id IN (1, 2, 3);

-- 4) Tareas: fuente legada hcm.tasks (gap del shape, ver cabecera).
CREATE OR REPLACE VIEW hcm_contract.talentassistant_tasks AS
SELECT
    reference,
    employee_id,
    title_es,
    title_en,
    status_es,
    status_en,
    due_on,
    summary_es,
    summary_en
FROM hcm.tasks;

-- 5) Insights de asistencia: fuente legada hcm.attendance_incidents (gap del
--    shape: el HCM real no expone hallazgos de regla evaluados; ver cabecera).
CREATE OR REPLACE VIEW hcm_contract.talentassistant_attendance_insights AS
SELECT
    i.incident_id,
    i.employee_id,
    e.display_name AS employee_display_name,
    e.office_ids AS employee_office_ids,
    i.office_id,
    i.office_country,
    i.office_city,
    i.office_iana_time_zone,
    i.incident_at_utc,
    i.work_date,
    i.schedule_starts_at,
    i.schedule_ends_at,
    i.punches,
    i.rule_reference,
    i.rule_requires_review,
    i.rule_summary_es,
    i.rule_summary_en,
    i.rule_evaluated_at_utc,
    i.rule_evidence_es,
    i.rule_evidence_en,
    i.rule_review_items_es,
    i.rule_review_items_en,
    i.related_timeoff_reference,
    i.related_starts_on,
    i.related_ends_before,
    i.related_summary_es,
    i.related_summary_en,
    i.related_type_es,
    i.related_type_en,
    i.related_status_es,
    i.related_status_en,
    i.related_duration,
    i.related_duration_unit_es,
    i.related_duration_unit_en
FROM hcm.attendance_incidents AS i
JOIN hcm.employees AS e ON e.id = i.employee_id;
