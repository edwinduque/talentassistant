# Live check for SPEC-009 AC-029/AC-032: the ten reference questions from
# SPEC-008 section 4 plus the eleventh disambiguation probe must reach
# POST /chat and come back with a non-empty answer that cites the expected
# tool; the eleventh must list both candidate Juans and ask which one.
# Requires the local API plus a real AI_API_KEY in .env (parent task runs
# the live pass; without the key the script exits with a clear
# "AI key/quota missing" message).
# Credentials are read from .env and never printed.
# This file stays 100% ASCII: Windows PowerShell 5.1 reads it as ANSI without
# a BOM, so accented literals would be corrupted (regexes use \w instead).
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5101",

    # Seconds to wait between questions (the provider rate-limits bursts;
    # a paced full pass stays inside the window).
    [int]$PaceSeconds = 0,

    # 1-based index to run a single reference question (0 = run all eleven).
    [int]$Only = 0
)

$ErrorActionPreference = "Stop"

# The answers are localized; decode curl's UTF-8 output correctly in Windows
# PowerShell as well as pwsh.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

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

# Never print or log the credential values; only their presence matters here.
if ([string]::IsNullOrWhiteSpace($seedUser) -or [string]::IsNullOrWhiteSpace($seedPassword)) {
    throw "AUTH_SEED_USER and AUTH_SEED_PASSWORD must be set in .env (their values are not printed)."
}

$null = Get-Command curl.exe -ErrorAction Stop

$tempRoot = Join-Path $env:LOCALAPPDATA "Temp\opencode"
if (-not (Test-Path -LiteralPath $tempRoot)) {
    $tempRoot = [System.IO.Path]::GetTempPath()
}
$runId = [Guid]::NewGuid().ToString("N")
$bodyPath = Join-Path $tempRoot "talentassistant-assistant-questions-$runId.json"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Split-Response {
    param([string]$Raw)
    $match = [regex]::Match($Raw, "HTTP_STATUS=(\d{3})\s*$")
    if (-not $match.Success) {
        return @{ Status = 0; Body = "" }
    }
    return @{ Status = [int]$match.Groups[1].Value; Body = $Raw.Substring(0, $match.Index).TrimEnd() }
}

$today = [DateTime]::Today
$birthdayFixtures = @(
    @{ NamePattern = 'Juan P\u00E9rez'; Month = 4; Day = 12 },
    @{ NamePattern = 'Pedro Rojas'; Month = 7; Day = 23 },
    @{ NamePattern = 'Mauricio Cardenas'; Month = 11; Day = 30 },
    @{ NamePattern = 'Juan Herrera'; Month = 2; Day = 18 },
    @{ NamePattern = '(Empleado sint\u00E9tico 005|Synthetic employee 005)'; Month = 5; Day = 9 },
    @{ NamePattern = '(Empleado sint\u00E9tico 004|Synthetic employee 004)'; Month = 9; Day = 27 },
    @{ NamePattern = '(Empleado sint\u00E9tico 002|Synthetic employee 002)'; Month = 12; Day = 2 },
    @{ NamePattern = '(Empleado sint\u00E9tico 003|Synthetic employee 003)'; Month = 3; Day = 14 }
)
$nextBirthday = $birthdayFixtures | ForEach-Object {
    $candidate = Get-Date -Year $today.Year -Month $_.Month -Day $_.Day -Hour 0 -Minute 0 -Second 0
    if ($candidate.Date -lt $today.Date) {
        $candidate = $candidate.AddYears(1)
    }
    [PSCustomObject]@{ NamePattern = $_.NamePattern; Date = $candidate }
} | Sort-Object Date | Select-Object -First 1
$monthPatterns = @(
    '', '(enero|january)', '(febrero|february)', '(marzo|march)',
    '(abril|april)', '(mayo|may)', '(junio|june)', '(julio|july)',
    '(agosto|august)', '(septiembre|september)', '(octubre|october)',
    '(noviembre|november)', '(diciembre|december)'
)
$birthdayMonthPattern = $monthPatterns[$nextBirthday.Date.Month]
$birthdayIsoDate = $nextBirthday.Date.ToString('yyyy-MM-dd')
$birthdayDayPattern = '\b0?' + $nextBirthday.Date.Day + '\b'
$birthdayDatePattern = '(?i)(' + $birthdayIsoDate + '|' + $birthdayDayPattern + '.*' + $birthdayMonthPattern + '|' + $birthdayMonthPattern + '.*' + $birthdayDayPattern + ')'
$birthdayAnswerPattern = '(?is)(?=.*' + $nextBirthday.NamePattern + ')(?=.*' + $birthdayDatePattern + ')'

