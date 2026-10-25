# Regression probe for the fictional HCM demo upgrade bundle.
# Creates disposable databases inside the local hcm-postgres container only.
# It never connects to the VPS and never prints credentials or employee rows.
[CmdletBinding()]
param(
    [string]$ContainerName = "talentassistant-local-hcm-postgres-1",
    [string]$AdminUser = "talentassistant_hcm_admin"
)

$ErrorActionPreference = "Continue"
$runId = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$upgradeDb = "hcm_demo_upgrade_$runId"
$negativeDb = "hcm_demo_negative_$runId"
$containerScratch = "/tmp/hcm-demo-loader-$runId"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$createdDatabases = New-Object System.Collections.Generic.List[string]
$scratchCopied = $false

function Invoke-ContainerCommand {
    param([string[]]$Arguments)

    $output = & docker exec $ContainerName @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    return [PSCustomObject]@{ ExitCode = $exitCode; Output = $output }
}

function Invoke-PsqlFile {
    param([string]$Database, [string]$Path)

    Invoke-ContainerCommand @(
        "psql", "-X", "-U", $AdminUser, "-d", $Database,
        "-v", "ON_ERROR_STOP=1", "-f", $Path)
}

function Invoke-PsqlText {
    param([string]$Database, [string]$Sql)

    $output = $Sql | & docker exec -i $ContainerName psql -X -t -A -U $AdminUser -d $Database -v ON_ERROR_STOP=1 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    return [PSCustomObject]@{ ExitCode = $exitCode; Output = $output }
}

function Assert-PsqlFile {
    param([string]$Database, [string]$Path)

    $result = Invoke-PsqlFile -Database $Database -Path $Path
    if ($result.ExitCode -ne 0) {
        throw "SQL file failed in disposable database '$Database': $Path`n$($result.Output)"
    }
}

function New-InitializedDatabase {
    param([string]$Database)

    $created = Invoke-ContainerCommand @("createdb", "-U", $AdminUser, $Database)
    if ($created.ExitCode -ne 0) {
        throw "Could not create disposable database '$Database': $($created.Output)"
    }
    $createdDatabases.Add($Database)

    foreach ($file in @(
        "/real/schema.sql",
        "/real/seed.sql",
        "/contract/local/local_tables.sql",
        "/contract/views.sql",
        "/contract/local/local_seed.sql",
        "/contract/roles.sql",
        "/contract/grants.sql",
        "/real/verify-demo-data.sql"
    )) {
        Assert-PsqlFile -Database $Database -Path $file
    }
}

function Get-Counts {
    param([string]$Database)

    $query = @"
SELECT
  (SELECT count(*) FROM public.employee)::text || '/' ||
  (SELECT count(*) FROM hcm_contract.talentassistant_employees)::text || '/' ||
  (SELECT count(*) FROM hcm_contract.talentassistant_timeoff)::text || '/' ||
  (SELECT count(*) FROM public.time_check
   WHERE time_check_date_utc >= CURRENT_DATE - 44
     AND time_check_date_utc < CURRENT_DATE + 1)::text || '/' ||
  (SELECT count(*) FROM hcm_contract.talentassistant_tasks)::text;
"@
    $result = Invoke-PsqlText -Database $Database -Sql $query
    if ($result.ExitCode -ne 0) {
        throw "Could not read counts from disposable database '$Database': $($result.Output)"
    }
    return $result.Output.Trim()
}

