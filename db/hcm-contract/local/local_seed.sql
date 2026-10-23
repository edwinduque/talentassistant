-- Semilla SINTÉTICA local de las tablas de sustitución que el shape real aún
-- no expone (SPEC-012 S3): TAREAS + INSIGHT de asistencia. Personas, presencia
-- y ausencias salen de db/hcm-real/seed.sql (tabla real). Nunca contiene
-- datos reales. Empleados mínimos: solo los que las FK necesitan (tareas de
-- Mauricio y Juan; incidente de Juan).

INSERT INTO hcm.employees (id, display_name, office_ids) VALUES
    ('employee-demo-juan',    'Juan Pérez',         '{"office-demo-madrid"}'),
    ('employee-demo-mauricio','Mauricio Cardenas',  '{"office-demo-madrid"}')
ON CONFLICT (id) DO NOTHING;

-- Tareas (4, idénticas a BuildTasks).
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
