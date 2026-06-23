#Requires -Version 5.1
<#
.SYNOPSIS
  Start Docker infrastructure and run all GradePaper microservices + API Gateway + AI grading (local Python).

.PARAMETER SkipDocker
  Do not start docker-compose (postgres, mongodb, redis, rabbitmq, qdrant).

.PARAMETER SkipBuild
  Skip dotnet build before launching services.

.PARAMETER SkipAi
  Do not start the local Python AIGradingService.

.PARAMETER Migrate
  Run EF migrations (ExamCatalog + Grading) before starting services.

.PARAMETER NoNewWindow
  Run services in the current console (background jobs). Default opens one window per service.
#>
param(
    [switch]$SkipDocker,
    [switch]$SkipBuild,
    [switch]$SkipAi,
    [switch]$Migrate,
    [switch]$NoNewWindow
)

$ErrorActionPreference = "Stop"

$ScriptRoot = $PSScriptRoot
$SrcRoot = Split-Path -Parent $ScriptRoot
$CoreRoot = Split-Path -Parent $SrcRoot
$RepoRoot = Split-Path -Parent $CoreRoot
$SolutionPath = Join-Path $CoreRoot "GradingSystem.slnx"
$ComposePath = Join-Path $RepoRoot "infra/docker/docker-compose.yml"
$AiRoot = Join-Path $RepoRoot "ai"
$PidDir = Join-Path $ScriptRoot ".pids"

$DockerServices = @("postgres", "mongodb", "redis", "rabbitmq", "qdrant", "gotenberg")

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

function Sync-InternalApiKeys {
    Import-RepoDotEnv
    if (-not $env:INTERNAL_API_KEY) {
        $env:INTERNAL_API_KEY = "duN6gR1GLWwprRnNH4tWxKLZLqM9zDYWQM2x0S0tYfC"
    }
    $env:InternalAuth__ApiKey = $env:INTERNAL_API_KEY
}

function Import-RepoDotEnv {
    $envFile = Join-Path $RepoRoot ".env"
    if (-not (Test-Path $envFile)) { return }

    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -notmatch '=') { return }
        $name, $value = $_ -split '=', 2
        $name = $name.Trim()
        $value = $value.Trim().Trim('"')
        if (-not [string]::IsNullOrWhiteSpace($name)) {
            Set-Item -Path "env:$name" -Value $value
        }
    }
}

function Ensure-AiVenv {
    $venvDir = Join-Path $AiRoot ".venv"
    $venvPython = Join-Path $venvDir "Scripts\python.exe"

    if (-not (Test-Path $venvPython)) {
        $python = Get-Command python -ErrorAction SilentlyContinue
        if (-not $python) {
            throw "Python not found. Install Python 3.11+ or use -SkipAi."
        }
        Write-Host "[AIGradingService] Creating venv at $venvDir ..." -ForegroundColor Yellow
        & python -m venv $venvDir
        & $venvPython -m pip install -q --upgrade pip
        & $venvPython -m pip install -q -r (Join-Path $AiRoot "requirements.txt")
    }

    return $venvPython
}

function Start-ServiceProcess {
    param(
        [string]$Name,
        [string]$ProjectPath,
        [int]$Port
    )

    if (Test-PortInUse -Port $Port) {
        Write-Warning "[$Name] Port $Port is already in use - skipping."
        return
    }

    $runArgs = @(
        "run"
        "--project", $ProjectPath
        "--launch-profile", "http"
    )

    if ($NoNewWindow) {
        Write-Host "[$Name] Starting on port $Port (background process)..."
        New-Item -ItemType Directory -Force -Path $PidDir | Out-Null
        $logFile = Join-Path $PidDir "$Name.log"
        Start-Process -FilePath "dotnet" -ArgumentList @(
            "run", "--project", $ProjectPath, "--launch-profile", "http"
        ) -WorkingDirectory $SrcRoot -WindowStyle Hidden -RedirectStandardOutput $logFile -RedirectStandardError $logFile
        return
    }

    $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }
    $command = "Set-Location '$SrcRoot'; `$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:INTERNAL_API_KEY='$($env:INTERNAL_API_KEY)'; `$env:InternalAuth__ApiKey='$($env:INTERNAL_API_KEY)'; dotnet $($runArgs -join ' ')"
    Write-Host "[$Name] Starting on port $Port..."
    Start-Process -FilePath $shell -ArgumentList @("-NoExit", "-Command", $command) -WindowStyle Normal
}

