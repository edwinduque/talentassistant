-- Check-only guard for the production-like fictional HCM dataset.
-- It reports counts only; it never emits employee or task details.
SET TIME ZONE 'UTC';

DO $$
DECLARE
    total_employee_count bigint;
    active_employee_count bigint;
    distinct_visible_names bigint;
    active_without_gender bigint;
    active_without_manager bigint;
    unresolved_manager_count bigint;
    distinct_area_count bigint;
    incomplete_name_count bigint;
    placeholder_name_count bigint;
    visible_timeoff_count bigint;
    recent_time_check_count bigint;
    visible_task_count bigint;
    orphan_task_count bigint;
    failures text[] := ARRAY[]::text[];
BEGIN
    SELECT count(*)
    INTO total_employee_count
    FROM public.employee;

    SELECT count(*)
    INTO active_employee_count
    FROM hcm_contract.talentassistant_employees;

    -- El dataset realista exige nombres completos únicos en los 197 activos
    -- (200 generados + 9 fixtures menos los 12 borrados), género y jefe en
    -- todos los activos salvo el máximo responsable.
    SELECT count(DISTINCT display_name)
    INTO distinct_visible_names
    FROM hcm_contract.talentassistant_employees;

    SELECT count(*)
    INTO active_without_gender
    FROM hcm_contract.talentassistant_employees
    WHERE gender IS NULL OR gender NOT IN ('F', 'M');

    SELECT count(*)
    INTO active_without_manager
    FROM hcm_contract.talentassistant_employees
    WHERE manager_employee_id IS NULL;

    SELECT count(*)
    INTO unresolved_manager_count
    FROM hcm_contract.talentassistant_employees AS employee
    LEFT JOIN hcm_contract.talentassistant_employees AS manager
        ON manager.employee_id = employee.manager_employee_id
    WHERE employee.manager_employee_id IS NOT NULL
      AND manager.employee_id IS NULL;

    SELECT count(DISTINCT area)
    INTO distinct_area_count
    FROM hcm_contract.talentassistant_employees
    WHERE area IS NOT NULL;

    SELECT count(*)
    INTO incomplete_name_count
    FROM public.employee
    WHERE deleted_on IS NULL
      AND status_id <> 3
      AND (name IS NULL OR btrim(name) = '' OR last_name IS NULL OR btrim(last_name) = '');

    SELECT count(*)
    INTO placeholder_name_count
    FROM hcm_contract.talentassistant_employees
    WHERE display_name LIKE 'Empleado sintético %';

    SELECT count(*)
    INTO visible_timeoff_count
    FROM hcm_contract.talentassistant_timeoff;

    SELECT count(*)
    INTO recent_time_check_count
    FROM public.time_check
    WHERE time_check_date_utc >= CURRENT_DATE - 44
      AND time_check_date_utc < CURRENT_DATE + 1;

    SELECT count(*)
    INTO visible_task_count
    FROM hcm_contract.talentassistant_tasks;

    SELECT count(*)
    INTO orphan_task_count
    FROM hcm_contract.talentassistant_tasks AS task
    LEFT JOIN hcm_contract.talentassistant_employees AS employee
        ON employee.employee_id = task.employee_id
    WHERE employee.employee_id IS NULL;

    IF total_employee_count <> 209 THEN
        failures := array_append(failures, format('employees=%s (expected 209)', total_employee_count));
    END IF;
    IF active_employee_count <> 197 THEN
        failures := array_append(failures, format('active_employees=%s (expected 197)', active_employee_count));
    END IF;
    IF distinct_visible_names <> 197 THEN
        failures := array_append(failures, format('distinct_display_names=%s (expected 197 unique)', distinct_visible_names));
    END IF;
    IF active_without_gender <> 0 THEN
        failures := array_append(failures, format('active_employees_without_gender=%s', active_without_gender));
    END IF;
    IF active_without_manager <> 1 THEN
        failures := array_append(failures, format('active_employees_without_manager=%s (expected 1)', active_without_manager));
    END IF;
    IF unresolved_manager_count <> 0 THEN
        failures := array_append(failures, format('unresolved_manager_references=%s', unresolved_manager_count));
    END IF;
    IF distinct_area_count < 10 THEN
        failures := array_append(failures, format('distinct_areas=%s (minimum 10)', distinct_area_count));
    END IF;
    IF incomplete_name_count <> 0 THEN
        failures := array_append(failures, format('active_employees_without_full_name=%s', incomplete_name_count));
    END IF;
    IF placeholder_name_count <> 0 THEN
        failures := array_append(failures, format('placeholder_display_names=%s', placeholder_name_count));
    END IF;
    IF visible_timeoff_count < 2000 THEN
        failures := array_append(failures, format('visible_timeoff=%s (minimum 2000)', visible_timeoff_count));
    END IF;
    IF recent_time_check_count < 20000 THEN
        failures := array_append(failures, format('time_checks_last_45_days=%s (minimum 20000)', recent_time_check_count));
    END IF;
    IF visible_task_count < 2000 THEN
        failures := array_append(failures, format('visible_tasks=%s (minimum 2000)', visible_task_count));
    END IF;
    IF orphan_task_count <> 0 THEN
        failures := array_append(failures, format('tasks_without_active_employee=%s', orphan_task_count));
    END IF;

    IF cardinality(failures) > 0 THEN
        RAISE EXCEPTION 'Production-like fictional HCM dataset validation failed: %',
            array_to_string(failures, '; ');
    END IF;

    RAISE NOTICE 'Validated fictional HCM demo dataset: employees=%, active=%, distinct_names=%, areas=%, timeoff=%, time_checks_45d=%, tasks=%',
        total_employee_count,
        active_employee_count,
        distinct_visible_names,
        distinct_area_count,
        visible_timeoff_count,
        recent_time_check_count,
        visible_task_count;
END
$$;
