#Requires -Version 5.1
<#
.SYNOPSIS
  Start Docker infrastructure and run all GradePaper microservices + API Gateway.

.PARAMETER SkipDocker
  Do not start docker-compose (postgres, mongodb, redis, rabbitmq).

.PARAMETER SkipBuild
  Skip dotnet build before launching services.

.PARAMETER NoNewWindow
  Run services in the current console (background jobs). Default opens one window per service.
#>
param(
    [switch]$SkipDocker,
    [switch]$SkipBuild,
    [switch]$NoNewWindow
)

$ErrorActionPreference = "Stop"

$ScriptRoot = $PSScriptRoot
$SrcRoot = Split-Path -Parent $ScriptRoot
$CoreRoot = Split-Path -Parent $SrcRoot
$SolutionPath = Join-Path $CoreRoot "GradingSystem.slnx"
$ComposePath = Join-Path $SrcRoot "docker-compose.yml"

$Services = @(
    @{ Name = "IamService";           Project = "Services\IamService\IamService.API\IamService.API.csproj";           Port = 5055; Url = "http://localhost:5055/swagger" }
    @{ Name = "ExamCatalogService";   Project = "Services\ExamCatalogService\ExamCatalogService.API\ExamCatalogService.API.csproj"; Port = 5056; Url = "http://localhost:5056/swagger" }
    @{ Name = "SubmissionService";    Project = "Services\SubmissionService\SubmissionService.API\SubmissionService.API.csproj";   Port = 5057; Url = "http://localhost:5057/swagger" }
    @{ Name = "GradingService";       Project = "Services\GradingService\GradingService.API\GradingService.API.csproj";             Port = 5058; Url = "http://localhost:5058/swagger" }
    @{ Name = "ReportingService";     Project = "Services\ReportingService\ReportingService.API\ReportingService.API.csproj";     Port = 5059; Url = "http://localhost:5059/swagger" }
    @{ Name = "NotificationService";  Project = "Services\NotificationService\NotificationService.API\NotificationService.API.csproj"; Port = 5060; Url = "http://localhost:5060/swagger" }
    @{ Name = "ApiGateway";           Project = "ApiGateway\ApiGateway.csproj";                                         Port = 5016; Url = "http://localhost:5016/swagger" }
)

function Test-PortInUse([int]$Port) {
    return $null -ne (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1)
}

function Start-ServiceProcess {
    param(
        [string]$Name,
        [string]$ProjectPath,
        [int]$Port
    )

    if (Test-PortInUse -Port $Port) {
        Write-Warning "[$Name] Port $Port is already in use — skipping."
        return
    }

    $runArgs = @(
        "run"
        "--project", "`"$ProjectPath`""
        "--launch-profile", "http"
    )

    if ($NoNewWindow) {
        Write-Host "[$Name] Starting on port $Port (background job)..."
        Start-Job -Name $Name -ScriptBlock {
            param($Src, $Args)
            Set-Location $Src
            & dotnet @Args
        } -ArgumentList $SrcRoot, $runArgs | Out-Null
        return
    }

    $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }
    $command = "Set-Location '$SrcRoot'; dotnet $($runArgs -join ' ')"
    Write-Host "[$Name] Starting on port $Port..."
    Start-Process -FilePath $shell -ArgumentList @("-NoExit", "-Command", $command) -WindowStyle Normal
}

Write-Host "=== GradePaper — run all services ===" -ForegroundColor Cyan
Write-Host "Source root: $SrcRoot"
Write-Host ""

if (-not $SkipDocker) {
    if (-not (Test-Path $ComposePath)) {
        throw "docker-compose.yml not found: $ComposePath"
    }

    Write-Host "Starting infrastructure (docker compose)..." -ForegroundColor Yellow
    docker compose -f $ComposePath up -d
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE"
    }
    Write-Host "Infrastructure is up." -ForegroundColor Green
    Write-Host ""
}

if (-not $SkipBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build $SolutionPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
    Write-Host "Build succeeded." -ForegroundColor Green
    Write-Host ""
}

foreach ($service in $Services) {
    $projectPath = Join-Path $SrcRoot $service.Project
    if (-not (Test-Path $projectPath)) {
        throw "Project not found: $projectPath"
    }

    Start-ServiceProcess -Name $service.Name -ProjectPath $projectPath -Port $service.Port
    Start-Sleep -Milliseconds 400
}

Write-Host ""
Write-Host "=== All services launched ===" -ForegroundColor Green
Write-Host ""
Write-Host "Endpoints:" -ForegroundColor Cyan
foreach ($service in $Services) {
    Write-Host ("  {0,-22} {1}" -f $service.Name, $service.Url)
}
Write-Host ""
if ($NoNewWindow) {
    Write-Host "Services run as PowerShell jobs. View output: Get-Job | Receive-Job"
    Write-Host "Stop all: .\stop-all.ps1"
} else {
    Write-Host "Each service runs in its own terminal window."
    Write-Host "Stop all: .\stop-all.ps1"
}
