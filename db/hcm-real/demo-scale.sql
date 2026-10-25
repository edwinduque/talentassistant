-- Idempotent fictional-demo top-up for an initialized HCM database.
-- The caller owns the transaction and sets TimeZone=UTC. This file never
-- deletes records and refuses to rename rows unless the seeded dataset matches
-- the known fictional HCM signature.

DO $$
DECLARE
    total_employees bigint;
    active_employees bigint;
    generated_employees bigint;
    matching_generated_emails bigint;
    matching_fixture_names bigint;
    deleted_generated_employees bigint;
    pending_generated_employees bigint;
    nullable_generated_surnames bigint;
    visible_timeoff_count bigint;
    maximum_timeoff_id bigint;
BEGIN
    SELECT count(*)
    INTO total_employees
    FROM public.employee;

    SELECT count(*)
    INTO active_employees
    FROM public.employee
    WHERE deleted_on IS NULL AND status_id <> 3;

    SELECT count(*)
    INTO generated_employees
    FROM public.employee
    WHERE id BETWEEN 101 AND 300;

    SELECT count(*)
    INTO matching_generated_emails
    FROM public.employee
    WHERE id BETWEEN 101 AND 300
      AND (first_email = '' OR first_email = 'persona' || (id - 100)::text || '@example.com');

    SELECT count(*)
    INTO matching_fixture_names
    FROM public.employee
    WHERE (id = 5 AND ((name = 'Empleado' AND last_name = 'sintético 001') OR (name = 'Aitana' AND last_name = 'Vidal')))
       OR (id = 6 AND ((name = 'Empleado' AND last_name = 'sintético 005') OR (name = 'Gael' AND last_name = 'Domínguez')))
       OR (id = 7 AND ((name = 'Empleado' AND last_name = 'sintético 004') OR (name = 'Nadia' AND last_name = 'Soler')))
       OR (id = 8 AND ((name = 'Empleado' AND last_name = 'sintético 002') OR (name = 'Mireia' AND last_name = 'Pardo')))
       OR (id = 9 AND ((name = 'Empleado' AND last_name = 'sintético 003') OR (name = 'Bruno' AND last_name = 'Castaño')));

    SELECT count(*)
    INTO deleted_generated_employees
    FROM public.employee
    WHERE id BETWEEN 101 AND 300
      AND deleted_on IS NOT NULL
      AND status_id = 3;

    SELECT count(*)
    INTO pending_generated_employees
    FROM public.employee
    WHERE id BETWEEN 101 AND 300
      AND deleted_on IS NULL
      AND status_id = 1;

    SELECT count(*)
    INTO nullable_generated_surnames
    FROM public.employee
    WHERE id BETWEEN 101 AND 300
      AND last_name IS NULL;

    SELECT count(*)
    INTO visible_timeoff_count
    FROM public.timeoff AS t
    JOIN public.employee AS e ON e.id = t.employee_id
    WHERE e.deleted_on IS NULL AND e.status_id <> 3;

    SELECT COALESCE(max(id), 0)::bigint
    INTO maximum_timeoff_id
    FROM public.timeoff;

    IF total_employees <> 209
       OR active_employees <> 197
       OR generated_employees <> 200
       OR matching_generated_emails <> 200
       OR matching_fixture_names <> 5
       OR deleted_generated_employees <> 12
       OR pending_generated_employees <> 11
       OR nullable_generated_surnames NOT IN (0, 100) THEN
        RAISE EXCEPTION
            'Refusing to modify HCM rows: database does not match the fictional seed signature (employees=%, active=%, generated=%, generated_email_matches=%, fixture_name_matches=%, deleted=%, pending=%, null_surnames=%)',
            total_employees,
            active_employees,
            generated_employees,
            matching_generated_emails,
            matching_fixture_names,
            deleted_generated_employees,
            pending_generated_employees,
            nullable_generated_surnames;
    END IF;

    IF (SELECT count(*) FROM public.timeoff_type) <> 7
       OR (SELECT count(*) FROM public.working_day) <> 7 THEN
        RAISE EXCEPTION 'Refusing to load demo activity: expected the 7-row HCM reference catalogs';
    END IF;

    IF maximum_timeoff_id + GREATEST(0::bigint, 2000 - visible_timeoff_count) > 2147483647 THEN
        RAISE EXCEPTION 'Refusing to top up timeoff: primary-key range would overflow';
    END IF;
