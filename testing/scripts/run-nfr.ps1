# FreightLink non-functional evidence runner (PowerShell / Windows native).
# Runs k6 load testing and OWASP ZAP API security scanning against a disposable stack.
param(
    [ValidateSet("k6", "zap", "all")]
    [string]$What = "all"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ROOT = (Resolve-Path "$ScriptDir\..\..").Path
$EVID = Join-Path $ROOT "testing\evidence"
$DockerRoot = $ROOT.Replace("\", "/")
$DockerEvid = $EVID.Replace("\", "/")

# Port 15432 is outside Windows dynamic port exclusion ranges (e.g. 55000+)
$PG_NAME = "fl-nfr-pg"
$PG_PORT = 15432
$API_PORT = 5188
$API_URL = "http://localhost:$API_PORT"
$DOCKER_API_URL = "http://host.docker.internal:$API_PORT"
$INTERNAL_KEY = "nfr-internal-key-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"

New-Item -ItemType Directory -Path "$EVID\k6" -Force | Out-Null
New-Item -ItemType Directory -Path "$EVID\zap" -Force | Out-Null

$WorkDir = Join-Path $env:TEMP ("fl-nfr-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path "$WorkDir\publish" -Force | Out-Null
$apiProcess = $null

function Cleanup {
    Write-Host "==> Cleaning up..." -ForegroundColor Cyan
    if ($apiProcess -and -not $apiProcess.HasExited) {
        try {
            $apiProcess.Kill()
        } catch {
            Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
        }
    }
    docker rm -f $PG_NAME 2>$null | Out-Null
    if (Test-Path $WorkDir) {
        Remove-Item -Recurse -Force $WorkDir -ErrorAction SilentlyContinue
    }
}

try {
    Write-Host "==> Starting disposable Postgres on :$PG_PORT" -ForegroundColor Cyan
    docker rm -f $PG_NAME 2>$null | Out-Null
    $startPg = docker run -d --name $PG_NAME `
        -e POSTGRES_PASSWORD=nfrpass `
        -e POSTGRES_DB=freightlink_nfr `
        -p "${PG_PORT}:5432" `
        postgres:16-alpine 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start Postgres container on port $PG_PORT`: $startPg"
    }

    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        $null = docker exec $PG_NAME pg_isready -U postgres 2>$null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) {
        throw "PostgreSQL container failed to become ready."
    }
    Write-Host "    PostgreSQL ready." -ForegroundColor Green

    Write-Host "==> Publishing API" -ForegroundColor Cyan
    dotnet publish "$ROOT\backend\FreightLink.Api.csproj" -c Release -o "$WorkDir\publish" --nologo | Out-Null
    Remove-Item "$WorkDir\publish\appsettings.Development.json" -ErrorAction SilentlyContinue

    Write-Host "==> Starting API on $API_URL" -ForegroundColor Cyan
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://0.0.0.0:$API_PORT"
    $env:CONNECTIONSTRINGS__DEFAULTCONNECTION = "Host=localhost;Port=$PG_PORT;Database=freightlink_nfr;Username=postgres;Password=nfrpass"
    $env:JWT__ISSUER = "FreightLinkApi"
    $env:JWT__AUDIENCE = "FreightLinkClient"
    $env:JWT__KEY = "nfr-throwaway-signing-key-that-is-long-enough-1234567890"
    $env:JWT__ACCESSTOKENMINUTES = "60"
    $env:JWT__REFRESHTOKENDAYS = "1"
    $env:ADMIN_USER_EMAIL = "nfr-admin@example.com"
    $env:ADMIN_USER_PASSWORD = 'NfrAdm1n$ecret'
    $env:INTERNAL_API_KEY = $INTERNAL_KEY
    $env:EMAIL__ENABLED = "false"
    $env:EMAIL__REQUIREEMAILVERIFICATION = "false"
    $env:CORS_ORIGINS = "http://localhost:5173"

    $apiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "`"$WorkDir\publish\FreightLink.Api.dll`"" `
        -WorkingDirectory $WorkDir `
        -RedirectStandardOutput "$WorkDir\api.log" `
        -RedirectStandardError "$WorkDir\api.err.log" `
        -PassThru `
        -NoNewWindow

    $healthy = $false
    for ($i = 0; $i -lt 60; $i++) {
        try {
            $resp = Invoke-WebRequest -Uri "$API_URL/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($resp.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    if (-not $healthy) {
        $logSnippet = ""
        if (Test-Path "$WorkDir\api.err.log") { $logSnippet += Get-Content "$WorkDir\api.err.log" -Tail 20 | Out-String }
        if (Test-Path "$WorkDir\api.log") { $logSnippet += Get-Content "$WorkDir\api.log" -Tail 20 | Out-String }
        throw "API failed to start on $API_URL. Details:`n$logSnippet"
    }
    Write-Host "    API healthy." -ForegroundColor Green

    # Run k6
    if ($What -eq "k6" -or $What -eq "all") {
        Write-Host "==> k6: loads API (10/50/100 VUs)" -ForegroundColor Cyan
        docker run --rm `
            -v "${DockerRoot}/backend/PerformanceTests:/scripts:ro" `
            -v "${DockerEvid}/k6:/out" `
            -e API_BASE_URL="$DOCKER_API_URL" `
            grafana/k6 run `
            --summary-export=/out/loads-api-summary.json /scripts/loads-api.perf.js 2>&1 | Tee-Object -FilePath "$EVID\k6\loads-api-console.log"

        Write-Host "==> k6: agent workflow persistence latency" -ForegroundColor Cyan
        docker run --rm `
            -v "${DockerRoot}/backend/PerformanceTests:/scripts:ro" `
            -v "${DockerEvid}/k6:/out" `
            -e API_BASE_URL="$DOCKER_API_URL" `
            -e INTERNAL_API_KEY="$INTERNAL_KEY" `
            grafana/k6 run `
            --summary-export=/out/agent-workflow-summary.json /scripts/agent-workflow-latency.perf.js 2>&1 | Tee-Object -FilePath "$EVID\k6\agent-workflow-console.log"
    }

    # Run ZAP
    if ($What -eq "zap" -or $What -eq "all") {
        Write-Host "==> ZAP: registering a Shipper so the scan runs authenticated" -ForegroundColor Cyan
        $email = "zap-shipper-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())@example.com"
        $regBody = @{
            email = $email
            password = 'Sup3r$ecret1'
            fullName = "ZAP Shipper"
            companyName = "ZAP Co"
            billingAddress = "1 Test Lane, Colombo"
        } | ConvertTo-Json
        Invoke-RestMethod -Uri "$API_URL/api/v1/auth/register/shipper" -Method Post -Body $regBody -ContentType "application/json" | Out-Null

        $loginBody = @{
            email = $email
            password = 'Sup3r$ecret1'
        } | ConvertTo-Json
        $loginRes = Invoke-RestMethod -Uri "$API_URL/api/v1/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
        $token = $loginRes.accessToken

        Write-Host "==> ZAP: API scan from OpenAPI document (authenticated as Shipper)" -ForegroundColor Cyan
        $zapReplacer = "-config replacer.full_list(0).description=auth -config replacer.full_list(0).enabled=true -config replacer.full_list(0).matchtype=REQ_HEADER -config replacer.full_list(0).matchstr=Authorization -config replacer.full_list(0).regex=false -config replacer.full_list(0).replacement='Bearer $token'"
        
        docker run --rm `
            -v "${DockerEvid}/zap:/zap/wrk:rw" `
            -t ghcr.io/zaproxy/zaproxy:stable zap-api-scan.py `
            -t "$DOCKER_API_URL/swagger/v1/swagger.json" `
            -f openapi `
            -O "$DOCKER_API_URL" `
            -r zap-report.html `
            -J zap-report.json `
            -w zap-report.md `
            -T 10 `
            -I `
            -z "$zapReplacer" 2>&1 | Tee-Object -FilePath "$EVID\zap\zap-console.log"
    }

    Write-Host "==> Done. Evidence in $EVID\k6 and $EVID\zap" -ForegroundColor Green
} finally {
    Cleanup
}