function Start-AiGradingProcess {
    $port = 8080

    if (Test-PortInUse -Port $port) {
        Write-Warning "[AIGradingService] Port $port is already in use - skipping."
        return
    }

    if (-not (Test-Path $AiRoot)) {
        throw "AI project not found: $AiRoot"
    }

    Import-RepoDotEnv

    if (-not $env:INTERNAL_API_KEY) {
        $env:INTERNAL_API_KEY = "duN6gR1GLWwprRnNH4tWxKLZLqM9zDYWQM2x0S0tYfC"
    }
    if (-not $env:OPENAI_MODEL) {
        $env:OPENAI_MODEL = "gpt-4o-mini"
    }

    $venvPython = Ensure-AiVenv
    $uvicorn = Join-Path $AiRoot ".venv\Scripts\uvicorn.exe"

    New-Item -ItemType Directory -Force -Path $PidDir | Out-Null
    $logFile = Join-Path $PidDir "AIGradingService.log"

    if ($NoNewWindow) {
        Write-Host "[AIGradingService] Starting on port $port (background, log: $logFile)..."
        $proc = Start-Process -FilePath $uvicorn -ArgumentList @(
            "app.main:app", "--host", "0.0.0.0", "--port", "$port", "--reload"
        ) -WorkingDirectory $AiRoot -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput $logFile -RedirectStandardError $logFile
        $proc.Id | Out-File -FilePath (Join-Path $PidDir "AIGradingService.pid") -Encoding ascii
        return
    }

    $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }
    $command = @"
Set-Location '$AiRoot'
`$env:INTERNAL_API_KEY='$($env:INTERNAL_API_KEY)'
`$env:OPENAI_API_KEY='$($env:OPENAI_API_KEY)'
`$env:OPENAI_MODEL='$($env:OPENAI_MODEL)'
& '$uvicorn' app.main:app --host 0.0.0.0 --port $port --reload
"@
    Write-Host "[AIGradingService] Starting on port $port (local Python, no Docker)..."
    Start-Process -FilePath $shell -ArgumentList @("-NoExit", "-Command", $command) -WindowStyle Normal
}

Write-Host "=== GradePaper - run all services ===" -ForegroundColor Cyan
Write-Host "Source root: $SrcRoot"
Write-Host ""

Sync-InternalApiKeys

if ($Migrate) {
    & (Join-Path $ScriptRoot "apply-migrations.ps1")
    Write-Host ""
}

if (-not $SkipDocker) {
    if (-not (Test-Path $ComposePath)) {
        throw "docker-compose.yml not found: $ComposePath"
    }

    Write-Host "Starting infrastructure (docker compose, no AI container)..." -ForegroundColor Yellow
    docker compose -f $ComposePath up -d @DockerServices
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

if (-not $SkipAi) {
    Start-AiGradingProcess
    Start-Sleep -Milliseconds 400
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
if (-not $SkipAi) {
    Write-Host ("  {0,-22} {1}" -f "AIGradingService", "http://localhost:8080/docs")
}
foreach ($service in $Services) {
    Write-Host ("  {0,-22} {1}" -f $service.Name, $service.Url)
}
Write-Host ""
if ($NoNewWindow) {
    Write-Host "Services run as background processes. Logs: $PidDir"
    Write-Host "Stop all: .\stop-all.ps1"
} else {
    Write-Host "Each service runs in its own terminal window."
    Write-Host "Stop all: .\stop-all.ps1"
}
Write-Host "Apply migrations only: .\apply-migrations.ps1"