END
$$;

-- Replace only the five known fictional labels. Stable integer ids keep the
-- employee-demo-* contract aliases unchanged.
UPDATE public.employee
SET name = 'Aitana', last_name = 'Vidal'
WHERE id = 5
  AND (name, last_name) IS DISTINCT FROM ('Aitana', 'Vidal');

UPDATE public.employee
SET name = 'Gael', last_name = 'Domínguez'
WHERE id = 6
  AND (name, last_name) IS DISTINCT FROM ('Gael', 'Domínguez');

UPDATE public.employee
SET name = 'Nadia', last_name = 'Soler'
WHERE id = 7
  AND (name, last_name) IS DISTINCT FROM ('Nadia', 'Soler');

UPDATE public.employee
SET name = 'Mireia', last_name = 'Pardo'
WHERE id = 8
  AND (name, last_name) IS DISTINCT FROM ('Mireia', 'Pardo');

UPDATE public.employee
SET name = 'Bruno', last_name = 'Castaño'
WHERE id = 9
  AND (name, last_name) IS DISTINCT FROM ('Bruno', 'Castaño');

-- Repair the seeded surname-array index bug without touching non-demo rows.
-- The 2026-10-25 generator mixes the row block into the surname index so the
-- full name is unique; keep this repair aligned with db/hcm-real/seed.sql.
UPDATE public.employee AS e
SET last_name = (ARRAY[
    'García','López','Martínez','Sánchez','Romero',
    'Navarro','Iglesias','Ortega','Vázquez','Molina'
])[1 + (((e.id - 100) * 7 + ((e.id - 101) / 20)) % 10)]
WHERE e.id BETWEEN 101 AND 300
  AND e.last_name IS NULL;

-- Replace only the previous seed's visible generator phrases with plausible
-- fictional absence notes. The fixture notes and NULL-note cases stay intact.
UPDATE public.timeoff
SET note = CASE
    WHEN note LIKE '%nota larga para robustez del contrato%'
        THEN left(repeat('Solicitud de ausencia registrada con observaciones para revisión del calendario laboral. ', 24), 1000)
    ELSE CASE id % 6
        WHEN 0 THEN 'Solicitud de ausencia relacionada con asuntos personales.'
        WHEN 1 THEN 'Vacaciones planificadas según el calendario del equipo.'
        WHEN 2 THEN 'Permiso para asistencia a una cita médica.'
        WHEN 3 THEN 'Ausencia para participar en formación profesional.'
        WHEN 4 THEN 'Ajuste de jornada acordado con la persona responsable.'
        ELSE 'Solicitud de ausencia para atender responsabilidades familiares.'
    END
END
WHERE id >= 16
  AND employee_id BETWEEN 101 AND 300
  AND note LIKE 'Solicitud de ausencia generada%';

UPDATE public.timeoff
SET note = 'Vacaciones aprobadas que cubren la fecha actual.'
WHERE id IN (4, 5)
  AND note LIKE 'Vacaciones aprobadas que cubren la fecha de hoy (hecho de paridad%';

