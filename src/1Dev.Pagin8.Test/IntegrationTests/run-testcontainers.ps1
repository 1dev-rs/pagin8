#!/usr/bin/env pwsh
# Quick test runner for Testcontainers integration tests

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Pagin8 Testcontainers Integration Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Docker is running
Write-Host "Checking Docker..." -ForegroundColor Yellow
$dockerRunning = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)

if (-not $dockerRunning) {
    Write-Host "ERROR: Docker is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install Docker Desktop: https://www.docker.com/products/docker-desktop" -ForegroundColor Yellow
    exit 1
}

try {
    docker ps | Out-Null
    Write-Host "? Docker is running" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Docker is not running" -ForegroundColor Red
    Write-Host "Please start Docker Desktop and try again" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Choose tests to run:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. All Testcontainer Tests (SQL Server + PostgreSQL)" -ForegroundColor Green
Write-Host "2. SQL Server Only" -ForegroundColor Green
Write-Host "3. PostgreSQL Only" -ForegroundColor Green
Write-Host "4. Specific Test" -ForegroundColor Green
Write-Host ""

$choice = Read-Host "Enter your choice (1-4)"

$filter = switch ($choice) {
    "1" { 
        Write-Host "`nRunning all Testcontainer tests..." -ForegroundColor Cyan
        "Container=Testcontainers"
    }
    "2" {
        Write-Host "`nRunning SQL Server tests..." -ForegroundColor Cyan
        "Database=SqlServer&Container=Testcontainers"
    }
    "3" {
        Write-Host "`nRunning PostgreSQL tests..." -ForegroundColor Cyan
        "Database=PostgreSql&Container=Testcontainers"
    }
    "4" {
        Write-Host "`nAvailable test patterns:" -ForegroundColor Gray
        Write-Host "  TC-SQL-001  (specific SQL Server test)" -ForegroundColor Gray
        Write-Host "  TC-PG-001   (specific PostgreSQL test)" -ForegroundColor Gray
        $testName = Read-Host "`nEnter test code (e.g., TC-SQL-001)"
        "FullyQualifiedName~$testName"
    }
    default {
        Write-Host "Invalid choice. Running all tests..." -ForegroundColor Yellow
        "Container=Testcontainers"
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Test Execution" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date

# Run tests
dotnet test --filter $filter --logger "console;verbosity=normal"

$exitCode = $LASTEXITCODE
$duration = (Get-Date) - $startTime

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor White

if ($exitCode -eq 0) {
    Write-Host "Result: ? All tests passed!" -ForegroundColor Green
} else {
    Write-Host "Result: ? Some tests failed (exit code: $exitCode)" -ForegroundColor Red
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

exit $exitCode
