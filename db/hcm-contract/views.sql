-- Contrato de vistas del conector HCM, RECABLEADO sobre las tablas reales
-- (SPEC-012 S3; ADR-0012). Las vistas son la capa anti-corrupción: la
-- aplicación solo conoce este esquema. Desde ahora las tablas fuente son las
-- de forma real (db/hcm-real/schema.sql + seed.sql) y la sustitución hcm.* se
-- reduce a lo que el shape real todavía no expone.
--
-- Alcance de servicio (ampliado 2026-10-25 por decisión literal del usuario:
-- «Ampliar a los 209 empleados reales»): las vistas de personas, presencia y
-- ausencias sirven a TODOS los empleados activos del HCM (deleted_on IS NULL y
-- status_id <> 3), no solo a los NUEVE fixtures con department 'office-demo-%'.
-- Los nueve fixtures conservan sus alias employee-demo-* para que la paridad
-- campo a campo con el fuente sintético siga intacta; el resto del catálogo
-- activo recibe el alias determinista 'employee-'||id. Cualquier empleado
-- fuera de ese alcance (borrado o inactivo) queda fuera de TODAS las vistas,
-- de modo que ningún identificador servido cuelga del directorio.
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
-- Identificadores: el shape real usa enteros; el contrato usa textos. El CASE
-- de alias es el mapeo explícito de los nueve fixtures y se repite en las tres
-- vistas que necesitan el identificador de texto (personas, presencia y
-- ausencias), siempre con el mismo criterio (nombre del empleado) para que las
-- tres coincidan sin depender del orden de ids de la semilla; el COALESCE
-- añade 'employee-'||id al resto del catálogo activo. En AUSENCIAS las
-- referencias de los tres fixtures se conservan y el resto usa 'timeoff-'||id.
-- Duración de ausencias: derivada como (ends_before - starts_on) en días,
-- con unidad bilingüe literal; el shape real no almacena duración.

CREATE SCHEMA IF NOT EXISTS hcm_contract;

-- 1) Directorio (todos los empleados activos; display name = name + last_name).
--    Job title,
--    surname, hire date, birthday month/day and the DERIVED age_on_next_birthday
--    (age turned at the next birthday from CURRENT_DATE; approved 2026-10-24)
--    are explicit approved projections; birth year and every other employee
--    column remain outside the contract. Desde 2026-10-25 el contrato añade
--    area (=department escalar), gender, years_in_company, el jefe directo
--    (manager_employee_id/manager_display_name desde vacancy_supervisor) y el
--    correo corporativo (email = first_email; second_email queda fuera) para
--    las preguntas de headcount, antigüedad, jefe y contacto. gender es un
--    añadido local del dataset ficticio: el shape real pegado no lo expone.
--    Sobre un volumen existente hay que re-aplicar este fichero
--    (ver docs/development/local-setup.md).
CREATE OR REPLACE VIEW hcm_contract.talentassistant_employees AS
SELECT
    aliases.employee_id,
    aliases.display_name,
    aliases.email,
    aliases.office_ids,
    aliases.area,
    aliases.gender,
    aliases.job_title,
    aliases.birthday_month,
    aliases.birthday_day,
    aliases.last_name,
    aliases.hire_date,
    aliases.years_in_company,
    aliases.manager_employee_id,
    aliases.manager_display_name,
    aliases.age_on_next_birthday
