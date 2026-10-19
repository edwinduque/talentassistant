-- Roles del contrato (SPEC-011 / ADR-0012). Idempotente; sin contraseñas en
-- el archivo: localmente la contraseña del rol de aplicación la fija el init
-- desde la variable de entorno, y en la base real la fija el DBA.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'talentassistant_readonly') THEN
        CREATE ROLE talentassistant_readonly NOLOGIN;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'talentassistant_hcm_app') THEN
        CREATE ROLE talentassistant_hcm_app LOGIN;
    END IF;
END
$$;

GRANT talentassistant_readonly TO talentassistant_hcm_app;