-- Add only enough active-employee absences to reach the approved visible floor.
WITH active_generated_employees AS (
    SELECT
        e.id,
        row_number() OVER (ORDER BY e.id) AS employee_number,
        count(*) OVER () AS employee_count
    FROM public.employee AS e
    WHERE e.id BETWEEN 101 AND 300
      AND e.deleted_on IS NULL
      AND e.status_id <> 3
), current_count AS (
    SELECT count(*) AS visible_count
    FROM public.timeoff AS t
    JOIN public.employee AS e ON e.id = t.employee_id
    WHERE e.deleted_on IS NULL AND e.status_id <> 3
), id_base AS (
    SELECT COALESCE(max(id), 0)::bigint AS maximum_id
    FROM public.timeoff
), generated AS (
    SELECT
        (id_base.maximum_id + g.n)::integer AS id,
        g.n,
        target.id AS employee_id,
        supervisor.id AS employee_id_supervisor,
        DATE '2025-01-01' + ((g.n * 17) % 730)::integer AS start_date
    FROM current_count
    CROSS JOIN id_base
    CROSS JOIN LATERAL generate_series(
        1::bigint,
        GREATEST(0::bigint, 2000 - current_count.visible_count)) AS g(n)
    JOIN active_generated_employees AS target
      ON target.employee_number = ((g.n - 1) % target.employee_count) + 1
    JOIN active_generated_employees AS supervisor
      ON supervisor.employee_number = ((g.n * 29) % supervisor.employee_count) + 1
)
INSERT INTO public.timeoff (
    id, date_start, date_end, note, status_id, timeoff_type_id, employee_id,
    employee_id_supervisor, date_supervised, supervisor_note, fiscal_year,
    by_hour, created_on)
SELECT
    id,
    start_date::timestamp,
    CASE WHEN n % 13 = 0 THEN NULL
         ELSE (start_date + ((n * 3) % 10)::integer)::timestamp END,
    CASE WHEN n % 11 = 0 THEN NULL
         WHEN n % 7 = 0 THEN left(repeat('Solicitud de ausencia registrada con observaciones para revisión del calendario laboral. ', 24), 1000)
         ELSE CASE n % 6
             WHEN 0 THEN 'Solicitud de ausencia relacionada con asuntos personales.'
             WHEN 1 THEN 'Vacaciones planificadas según el calendario del equipo.'
             WHEN 2 THEN 'Permiso para asistencia a una cita médica.'
             WHEN 3 THEN 'Ausencia para participar en formación profesional.'
             WHEN 4 THEN 'Ajuste de jornada acordado con la persona responsable.'
             ELSE 'Solicitud de ausencia para atender responsabilidades familiares.'
         END END,
    CASE WHEN n % 20 = 0 THEN 3 WHEN n % 17 = 0 THEN 1 ELSE 2 END,
    1 + (n % 6)::integer,
    employee_id,
    employee_id_supervisor,
    CASE WHEN n % 11 = 0 THEN NULL
         ELSE (start_date + ((n * 3) % 10)::integer + 2)::timestamp + interval '10 hours' END,
    CASE WHEN n % 5 = 0 THEN NULL ELSE 'Revisión de la ausencia por el responsable.' END,
    CASE WHEN start_date < DATE '2026-01-01' THEN 2025 ELSE 2026 END,
    (n % 2 = 0),
    start_date::timestamp - ((n % 30) + 5) * interval '1 day' + interval '10 hours'
FROM generated
ON CONFLICT (id) DO NOTHING;

-- Top up the rolling 45-day window without deleting existing punches. Extra
-- events are deterministic, distributed over generated active employees, and
-- use the primary-key maximum so a repeated run on the same day inserts none.
DO $$
DECLARE
    needed bigint;
    maximum_id bigint;
    inserted_count bigint;