FROM (
    SELECT
        COALESCE(
            CASE e.id
                WHEN 1 THEN 'employee-demo-juan'
                WHEN 2 THEN 'employee-demo-pedro'
                WHEN 3 THEN 'employee-demo-mauricio'
                WHEN 4 THEN 'employee-demo-herrera'
                WHEN 5 THEN 'employee-demo-madrid'
                WHEN 6 THEN 'employee-demo-tokyo-002'
                WHEN 7 THEN 'employee-demo-madrid-004'
                WHEN 8 THEN 'employee-demo-tokyo'
                WHEN 9 THEN 'employee-demo-madrid-002'
            END,
            'employee-' || e.id::text
        ) AS employee_id,
        COALESCE(
            NULLIF(TRIM(CONCAT(e.name, ' ', e.last_name)), ''),
            'employee-' || e.id::text
        ) AS display_name,
        ARRAY_REMOVE(ARRAY[e.department]::text[], NULL) AS office_ids,
        NULLIF(e.first_email, '') AS email,
        e.department AS area,
        e.gender AS gender,
        e."job:title" AS job_title,
        EXTRACT(MONTH FROM e.birthday_date)::integer AS birthday_month,
        EXTRACT(DAY FROM e.birthday_date)::integer AS birthday_day,
        e.last_name AS last_name,
        e.hire_date::date AS hire_date,
        -- Tenure in completed years, derived in the HCM view from the approved
        -- hire date (same pattern as age_on_next_birthday; no birth year or
        -- other restricted column leaves this view).
        CASE WHEN e.hire_date IS NULL THEN NULL
             ELSE DATE_PART('year', AGE(CURRENT_DATE, e.hire_date))::integer
        END AS years_in_company,
        -- Jefe directo: el shape real expone vacancy_supervisor como id de
        -- empleado (0 = sin jefe). Se resuelve el alias estable y el nombre
        -- visible con el mismo criterio que el directorio.
        CASE WHEN manager.id IS NULL THEN NULL
             ELSE COALESCE(
                 CASE manager.id
                     WHEN 1 THEN 'employee-demo-juan'
                     WHEN 2 THEN 'employee-demo-pedro'
                     WHEN 3 THEN 'employee-demo-mauricio'
                     WHEN 4 THEN 'employee-demo-herrera'
                     WHEN 5 THEN 'employee-demo-madrid'
                     WHEN 6 THEN 'employee-demo-tokyo-002'
                     WHEN 7 THEN 'employee-demo-madrid-004'
                     WHEN 8 THEN 'employee-demo-tokyo'
                     WHEN 9 THEN 'employee-demo-madrid-002'
                 END,
                 'employee-' || manager.id::text)
        END AS manager_employee_id,
        CASE WHEN manager.id IS NULL THEN NULL
             ELSE COALESCE(
                 NULLIF(TRIM(CONCAT(manager.name, ' ', manager.last_name)), ''),
                 'employee-' || manager.id::text)
        END AS manager_display_name,
        -- Derived age (SPEC-008 §3, approved 2026-10-24): age turned at the
        -- next birthday occurrence from CURRENT_DATE, with the same Feb-29
        -- rule as the adapter laterals (Feb 29 only on Feb 29); NULL when the
        -- source has no birthday. The birth year never leaves this expression.
        (EXTRACT(YEAR FROM next_birthday.next_birthday_on)
            - EXTRACT(YEAR FROM e.birthday_date))::integer AS age_on_next_birthday
    FROM public.employee AS e
    LEFT JOIN public.employee AS manager ON manager.id = e.vacancy_supervisor
    LEFT JOIN LATERAL (
        SELECT dates.next_birthday_on
        FROM (
            SELECT CASE
                WHEN EXTRACT(MONTH FROM e.birthday_date) = 2
                 AND EXTRACT(DAY FROM e.birthday_date) = 29
                 AND NOT (
                     years.candidate_year % 400 = 0
                     OR (years.candidate_year % 4 = 0
                         AND years.candidate_year % 100 <> 0))
                THEN NULL::date
                ELSE make_date(
                    years.candidate_year,
                    EXTRACT(MONTH FROM e.birthday_date)::integer,
                    EXTRACT(DAY FROM e.birthday_date)::integer)
            END AS next_birthday_on
            FROM generate_series(
                EXTRACT(YEAR FROM CURRENT_DATE)::integer,
                EXTRACT(YEAR FROM CURRENT_DATE)::integer + 8)
                AS years(candidate_year)
        ) AS dates
        WHERE dates.next_birthday_on >= CURRENT_DATE
        ORDER BY dates.next_birthday_on
        LIMIT 1
    ) AS next_birthday ON true
    WHERE e.deleted_on IS NULL
      AND e.status_id <> 3
) AS aliases
WHERE aliases.employee_id IS NOT NULL;

