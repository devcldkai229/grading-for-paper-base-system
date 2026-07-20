#Requires -Version 5.1
<#
.SYNOPSIS
  Stop all GradePaper microservices, API Gateway, and local AIGradingService.

.PARAMETER StopDocker
  Also run docker compose down for infrastructure containers.
#>
param(
    [switch]$StopDocker
)

$ErrorActionPreference = "SilentlyContinue"

$ScriptRoot = $PSScriptRoot
$SrcRoot = Split-Path -Parent $ScriptRoot
$CoreRoot = Split-Path -Parent $SrcRoot
$RepoRoot = Split-Path -Parent $CoreRoot
$ComposePath = Join-Path $RepoRoot "infra/docker/docker-compose.yml"
$PidDir = Join-Path $ScriptRoot ".pids"

$Ports = @(5016, 5055, 5056, 5057, 5058, 5059, 5060, 8080, 8081)

Write-Host "=== GradePaper - stop all services ===" -ForegroundColor Cyan

$stopped = 0

$aiPidFiles = @("AIParseQuestionService.pid", "AIGradingService.pid", "AIGradingService.pid")
foreach ($pidFileName in $aiPidFiles) {
    $aiPidFile = Join-Path $PidDir $pidFileName
    if (Test-Path $aiPidFile) {
        $aiPid = Get-Content $aiPidFile -Raw
        if ($aiPid -and (Get-Process -Id $aiPid.Trim() -ErrorAction SilentlyContinue)) {
            Write-Host "Stopping AI service (PID $($aiPid.Trim()))..."
            Stop-Process -Id $aiPid.Trim() -Force
            $stopped++
        }
        Remove-Item $aiPidFile -Force -ErrorAction SilentlyContinue
    }
}

foreach ($port in $Ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    foreach ($conn in $connections) {
        $processId = $conn.OwningProcess
        if ($processId -and $processId -gt 0) {
            $proc = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "Stopping PID $processId ($($proc.ProcessName)) on port $port..."
                Stop-Process -Id $processId -Force
                $stopped++
            }
        }
    }
}

Get-Job -Name "IamService", "ExamCatalogService", "SubmissionService", "GradingService", "ReportingService", "NotificationService", "ApiGateway", "AIGradingService" -ErrorAction SilentlyContinue |
    ForEach-Object {
        Write-Host "Stopping job $($_.Name)..."
        Stop-Job $_
        Remove-Job $_ -Force
        $stopped++
    }

if ($stopped -eq 0) {
    Write-Host "No running services found on ports $($Ports -join ', ')." -ForegroundColor Yellow
} else {
    Write-Host "Stopped $stopped process(es)/job(s)." -ForegroundColor Green
}

if ($StopDocker) {
    Write-Host ""
    Write-Host "Stopping Docker infrastructure..." -ForegroundColor Yellow
    docker compose -f $ComposePath down
}

Write-Host "Done."
