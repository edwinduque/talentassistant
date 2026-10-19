-- Grants de solo lectura del conector HCM (SPEC-011 AC-037 / ADR-0012).
-- Aplicable idénticamente en la base local de pruebas y en la base real del
-- HCM: la aplicación solo puede leer las vistas del contrato; ni INSERT,
-- UPDATE ni DELETE sobre ellas, y ningún grant sobre las tablas físicas.

GRANT USAGE ON SCHEMA hcm_contract TO talentassistant_readonly;

GRANT SELECT ON ALL TABLES IN SCHEMA hcm_contract TO talentassistant_readonly;

ALTER DEFAULT PRIVILEGES IN SCHEMA hcm_contract
    GRANT SELECT ON TABLES TO talentassistant_readonly;
