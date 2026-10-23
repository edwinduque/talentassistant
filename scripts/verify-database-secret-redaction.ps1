$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
$sdkVersion = & $dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne "10.0.402") {
    throw "Run this check with the repository-pinned .NET SDK 10.0.402."
}

$listener = Get-NetTCPConnection -LocalPort 5101 -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    throw "Port 5101 is already in use. Stop the local API yourself, then retry."
}

$tempRoot = Join-Path $env:LOCALAPPDATA "Temp\opencode"
if (-not (Test-Path -LiteralPath $tempRoot)) {
    $tempRoot = [System.IO.Path]::GetTempPath()
}

$runId = [Guid]::NewGuid().ToString("N")
$stdoutPath = Join-Path $tempRoot "talentassistant-secret-check-$runId.stdout.log"
$stderrPath = Join-Path $tempRoot "talentassistant-secret-check-$runId.stderr.log"
$sentinel = "synthetic-secret-sentinel-$runId"
$previousEnvironment = @{
    ASPNETCORE_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
    ConnectionStrings__TalentAssistant = $env:ConnectionStrings__TalentAssistant
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5101"
$env:ConnectionStrings__TalentAssistant = "Host=127.0.0.1;Port=5434;Database=talentassistant_dev;Username=talentassistant_sentinel;Password=$sentinel;"

$process = $null
$responseBody = ""
$responseStatus = 0
$apiLogs = ""

try {
    $process = Start-Process `
        -FilePath $dotnet `
        -ArgumentList "run --no-build --no-restore --no-launch-profile --project src/TalentAssistant.Api/TalentAssistant.Api.csproj" `
        -WorkingDirectory $repoRoot `
        -PassThru `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden

    $null = Get-Command curl.exe -ErrorAction Stop
    $healthReady = $false

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) {
            break
        }

        try {
            $healthOutput = & curl.exe --noproxy "*" --silent --show-error --max-time 2 `
                --write-out "`nHTTP_STATUS=%{http_code}" "http://127.0.0.1:5101/health" 2>$null
            $healthResponse = [string]::Join([Environment]::NewLine, [string[]]@($healthOutput))
            $healthExitCode = $LASTEXITCODE
            $healthMatch = [regex]::Match($healthResponse, "HTTP_STATUS=(\d{3})\s*$")
            if ($healthExitCode -eq 0 -and $healthMatch.Success -and $healthMatch.Groups[1].Value -eq "200") {
                $healthReady = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    if (-not $healthReady) {
        throw "The local API did not become ready; output is intentionally withheld."
    }

    $responseOutput = & curl.exe --noproxy "*" --silent --show-error --max-time 10 `
        --write-out "`nHTTP_STATUS=%{http_code}" "http://127.0.0.1:5101/health/database" 2>$null
    $response = [string]::Join([Environment]::NewLine, [string[]]@($responseOutput))
    $curlExitCode = $LASTEXITCODE
    $statusMatch = [regex]::Match($response, "HTTP_STATUS=(\d{3})\s*$")
    if ($statusMatch.Success) {
        $responseStatus = [int]$statusMatch.Groups[1].Value
        $responseBody = $response.Substring(0, $statusMatch.Index).TrimEnd()
    }
    if ($curlExitCode -ne 0 -or -not $statusMatch.Success) {
        throw "The database diagnostic request did not complete; response details are withheld."
    }
}
finally {
    if ($process -and -not $process.HasExited) {
        & (Join-Path $env:WINDIR "System32\taskkill.exe") /PID $process.Id /T /F 2>$null | Out-Null
        $process.WaitForExit()
    }

    if (Test-Path -LiteralPath $stdoutPath) {
        $apiLogs += Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $stderrPath) {
        $apiLogs += Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue
    }

    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], "Process")
    }

    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
}

$sentinelLeaked = $responseBody.Contains($sentinel) -or $apiLogs.Contains($sentinel)
$stackTraceLeaked = $responseBody -match "(?m)^\s+at .+ in .+\.cs:line \d+" `
    -or $apiLogs -match "(?m)^\s+at .+ in .+\.cs:line \d+"
$expectedFailure = $responseStatus -eq 503 `
    -and $responseBody.Contains("Database unavailable") `
    -and $apiLogs.Contains("Database readiness check failed")

if ($sentinelLeaked) {
    Write-Error "FAIL: the synthetic sentinel appeared in captured output; values were withheld."
    exit 1
}

if ($stackTraceLeaked) {
    Write-Error "FAIL: a stack trace appeared in captured output; values were withheld."
    exit 1
}

if (-not $expectedFailure) {
    Write-Error "FAIL: expected a controlled HTTP 503 database error without secret details. Captured values were withheld."
    exit 1
}

Write-Output "PASS: synthetic authentication failure returned a generic HTTP 503; sentinel and stack trace were absent from the response and captured API logs."
