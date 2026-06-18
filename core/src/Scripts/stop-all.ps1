#Requires -Version 5.1
<#
.SYNOPSIS
  Stop all GradePaper microservices and API Gateway (by listening ports).

.PARAMETER StopDocker
  Also run docker compose down for infrastructure containers.
#>
param(
    [switch]$StopDocker
)

$ErrorActionPreference = "SilentlyContinue"

$ScriptRoot = $PSScriptRoot
$SrcRoot = Split-Path -Parent $ScriptRoot
$ComposePath = Join-Path $SrcRoot "docker-compose.yml"

$Ports = @(5016, 5055, 5056, 5057, 5058, 5059, 5060)

Write-Host "=== GradePaper - stop all services ===" -ForegroundColor Cyan

$stopped = 0
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

Get-Job -Name "IamService", "ExamCatalogService", "SubmissionService", "GradingService", "ReportingService", "NotificationService", "ApiGateway" -ErrorAction SilentlyContinue |
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