BEGIN
    SELECT GREATEST(
        0::bigint,
        20000 - count(*))
    INTO needed
    FROM public.time_check
    WHERE time_check_date_utc >= CURRENT_DATE - 44
      AND time_check_date_utc < CURRENT_DATE + 1;

    IF needed = 0 THEN
        RETURN;
    END IF;

    SELECT COALESCE(max(id), 0)::bigint
    INTO maximum_id
    FROM public.time_check;

    IF maximum_id + needed > 2147483647 THEN
        RAISE EXCEPTION 'Refusing to top up time_check: primary-key range would overflow';
    END IF;

    WITH employees AS (
        SELECT e.id, (e.id % 3 = 0) AS is_tokyo
        FROM public.employee AS e
        WHERE e.id BETWEEN 101 AND 300
          AND e.deleted_on IS NULL
          AND e.status_id <> 3
    ), days AS (
        SELECT k, CURRENT_DATE - k AS work_date
        FROM generate_series(1, 44) AS g(k)
        WHERE extract(isodow FROM CURRENT_DATE - k) BETWEEN 1 AND 5
    ), raw_events AS (
        SELECT
            employee.id AS employee_id,
            employee.is_tokyo,
            days.k,
            days.work_date,
            event.slot,
            event.local_time,
            event.check_type_id,
            event.checkout_type_id,
            CASE WHEN employee.is_tokyo THEN 'Asia/Tokyo' ELSE 'Europe/Madrid' END AS timezone
        FROM employees AS employee
        CROSS JOIN days
        CROSS JOIN LATERAL (VALUES
            (0, TIME '07:20' + ((employee.id + days.k) % 12) * interval '1 minute', 1, 0),
            (1, TIME '10:15' + ((employee.id + days.k) % 8) * interval '1 minute', 2, 1),
            (2, TIME '10:35' + ((employee.id + days.k) % 8) * interval '1 minute', 1, 0),
            (3, TIME '18:10' + ((employee.id + days.k) % 12) * interval '1 minute', 2, 3)
        ) AS event(slot, local_time, check_type_id, checkout_type_id)
    ), stamped AS (
        SELECT
            raw_events.*,
            (work_date + local_time) AT TIME ZONE timezone AS occurred_at
        FROM raw_events
    ), available AS (
        SELECT stamped.*
        FROM stamped
        WHERE NOT EXISTS (
            SELECT 1
            FROM public.time_check AS existing
            WHERE existing.employee_id = stamped.employee_id
              AND existing.time_check_date_utc = stamped.occurred_at)
    ), ranked AS (
        SELECT
            available.*,
            row_number() OVER (
                ORDER BY ((employee_id * 7919 + k * 104729 + slot * 1543) % 2147483629),
                         employee_id, k, slot) AS row_number
        FROM available
    )
    INSERT INTO public.time_check (
        id, employee_id, working_day_id, check_type_id, checkout_type_id,
        time_check_date_utc, time_check_date_long, time_check_timezone,
        check_device_type_id, status_id, supervisor_status_date, employee_note,
        longitude, latitude, ip_address, created_on, modified_on, delay_id,
        photo_face_url, photo_face_status_id)
    SELECT
        (maximum_id + ranked.row_number)::integer,
        ranked.employee_id,
        1 + (ranked.employee_id % 7),
        ranked.check_type_id,
        ranked.checkout_type_id,
        ranked.occurred_at,
        (extract(epoch FROM ranked.occurred_at) * 1000)::bigint,
        ranked.timezone,
        1 + ((ranked.employee_id + ranked.k + ranked.slot) % 3),
        2,
        ranked.occurred_at + interval '5 minutes',
        NULL,
        round((CASE WHEN ranked.is_tokyo THEN 139.6917 ELSE -3.7038 END
            + (((ranked.employee_id * 13 + ranked.k) % 9999)::double precision / 1000000.0))::numeric, 6)::double precision,
        round((CASE WHEN ranked.is_tokyo THEN 35.6895 ELSE 40.4168 END
            + (((ranked.employee_id * 7 + ranked.k) % 9999)::double precision / 1000000.0))::numeric, 6)::double precision,
        '10.' || (ranked.employee_id % 200) || '.' || ((ranked.k * 3) % 250) || '.' || ((ranked.employee_id + ranked.slot) % 250),
        ranked.occurred_at AT TIME ZONE 'UTC',
        (ranked.occurred_at + interval '30 seconds') AT TIME ZONE 'UTC',
        0,
        NULL,
        0
    FROM ranked
    WHERE ranked.row_number <= needed
    ON CONFLICT (id) DO NOTHING;

    GET DIAGNOSTICS inserted_count = ROW_COUNT;
    IF inserted_count < needed THEN
        RAISE EXCEPTION
            'Unable to reach the 20,000-punch minimum: needed %, inserted %',
            needed,
            inserted_count;
    END IF;
END
$$;

SELECT setval(pg_get_serial_sequence('public.timeoff', 'id'), (SELECT max(id) FROM public.timeoff), true);
SELECT setval(pg_get_serial_sequence('public.time_check', 'id'), (SELECT max(id) FROM public.time_check), true);