function Get-Snapshot {
    param([string]$Database)

    $query = @'
WITH snapshots AS (
    SELECT 'employee' AS name, count(*) AS row_count,
           md5(coalesce(string_agg(md5(row_to_json(e)::text), '' ORDER BY e.id), '')) AS digest
    FROM public.employee AS e
    UNION ALL
    SELECT 'timeoff', count(*),
           md5(coalesce(string_agg(md5(row_to_json(t)::text), '' ORDER BY t.id), ''))
    FROM public.timeoff AS t
    UNION ALL
    SELECT 'time_check', count(*),
           md5(coalesce(string_agg(md5(row_to_json(t)::text), '' ORDER BY t.id), ''))
    FROM public.time_check AS t
    UNION ALL
    SELECT 'hcm_employees', count(*),
           md5(coalesce(string_agg(md5(row_to_json(e)::text), '' ORDER BY e.id), ''))
    FROM hcm.employees AS e
    UNION ALL
    SELECT 'hcm_tasks', count(*),
           md5(coalesce(string_agg(md5(row_to_json(t)::text), '' ORDER BY t.reference), ''))
    FROM hcm.tasks AS t
)
SELECT string_agg(name || ':' || row_count || ':' || digest, E'\n' ORDER BY name)
FROM snapshots;
'@
    $result = Invoke-PsqlText -Database $Database -Sql $query
    if ($result.ExitCode -ne 0) {
        throw "Could not snapshot disposable database '$Database': $($result.Output)"
    }
    return $result.Output.Trim()
}

