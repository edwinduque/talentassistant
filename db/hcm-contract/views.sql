-- Contrato de vistas del conector HCM (SPEC-011 S1 / ADR-0012).
-- Estas vistas SON la capa anti-corrupción: la aplicación solo conoce este
-- esquema, nunca las tablas físicas. En la base real del HCM se aplican estos
-- mismos cuerpos reescritos sobre el esquema real (o el DBA los reescribe);
-- localmente se aplican sobre las tablas de sustitución de local/.
-- Reglas del contrato:
--   * Solo lectura: los grants de ../grants.sql limitan la app a SELECT.
--   * Idiomas: cada campo localizable se expone como _es y _en; si el HCM solo
--     tiene un idioma, el DBA rellena ambas columnas con el mismo valor (la
--     aplicación no traduce).
--   * Si el HCM no expone un concepto (p. ej. presencia de hoy), se expone
--     derivado EN ESTA VISTA por el lado del HCM: la aplicación nunca deriva
--     reglas de negocio del HCM.
--   * Estados y estados de negocio son datos del origen (texto), no enums
--     decididos por Talent Assistant.

CREATE SCHEMA IF NOT EXISTS hcm_contract;

-- Directorio de empleados (búsqueda por nombre/oficina; payload bilingüe lo
-- declara la aplicación con el nombre disponible).
CREATE OR REPLACE VIEW hcm_contract.talentassistant_employees AS
SELECT
    id            AS employee_id,
    display_name  AS display_name,
    office_ids    AS office_ids
FROM hcm.employees;

-- Presencia por fecha (estados suministrados por el origen: working,
-- not-arrived, vacation, absent).
CREATE OR REPLACE VIEW hcm_contract.talentassistant_presence AS
SELECT
    p.presence_date                        AS presence_date,
    p.employee_id                          AS employee_id,
    e.display_name                         AS employee_display_name,
    e.office_ids                           AS employee_office_ids,
    p.state                                AS state
FROM hcm.presence AS p
JOIN hcm.employees AS e ON e.id = p.employee_id;

-- Solicitudes de ausencia/vacaciones, tal como las declara el origen.
CREATE OR REPLACE VIEW hcm_contract.talentassistant_timeoff AS
SELECT
    reference,
    employee_id,
    type_es,
    type_en,
    status_es,
    status_en,
    starts_on,
    ends_before,
    duration,
    duration_unit_es,
    duration_unit_en,
    summary_es,
    summary_en
FROM hcm.timeoff;

-- Tareas, tal como las declara el origen (el estado es dato del origen).
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

-- Insights de asistencia con la regla ya evaluada por el origen. Los arreglos
-- (punches, evidencia, elementos de revisión) viajan como jsonb con pares
-- description_es/description_en dentro de cada ítem.
CREATE OR REPLACE VIEW hcm_contract.talentassistant_attendance_insights AS
SELECT
    incident_id,
    i.employee_id,
    e.display_name AS employee_display_name,
    e.office_ids   AS employee_office_ids,
    office_id,
    office_country,
    office_city,
    office_iana_time_zone,
    incident_at_utc,
    work_date,
    schedule_starts_at,
    schedule_ends_at,
    punches,
    rule_reference,
    rule_requires_review,
    rule_summary_es,
    rule_summary_en,
    rule_evaluated_at_utc,
    rule_evidence_es,
    rule_evidence_en,
    rule_review_items_es,
    rule_review_items_en,
    related_timeoff_reference,
    related_starts_on,
    related_ends_before,
    related_summary_es,
    related_summary_en,
    related_type_es,
    related_type_en,
    related_status_es,
    related_status_en,
    related_duration,
    related_duration_unit_es,
    related_duration_unit_en
FROM hcm.attendance_incidents AS i
JOIN hcm.employees AS e ON e.id = i.employee_id;
