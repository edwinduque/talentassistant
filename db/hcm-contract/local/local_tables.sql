-- Tablas locales de sustitución SOLO para desarrollo y pruebas (SPEC-011 S1).
-- Reproducen de forma representativa las tablas del HCM real; en la base real
-- del HCM solo se aplican los archivos ../views.sql y ../grants.sql reescritos
-- sobre las tablas y esquemas propios del HCM (descubrimiento en la validación
-- contra la base real). Nunca se cargan datos reales aquí: la semilla es
-- íntegramente sintética.

CREATE SCHEMA IF NOT EXISTS hcm;

CREATE TABLE IF NOT EXISTS hcm.employees (
    id            text PRIMARY KEY,
    display_name  text NOT NULL,
    office_ids    text[] NOT NULL DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS hcm.presence (
    presence_date date NOT NULL,
    employee_id   text NOT NULL REFERENCES hcm.employees (id),
    state         text NOT NULL CHECK (state IN ('working', 'not-arrived', 'vacation', 'absent')),
    PRIMARY KEY (presence_date, employee_id)
);

CREATE TABLE IF NOT EXISTS hcm.timeoff (
    reference        text PRIMARY KEY,
    employee_id      text NOT NULL REFERENCES hcm.employees (id),
    type_es          text NOT NULL,
    type_en          text NOT NULL,
    status_es        text NOT NULL,
    status_en        text NOT NULL,
    starts_on        date NOT NULL,
    ends_before      date,
    duration         text,
    duration_unit_es text,
    duration_unit_en text,
    summary_es       text,
    summary_en       text
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
    related_timeoff_reference text REFERENCES hcm.timeoff (reference),
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