try {
    $containerCheck = Invoke-ContainerCommand @("test", "-d", "/real")
    if ($containerCheck.ExitCode -ne 0) {
        throw "Container '$ContainerName' is unavailable or missing the /real mount."
    }

    New-InitializedDatabase -Database $upgradeDb

    # Model the already-initialized older VPS in disposable state: old labels,
    # NULL surnames, legacy absence/punch/task volumes, and the three-column view.
    $legacyState = @'
BEGIN;
UPDATE public.employee SET name='Empleado', last_name=U&'sint\00E9tico 001' WHERE id=5;
UPDATE public.employee SET name='Empleado', last_name=U&'sint\00E9tico 005' WHERE id=6;
UPDATE public.employee SET name='Empleado', last_name=U&'sint\00E9tico 004' WHERE id=7;
UPDATE public.employee SET name='Empleado', last_name=U&'sint\00E9tico 002' WHERE id=8;
UPDATE public.employee SET name='Empleado', last_name=U&'sint\00E9tico 003' WHERE id=9;
UPDATE public.employee SET last_name=NULL
WHERE id BETWEEN 101 AND 300 AND ((id - 100) * 7) % 20 >= 10;

WITH inactive AS (
    SELECT id FROM public.employee
    WHERE status_id = 3 AND deleted_on IS NOT NULL
    ORDER BY id LIMIT 1
), targets AS (
    SELECT t.id
    FROM public.timeoff AS t
    JOIN public.employee AS e ON e.id = t.employee_id
    WHERE e.deleted_on IS NULL AND e.status_id <> 3
      AND t.employee_id BETWEEN 101 AND 300
    ORDER BY t.id DESC
    LIMIT 1417
)
UPDATE public.timeoff AS t
SET employee_id = inactive.id
FROM inactive, targets
WHERE t.id = targets.id;

WITH targets AS (
    SELECT id
    FROM public.time_check
    WHERE employee_id BETWEEN 101 AND 300
      AND time_check_date_utc >= CURRENT_DATE - 44
      AND time_check_date_utc < CURRENT_DATE + 1
    ORDER BY id DESC
    LIMIT 1494
)
UPDATE public.time_check AS t
SET time_check_date_utc = (CURRENT_DATE - 46)::timestamp AT TIME ZONE 'UTC',
    time_check_date_long = (extract(epoch FROM ((CURRENT_DATE - 46)::timestamp AT TIME ZONE 'UTC')) * 1000)::bigint
FROM targets
WHERE t.id = targets.id;

-- This DELETE is isolated to the disposable verification database.
DELETE FROM hcm.tasks WHERE reference LIKE 'task-demo-%';

DROP VIEW hcm_contract.talentassistant_employees;
CREATE VIEW hcm_contract.talentassistant_employees AS
SELECT
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
        ELSE 'employee-' || e.id::text
    END AS employee_id,
    coalesce(nullif(trim(concat(e.name, ' ', e.last_name)), ''), 'employee-' || e.id::text) AS display_name,
    array_remove(ARRAY[e.department]::text[], NULL) AS office_ids
FROM public.employee AS e
WHERE e.deleted_on IS NULL AND e.status_id <> 3;
COMMIT;
'@
    $legacyResult = Invoke-PsqlText -Database $upgradeDb -Sql $legacyState
    if ($legacyResult.ExitCode -ne 0) {
        throw "Could not prepare the disposable legacy state: $($legacyResult.Output)"
    }
    $legacyCounts = Get-Counts -Database $upgradeDb
    if ($legacyCounts -ne "209/197/583/18506/4") {
        throw "Legacy-state fixture did not reach expected counts; got $legacyCounts."
    }

    $bundleDirectory = Join-Path $repoRoot "db\hcm-contract"
    $realDirectory = Join-Path $repoRoot "db\hcm-real"
    $null = docker exec $ContainerName mkdir -p $containerScratch
    if ($LASTEXITCODE -ne 0) { throw "Could not create container scratch '$containerScratch'." }
    $null = docker cp $bundleDirectory "${ContainerName}:$containerScratch/"
    if ($LASTEXITCODE -ne 0) { throw "Could not copy the HCM contract files to scratch." }
    $null = docker cp $realDirectory "${ContainerName}:$containerScratch/"
    if ($LASTEXITCODE -ne 0) { throw "Could not copy the HCM real-shape files to scratch." }
    $scratchCopied = $true

    $bundle = "$containerScratch/hcm-contract/vps-demo-upgrade.psql"
    $firstRun = Invoke-PsqlFile -Database $upgradeDb -Path $bundle
    if ($firstRun.ExitCode -ne 0) {
        throw "First upgrade-bundle run failed: $($firstRun.Output)"
    }
    $firstCounts = Get-Counts -Database $upgradeDb
    if ($firstCounts -ne "209/197/2000/20000/2000") {
        throw "First upgrade-bundle run returned unexpected counts: $firstCounts."
    }
    $firstSnapshot = Get-Snapshot -Database $upgradeDb

    $secondRun = Invoke-PsqlFile -Database $upgradeDb -Path $bundle
    if ($secondRun.ExitCode -ne 0) {
        throw "Second upgrade-bundle run failed: $($secondRun.Output)"
    }
    $secondCounts = Get-Counts -Database $upgradeDb
    $secondSnapshot = Get-Snapshot -Database $upgradeDb
    if ($secondCounts -ne $firstCounts -or $secondSnapshot -ne $firstSnapshot) {
        throw "Second bundle run changed data: before='$firstCounts', after='$secondCounts'."
    }

    New-InitializedDatabase -Database $negativeDb
    $negativeUpdate = Invoke-PsqlText -Database $negativeDb -Sql "UPDATE public.employee SET first_email='changed@invalid.test' WHERE id=101;"
    if ($negativeUpdate.ExitCode -ne 0) {
        throw "Could not prepare the negative preflight case: $($negativeUpdate.Output)"
    }
    $negativeBefore = Get-Snapshot -Database $negativeDb
    $rejected = Invoke-PsqlFile -Database $negativeDb -Path $bundle
    if ($rejected.ExitCode -eq 0 -or $rejected.Output -notmatch "Refusing to modify HCM rows") {
        throw "Bundle did not refuse the mismatched fictional signature: $($rejected.Output)"
    }
    $negativeAfter = Get-Snapshot -Database $negativeDb
    if ($negativeAfter -ne $negativeBefore) {
        throw "Rejected preflight changed data in the disposable database."
    }

    Write-Output "PASS: legacy counts=$legacyCounts; upgraded counts=$firstCounts; second run identical; mismatched seed refused without mutation."
}
finally {
    if ($scratchCopied) {
        $null = docker exec $ContainerName rm -rf $containerScratch
        if ($LASTEXITCODE -ne 0) { Write-Error "Could not remove container scratch '$containerScratch'." }
    }
    foreach ($database in @($upgradeDb, $negativeDb)) {
        if ($createdDatabases.Contains($database)) {
            $null = docker exec $ContainerName dropdb -U $AdminUser $database
            if ($LASTEXITCODE -ne 0) { Write-Error "Could not drop disposable database '$database'." }
        }
    }
}
