-- Tablas de sustitución LOCALES reducidas (SPEC-012 S3): desde el recableado,
-- las personas, presencia y ausencias se sirven desde las tablas reales
-- (db/hcm-real/schema.sql). Esta sustitución conserva SOLO lo que el shape
-- real todavía no expone: TAREAS y el INSIGHT de asistencia (gaps de SPEC-012
-- §2 y cabecera de views.sql). Nunca se cargan datos reales aquí.

CREATE SCHEMA IF NOT EXISTS hcm;

-- Mínimo de empleados que las FK de tareas/incidentes necesitan (los fixtures
-- de esas dos entidades: Juan y Mauricio). La sustitución de personas ya no
-- existe: la vista de directorio lee public.employee.
CREATE TABLE IF NOT EXISTS hcm.employees (
    id            text PRIMARY KEY,
    display_name  text NOT NULL,
    office_ids    text[] NOT NULL DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS hcm.tasks (
    reference   text PRIMARY KEY,
    employee_id text NOT NULL REFERENCES hcm.employees (id),
    title_es    text NOT NULL,
    title_en    text NOT NULL,
    status_es   text NOT NULL,
    status_en   text NOT NULL,
    due_on      date,
    summary_es  text,
    summary_en  text
);

-- Forma ensamblada del insight de asistencia: la regla ya viene evaluada por el
-- origen (las reglas de negocio pertenecen al HCM; la aplicación no las deriva).
-- Sin FK a ausencias: la referencia de la ausencia relacionada es texto de
-- presentación y la sustitución de timeoff ya no existe.
CREATE TABLE IF NOT EXISTS hcm.attendance_incidents (
    incident_id            text PRIMARY KEY,
    employee_id            text NOT NULL REFERENCES hcm.employees (id),
    office_id              text NOT NULL,
    office_country         text NOT NULL,
    office_city            text NOT NULL,
    office_iana_time_zone  text NOT NULL,
    incident_at_utc        timestamptz NOT NULL,
    work_date              date NOT NULL,
    schedule_starts_at     time NOT NULL,
    schedule_ends_at       time NOT NULL,
    punches                jsonb NOT NULL DEFAULT '[]',
    rule_reference         text NOT NULL,
    rule_requires_review   boolean NOT NULL,
    rule_summary_es        text NOT NULL,
    rule_summary_en        text NOT NULL,
    rule_evaluated_at_utc  timestamptz NOT NULL,
    rule_evidence_es       jsonb NOT NULL DEFAULT '[]',
    rule_evidence_en       jsonb NOT NULL DEFAULT '[]',
    rule_review_items_es   jsonb NOT NULL DEFAULT '[]',
    rule_review_items_en   jsonb NOT NULL DEFAULT '[]',
    related_timeoff_reference text,
    related_starts_on      date,
    related_ends_before    date,
    related_summary_es     text,
    related_summary_en     text,
    related_type_es        text,
    related_type_en        text,
    related_status_es      text,
    related_status_en      text,
    related_duration       text,
    related_duration_unit_es text,
    related_duration_unit_en  text
);
