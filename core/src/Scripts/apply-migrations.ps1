#Requires -Version 5.1
<#
.SYNOPSIS
  Apply EF Core migrations for ExamCatalog and Grading databases.

.PARAMETER PostgresPassword
  Postgres password (default: 12345, matches infra/docker docker-compose).
#>
param(
    [string]$PostgresPassword = "12345"
)

$ErrorActionPreference = "Stop"

$ScriptRoot = $PSScriptRoot
$CoreRoot = Split-Path -Parent (Split-Path -Parent $ScriptRoot)
$RepoRoot = Split-Path -Parent $CoreRoot

if (Test-Path (Join-Path $RepoRoot ".env")) {
    Get-Content (Join-Path $RepoRoot ".env") | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -notmatch '=') { return }
        $name, $value = $_ -split '=', 2
        $name = $name.Trim()
        $value = $value.Trim().Trim('"')
        if ($name -eq "ConnectionStrings__DefaultConnection" -and $value -match 'Password=([^;]+)') {
            $PostgresPassword = $Matches[1]
        }
    }
}

$examCatalogCs = "Host=localhost;Port=5433;Database=gradepaper_exam_catalog;Username=postgres;Password=$PostgresPassword"
$gradingCs = "Host=localhost;Port=5433;Database=gradepaper_grading;Username=postgres;Password=$PostgresPassword"

$migrations = @(
    @{
        Name = "ExamCatalog"
        Project = "core/src/Services/ExamCatalogService/ExamCatalogService.Infrastructure/ExamCatalogService.Infrastructure.csproj"
        Startup = "core/src/Services/ExamCatalogService/ExamCatalogService.API/ExamCatalogService.API.csproj"
        Connection = $examCatalogCs
    },
    @{
        Name = "Grading"
        Project = "core/src/Services/GradingService/GradingService.Infrastructure/GradingService.Infrastructure.csproj"
        Startup = "core/src/Services/GradingService/GradingService.API/GradingService.API.csproj"
        Connection = $gradingCs
    }
)

Write-Host "=== GradePaper - apply database migrations ===" -ForegroundColor Cyan
Write-Host "Repo root: $RepoRoot"
Write-Host ""

Push-Location $RepoRoot
try {
    foreach ($m in $migrations) {
        Write-Host "[$($m.Name)] Updating database..." -ForegroundColor Yellow
        dotnet ef database update `
            --project $m.Project `
            --startup-project $m.Startup `
            --connection $m.Connection
        if ($LASTEXITCODE -ne 0) {
            throw "Migration failed for $($m.Name) (exit $LASTEXITCODE)"
        }
        Write-Host "[$($m.Name)] Done." -ForegroundColor Green
        Write-Host ""
    }
} finally {
    Pop-Location
}

Write-Host "All migrations applied." -ForegroundColor Green