$questions = @(
    @{ Text = "Dime cuales empleados ya estan trabajando."; Tools = @("presence"); Answer = 'Juan' },
    @{ Text = "Dime que empleados no han llegado aun."; Tools = @("presence"); Answer = 'Mauricio' },
    @{ Text = "Dime que empleados estan de vacaciones."; Tools = @("presence", "timeoff"); Answer = 'Pedro' },
    @{ Text = "Dime cuantos dias de vacaciones tiene Juan Perez."; Tools = @("timeoff"); Answer = '\b3\b' },
    @{ Text = "Dime cuando regresa Pedro Rojas."; Tools = @("timeoff"); Answer = '(20.*octubre|2026-10-20)' },
    @{ Text = "Dime que tareas tiene pendiente Mauricio Cardenas."; Tools = @("tasks"); Answer = '(n\wmina|payroll)' },
    @{ Text = "Quien es la proxima persona que cumple anos"; Tools = @("upcoming_birthday"); Answer = $birthdayAnswerPattern },
    @{ Text = "Que cargo tiene Mauricio Cardenas?"; Tools = @("search_employees"); Answer = '(T\w+ de n\wmina|payroll technician)' },
    @{ Text = "Dime la fecha de contratacion de Mauricio Cardenas."; Tools = @("search_employees"); Answer = '2018' },
    @{ Text = "hay alguien con apellido que tenga z"; Tools = @("search_employees"); Answer = 'Juan P\w+rez' },
    @{ Text = "Dime cuantos dias de vacaciones tiene Juan."; Tools = @("search_employees", "timeoff"); Answer = '(?i)(?s)(?=.*(cu\wl|which))(?=.*(Juan P\w+rez.*Juan Herrera|Juan Herrera.*Juan P\w+rez))' }
)

if ($Only -gt 0) {
    $questions = @($questions[$Only - 1])
}

$failures = 0
$aiUnavailable = $false

try {
    $loginBody = @{ userName = $seedUser; password = $seedPassword } | ConvertTo-Json -Compress
    [System.IO.File]::WriteAllText($bodyPath, $loginBody, $utf8NoBom)
    $loginRaw = [string]::Join([Environment]::NewLine, [string[]]@(
        & curl.exe --noproxy "*" --silent --show-error --max-time 10 `
            --header "Content-Type: application/json" `
            "--data-binary" "@$bodyPath" `
            --write-out "`nHTTP_STATUS=%{http_code}" "$ApiBaseUrl/auth/login" 2>$null
    ))
    $login = Split-Response $loginRaw
    if ($login.Status -ne 200) {
        throw "Login failed with HTTP $($login.Status). Check AUTH_SEED_USER/AUTH_SEED_PASSWORD in .env (values are not printed)."
    }

    $token = ($login.Body | ConvertFrom-Json).accessToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Login succeeded but no access token was returned; response body withheld."
    }

    foreach ($item in $questions) {
        $chatBody = @{ message = $item.Text; history = @() } | ConvertTo-Json -Compress
        [System.IO.File]::WriteAllText($bodyPath, $chatBody, $utf8NoBom)
        $chatRaw = [string]::Join([Environment]::NewLine, [string[]]@(
            & curl.exe --noproxy "*" --silent --show-error --max-time 120 `
                --header "Content-Type: application/json" `
                --header "Authorization: Bearer $token" `
                "--data-binary" "@$bodyPath" `
                --write-out "`nHTTP_STATUS=%{http_code}" "$ApiBaseUrl/chat" 2>$null
        ))
        $chat = Split-Response $chatRaw

        Write-Output ""
        Write-Output "Q: $($item.Text)"

        if ($chat.Status -eq 503 -or $chat.Status -eq 429) {
            $reason = ""
            try { $reason = ($chat.Body | ConvertFrom-Json).reason } catch { }
            Write-Output "The chat endpoint answered HTTP $($chat.Status) with reason '$reason'."
            switch ($reason) {
                "NotConfigured" {
                    Write-Output "No AI key is configured: set AI_API_KEY in .env and restart the api container."
                }
                "RateLimited" {
                    Write-Output "Provider rate limit reached: wait a minute and re-run this script."
                }
                default {
                    Write-Output "The provider rejected or failed the request: check 'docker logs talentassistant-local-api-1' for the upstream status and body."
                }
            }
            $aiUnavailable = $true
            break
        }

        if ($chat.Status -ne 200) {
            Write-Output "FAIL: expected HTTP 200, got HTTP $($chat.Status)."
            $failures++
            continue
        }

        $payload = $null
        try { $payload = $chat.Body | ConvertFrom-Json } catch { }
        if ($null -eq $payload) {
            Write-Output "FAIL: the response body is not valid JSON."
            $failures++
            continue
        }

        $tools = @($payload.toolsUsed)
        $answer = [string]$payload.answer
        Write-Output ("Tools: " + $(if ($tools.Count -gt 0) { [string]::Join(", ", $tools) } else { "(none)" }))
        Write-Output "Answer: $answer"

        if ([string]::IsNullOrWhiteSpace($answer)) {
            Write-Output "FAIL: the answer is empty."
            $failures++
        }
        if ($tools.Count -eq 0) {
            Write-Output "FAIL: toolsUsed is empty."
            $failures++
        }
        elseif (-not ($item.Tools | Where-Object { $tools -contains $_ })) {
            Write-Output "FAIL: toolsUsed does not contain one of the expected tools ($($item.Tools -join ', '))."
            $failures++
        }
        if ($item.ContainsKey('Answer') -and $answer -notmatch $item.Answer) {
            Write-Output "FAIL: the answer does not contain the expected fixture fact /$($item.Answer)/."
            $failures++
        }
        if ($PaceSeconds -gt 0) {
            Start-Sleep -Seconds $PaceSeconds
        }
    }
}
finally {
    Remove-Item -LiteralPath $bodyPath -Force -ErrorAction SilentlyContinue
}

if ($aiUnavailable) {
    exit 3
}

if ($failures -gt 0) {
    Write-Output ""
    Write-Output "FAIL: $failures assertion(s) failed across the reference questions."
    exit 1
}

Write-Output ""
Write-Output "PASS: all eleven reference questions returned a non-empty answer citing the expected tool."
exit 0