-- 2) Presencia derivada (§5) para el rango -45..+30 días; el adaptador filtra
--    la fecha consultada (cualquier consulta razonable queda cubierta).
CREATE OR REPLACE VIEW hcm_contract.talentassistant_presence AS
WITH scoped AS (
    SELECT
        e.id AS int_id,
        COALESCE(
            CASE e.id
                WHEN 1 THEN 'employee-demo-juan'
                WHEN 2 THEN 'employee-demo-pedro'
                WHEN 3 THEN 'employee-demo-mauricio'
                WHEN 4 THEN 'employee-demo-herrera'
                WHEN 5 THEN 'employee-demo-madrid'
                WHEN 6 THEN 'employee-demo-tokyo-002'
                WHEN 7 THEN 'employee-demo-madrid-004'
                WHEN 8 THEN 'employee-demo-tokyo'
                WHEN 9 THEN 'employee-demo-madrid-002'
            END,
            'employee-' || e.id::text
        ) AS employee_id,
        COALESCE(
            NULLIF(TRIM(CONCAT(e.name, ' ', e.last_name)), ''),
            'employee-' || e.id::text
        ) AS display_name,
        ARRAY_REMOVE(ARRAY[e.department]::text[], NULL) AS office_ids
    FROM public.employee AS e
    WHERE e.deleted_on IS NULL
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

-- 3) Ausencias: TODAS las filas de los empleados activos (ampliación de alcance
--    2026-10-25); date_end INCLUSIVO -> ends_before = date_end + 1 día; duración
--    derivada. reference: los tres fixtures conservan las referencias del
--    contrato y el resto usa 'timeoff-'||id. type sale del catálogo público
--    timeoff_type (type_en con la traducción fija de esos siete tipos), status
--    de status_id (1 Pendiente/Pending, 2 Aprobado/Approved, 3
--    Rechazado/Rejected) y el resumen real proyecta note en los dos idiomas
--    (decisión de producto 2026-10-25: «Mismo texto en summary_es y
--    summary_en»); los tres fixtures conservan sus textos bilingües literales
--    de paridad. Solo se sirven ausencias de empleados activos, así que ningún
--    employee_id cuelga del directorio.
CREATE OR REPLACE VIEW hcm_contract.talentassistant_timeoff AS
SELECT
    COALESCE(
        CASE t.id
            WHEN 1 THEN 'timeoff-juan-2026-10'
            WHEN 2 THEN 'timeoff-pedro-2026-10'
            WHEN 3 THEN 'timeoff-mauricio-2026-10'
        END,
        'timeoff-' || t.id::text
    ) AS reference,
    COALESCE(
        CASE e.id
            WHEN 1 THEN 'employee-demo-juan'
            WHEN 2 THEN 'employee-demo-pedro'
            WHEN 3 THEN 'employee-demo-mauricio'
            WHEN 4 THEN 'employee-demo-herrera'
            WHEN 5 THEN 'employee-demo-madrid'
            WHEN 6 THEN 'employee-demo-tokyo-002'
            WHEN 7 THEN 'employee-demo-madrid-004'
            WHEN 8 THEN 'employee-demo-tokyo'
            WHEN 9 THEN 'employee-demo-madrid-002'
        END,
        'employee-' || e.id::text
    ) AS employee_id,
    COALESCE(tt.name::text, '') AS type_es,
    CASE tt.id
        WHEN 1 THEN 'Vacation'
        WHEN 2 THEN 'Calamity'
        WHEN 3 THEN 'Sick leave'
        WHEN 4 THEN 'Personal leave'
        WHEN 5 THEN 'Paid leave'
        WHEN 6 THEN 'Compensatory time'
        WHEN 7 THEN 'External training'
        ELSE COALESCE(tt.name::text, '')
    END AS type_en,
    COALESCE(
        CASE t.status_id
            WHEN 1 THEN 'Pendiente'
            WHEN 2 THEN 'Aprobado'
            WHEN 3 THEN 'Rechazado'
        END,
        ''
    ) AS status_es,
    COALESCE(
        CASE t.status_id
            WHEN 1 THEN 'Pending'
            WHEN 2 THEN 'Approved'
            WHEN 3 THEN 'Rejected'
        END,
        ''
    ) AS status_en,
    t.date_start::date AS starts_on,
    (t.date_end::date + 1) AS ends_before,
    ((t.date_end::date + 1) - t.date_start::date)::text AS duration,
    'día laborable' AS duration_unit_es,
    'workday' AS duration_unit_en,
    CASE t.id
        WHEN 1 THEN 'El HCM sintético informa una ausencia de vacaciones aprobada de Juan Pérez.'
        WHEN 2 THEN 'El HCM sintético informa la ausencia de vacaciones en curso de Pedro Rojas.'
        WHEN 3 THEN 'El HCM sintético informa una solicitud de calamidad rechazada de Mauricio Cardenas.'
        ELSE t.note::text
    END AS summary_es,
    CASE t.id
        WHEN 1 THEN 'The synthetic HCM reports an approved vacation absence for Juan Pérez.'
        WHEN 2 THEN 'The synthetic HCM reports an in-progress vacation absence for Pedro Rojas.'
        WHEN 3 THEN 'The synthetic HCM reports a rejected calamity request for Mauricio Cardenas.'
        ELSE t.note::text
    END AS summary_en
FROM public.timeoff AS t
JOIN public.employee AS e
    ON e.id = t.employee_id
   AND e.deleted_on IS NULL
   AND e.status_id <> 3
LEFT JOIN public.timeoff_type AS tt
    ON tt.id = t.timeoff_type_id;

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
