# Opt-in smoke check for SPEC-022/AC-065: exercise the routing path with a
# hard call budget. The suite and CI never run this script; each call spends
# provider quota. It asserts HTTP 200, a non-empty answer and, for the
# out-of-scope probe, an empty tool list. Provider parity is a manual step:
# force AI__Provider=groq or gemini and compare facts, not wording.
# Credentials are read from .env and never printed. This file stays 100% ASCII
# because Windows PowerShell 5.1 reads it as ANSI without a BOM.
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5101",

    # Hard cap: never spend more than four provider calls.
    [ValidateRange(1, 4)]
    [int]$MaxCalls = 4,

    # Seconds between calls (free tiers rate-limit bursts).
    [ValidateRange(0, 120)]
    [int]$PaceSeconds = 15,

    # Print the plan and exit without logging in or calling the provider.
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$questions = @(
    @{ Text = "Cuantos empleados hay por area?"; RequiresTools = $true },
    @{ Text = "Dime cuantos dias de vacaciones tiene Juan Perez."; RequiresTools = $true },
    @{ Text = "Quien es la proxima persona que cumple anos?"; RequiresTools = $true },
    @{ Text = "dame una funcion python de ordenar numeros"; RequiresTools = $false }
)

$calls = [Math]::Min($MaxCalls, $questions.Count)

if ($DryRun) {
    Write-Host "Dry run: $calls provider call(s) max, ${PaceSeconds}s apart."
    for ($i = 0; $i -lt $calls; $i++) {
        Write-Host ("{0}. {1}" -f ($i + 1), $questions[$i].Text)
    }

    exit 0
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$envPath = Join-Path $repoRoot ".env"
if (-not (Test-Path -LiteralPath $envPath)) {
    throw ".env not found at $envPath. Create it from .env.example with AUTH_SEED_USER and AUTH_SEED_PASSWORD set."
}

$seedUser = $null
$seedPassword = $null
foreach ($line in Get-Content -LiteralPath $envPath) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        switch ($Matches[1]) {
            'AUTH_SEED_USER' { $seedUser = $Matches[2].Trim() }
            'AUTH_SEED_PASSWORD' { $seedPassword = $Matches[2].Trim() }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($seedUser) -or [string]::IsNullOrWhiteSpace($seedPassword)) {
    throw "AUTH_SEED_USER and AUTH_SEED_PASSWORD must be set in .env (their values are not printed)."
}

try {
    $login = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ userName = $seedUser; password = $seedPassword } | ConvertTo-Json) `
        -TimeoutSec 30
    $token = $login.accessToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "The login response did not include an access token."
    }

    for ($i = 0; $i -lt $calls; $i++) {
        $question = $questions[$i]
        $body = @{
            message = $question.Text
            conversationId = "smoke-$([Guid]::NewGuid().ToString('N'))"
        } | ConvertTo-Json

        try {
            $answer = Invoke-RestMethod `
                -Uri "$ApiBaseUrl/chat" `
                -Method Post `
                -Headers @{ Authorization = "Bearer $token" } `
                -ContentType "application/json" `
                -Body $body `
                -TimeoutSec 60
        }
        catch {
            $status = 0
            if ($null -ne $_.Exception.Response) {
                $status = [int]$_.Exception.Response.StatusCode
            }

            if ($status -eq 429 -or $status -eq 503) {
                Write-Host "Provider unavailable on call $($i + 1) (HTTP $status); stopping without retries."
                exit 3
            }

            throw
        }

        $answerText = [string]$answer.answer
        $toolCount = @($answer.toolsUsed).Count
        if ([string]::IsNullOrWhiteSpace($answerText)) {
            Write-Host "Call $($i + 1) returned an empty answer."
            exit 1
        }

        if ($question.RequiresTools -and $toolCount -eq 0) {
            Write-Host "Call $($i + 1) answered without tools."
            exit 1
        }

        if (-not $question.RequiresTools -and $toolCount -gt 0) {
            Write-Host "Call $($i + 1) invoked tools for an out-of-scope request."
            exit 1
        }

        Write-Host ("Call {0}/{1}: HTTP 200, answerLength={2}, toolsUsed={3}" -f ($i + 1), $calls, $answerText.Length, $toolCount)
        if ($i -lt ($calls - 1) -and $PaceSeconds -gt 0) {
            Start-Sleep -Seconds $PaceSeconds
        }
    }
}
catch {
    Write-Host "Smoke check failed: $($_.Exception.Message)"
    exit 1
}

exit 0
