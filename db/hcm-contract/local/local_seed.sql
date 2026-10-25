-- Semilla SINTÉTICA local de las tablas de sustitución que el shape real aún
-- no expone (SPEC-012 S3): TAREAS + INSIGHT de asistencia. Personas, presencia
-- y ausencias salen de db/hcm-real/seed.sql (tabla real). Nunca contiene
-- datos reales. Todos los empleados activos del contrato se cargan en la tabla
-- referenciada por tareas/insights; el HCM real-shape no tiene tabla de tareas.

INSERT INTO hcm.employees (id, display_name, office_ids)
SELECT employee_id, display_name, office_ids
FROM hcm_contract.talentassistant_employees
ON CONFLICT (id) DO UPDATE
SET display_name = EXCLUDED.display_name,
    office_ids = EXCLUDED.office_ids;

-- Cuatro tareas de fixture idénticas a BuildTasks conservan la paridad sintética.
INSERT INTO hcm.tasks (
    reference, employee_id, title_es, title_en, status_es, status_en,
    due_on, summary_es, summary_en)
VALUES
    ('task-mauricio-payroll', 'employee-demo-mauricio',
     'Preparar el informe de nómina de octubre', 'Prepare the October payroll report',
     'Pendiente', 'Pending', DATE '2026-10-20',
     'El HCM sintético asigna esta tarea a Mauricio Cardenas.',
     'The synthetic HCM assigns this task to Mauricio Cardenas.'),
    ('task-mauricio-onboarding', 'employee-demo-mauricio',
     'Revisar el alta de nuevas incorporaciones', 'Review new hire onboarding',
     'En curso', 'In progress', DATE '2026-10-22', NULL, NULL),
    ('task-mauricio-archive', 'employee-demo-mauricio',
     'Archivar la documentación del trimestre', 'Archive the quarterly documentation',
     'Completado', 'Completed', DATE '2026-10-10', NULL, NULL),
    ('task-juan-timesheet', 'employee-demo-juan',
     'Validar los marcajes de la semana', 'Validate the week''s punch records',
     'Pendiente', 'Pending', DATE '2026-10-21', NULL, NULL)
ON CONFLICT (reference) DO NOTHING;

-- Tareas operativas ficticias para empleados generados activos. References y
-- contenido son deterministas; 1.996 filas más las cuatro fixtures alcanzan
-- el mínimo de 2.000 tareas sin duplicar al re-ejecutar esta semilla.
WITH active_employees AS (
    SELECT
        employee_id,
        row_number() OVER (ORDER BY employee_id) AS employee_number,
        count(*) OVER () AS employee_count
    FROM hcm_contract.talentassistant_employees
    WHERE employee_id LIKE 'employee-%'
      AND employee_id NOT LIKE 'employee-demo-%'
), titles AS (
    SELECT *
    FROM (VALUES
        (1, 'Validar incidencias de marcajes', 'Review timekeeping exceptions'),
        (2, 'Preparar el resumen semanal de ausencias', 'Prepare the weekly absence summary'),
        (3, 'Revisar las solicitudes pendientes', 'Review pending leave requests'),
        (4, 'Confirmar la cobertura de turnos', 'Confirm shift coverage'),
        (5, 'Actualizar la documentación de incorporación', 'Update onboarding documentation'),
        (6, 'Conciliar los registros de asistencia', 'Reconcile attendance records'),
        (7, 'Revisar la planificación de vacaciones', 'Review vacation planning'),
        (8, 'Completar la revisión del expediente', 'Complete the personnel file review'),
        (9, 'Coordinar la formación de seguridad', 'Coordinate safety training'),
        (10, 'Comprobar cambios de asignación', 'Check assignment changes'),
        (11, 'Preparar el cierre mensual de jornada', 'Prepare the monthly timekeeping close'),
        (12, 'Resolver diferencias del calendario laboral', 'Resolve work-calendar differences')
    ) AS task_title(number, title_es, title_en)
)
INSERT INTO hcm.tasks (
    reference, employee_id, title_es, title_en, status_es, status_en,
    due_on, summary_es, summary_en)
SELECT
    'task-demo-' || lpad(g.n::text, 6, '0'),
    employee.employee_id,
    title.title_es,
    title.title_en,
    CASE WHEN g.n % 9 = 0 THEN 'Completado'
         WHEN g.n % 4 = 0 THEN 'En curso'
         ELSE 'Pendiente' END,
    CASE WHEN g.n % 9 = 0 THEN 'Completed'
         WHEN g.n % 4 = 0 THEN 'In progress'
         ELSE 'Pending' END,
    CASE WHEN g.n % 9 = 0 THEN CURRENT_DATE - 1 - (g.n % 90)
         WHEN g.n % 4 = 0 THEN CURRENT_DATE + (g.n % 30)
         ELSE CURRENT_DATE + (g.n % 90) END,
    CASE WHEN g.n % 6 = 0 THEN 'Revisión asignada al flujo operativo.' ELSE NULL END,
    CASE WHEN g.n % 6 = 0 THEN 'Review assigned to the operational workflow.' ELSE NULL END
FROM generate_series(1, 1996) AS g(n)
JOIN active_employees AS employee
  ON employee.employee_number = ((g.n - 1) % employee.employee_count) + 1
JOIN titles AS title
  ON title.number = ((g.n - 1) % 12) + 1
ON CONFLICT (reference) DO NOTHING;

-- Insight de asistencia (1 incidente con regla evaluada por el origen; fechas
-- fijas para pruebas con rangos explícitos; la ausencia relacionada es texto).
INSERT INTO hcm.attendance_incidents (
    incident_id, employee_id, office_id, office_country, office_city,
    office_iana_time_zone, incident_at_utc, work_date, schedule_starts_at,
    schedule_ends_at, punches, rule_reference, rule_requires_review,
    rule_summary_es, rule_summary_en, rule_evaluated_at_utc,
    rule_evidence_es, rule_evidence_en, rule_review_items_es, rule_review_items_en,
    related_timeoff_reference, related_starts_on, related_ends_before,
    related_summary_es, related_summary_en, related_type_es, related_type_en,
    related_status_es, related_status_en, related_duration,
    related_duration_unit_es, related_duration_unit_en)
VALUES (
    'incident-2026-10-13-juan-late', 'employee-demo-juan', 'office-madrid',
     'España', 'Spain', 'Europe/Madrid',
     TIMESTAMPTZ '2026-10-13 06:52:00+00', DATE '2026-10-13',
     TIME '09:00', TIME '17:00',
     '[{"occurred_at_utc":"2026-10-13T06:52:00Z","description_es":"Entrada","description_en":"Clock-in"}]'::jsonb,
     'arrival-after-schedule', TRUE,
     'Llegada después del inicio de jornada.', 'Arrival after the shift start.',
     TIMESTAMPTZ '2026-10-13 06:57:00+00',
     '["Fichaje de entrada a las 06:52 UTC","Horario de jornada 09:00-17:00"]'::jsonb,
     '["Clock-in at 06:52 UTC","Work schedule 09:00-17:00"]'::jsonb,
     '["Confirmar motivo de la llegada tardía"]'::jsonb,
     '["Confirm the reason for the late arrival"]'::jsonb,
     'timeoff-juan-2026-10', DATE '2026-10-13', DATE '2026-10-16',
     'El HCM sintético informa una ausencia de vacaciones aprobada de Juan Pérez.',
     'The synthetic HCM reports an approved vacation absence for Juan Pérez.',
     'Vacación', 'Vacation', 'Aprobado', 'Approved', '3',
     'día laborable', 'workday')
ON CONFLICT (incident_id) DO NOTHING;
