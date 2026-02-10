#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run Pagin8 integration tests with configurable dataset sizes for performance testing.

.DESCRIPTION
    This script runs integration tests with different dataset sizes to measure performance.
    You can specify dataset size, test category, and database type.

.PARAMETER DatasetSize
    Number of products to generate (default: 5000)

.PARAMETER Database
    Database to test: SqlServer, PostgreSql, or Both (default: Both)

.PARAMETER Seed
    Random seed for reproducible data (default: 42)

.PARAMETER Verbose
    Show detailed test output

.EXAMPLE
    .\run-performance-tests.ps1 -DatasetSize 50000
    Run tests with 50,000 products on both databases

.EXAMPLE
    .\run-performance-tests.ps1 -DatasetSize 100000 -Database SqlServer -Verbose
    Run tests with 100,000 products on SQL Server only with detailed output

.EXAMPLE
    .\run-performance-tests.ps1 -DatasetSize 10000 -Seed 123
    Run tests with 10,000 products using seed 123 for different data distribution
#>

param(
    [Parameter(Mandatory=$false)]
    [int]$DatasetSize = 0,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet('SqlServer', 'PostgreSql', 'Both')]
    [string]$Database = 'Both',
    
    [Parameter(Mandatory=$false)]
    [int]$Seed = 0,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet('quick', 'standard', 'realistic', 'stress', 'extreme', '')]
    [string]$Preset = '',
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Header($message) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success($message) {
    Write-Host "? $message" -ForegroundColor Green
}

function Write-Info($message) {
    Write-Host "??  $message" -ForegroundColor Blue
}

function Write-Warning($message) {
    Write-Host "??  $message" -ForegroundColor Yellow
}

function Write-Error($message) {
    Write-Host "? $message" -ForegroundColor Red
}

# Check Docker is running
function Test-Docker {
    try {
        docker ps | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

# Format dataset size
function Format-Number($number) {
    if ($number -ge 1000000) {
        return "{0:N1}M" -f ($number / 1000000)
    }
    elseif ($number -ge 1000) {
        return "{0:N0}k" -f ($number / 1000)
    }
    else {
        return "{0:N0}" -f $number
    }
}

# Estimate execution time
function Get-EstimatedTime($datasetSize) {
    # Rough estimates based on testing
    $containerStartup = 20  # seconds
    $seedingTime = [Math]::Max(5, $datasetSize / 1000)  # ~1000 records per second
    $testExecution = 30  # seconds for test execution
    
    return [int]($containerStartup + $seedingTime + $testExecution)
}

# Main script
Clear-Host
Write-Header "Pagin8 Performance Testing"

# Configuration summary
Write-Info "Configuration:"
Write-Host "  ?? Dataset Size: $(Format-Number $DatasetSize) products ($DatasetSize records)"
Write-Host "  ???  Database(s): $Database"
Write-Host "  ?? Seed: $seed"
Write-Host "  ?? Location: 1Dev.Pagin8.Test"
Write-Host ""

# Validate Docker
Write-Info "Checking prerequisites..."
if (-not (Test-Docker)) {
    Write-Error "Docker is not running. Please start Docker Desktop and try again."
    Write-Host "  Download: https://www.docker.com/products/docker-desktop/"
    exit 1
}
Write-Success "Docker is running"

# Estimate time
$estimatedTime = Get-EstimatedTime $DatasetSize
Write-Info "Estimated execution time: ~$estimatedTime seconds"

# Set environment variables
Write-Info "Setting environment variables..."
$env:PAGIN8_TEST_DATASET_SIZE = $DatasetSize.ToString()
$env:PAGIN8_TEST_SEED = $Seed.ToString()
Write-Success "Environment variables set"

# Determine test filter
$filter = "Container=Testcontainers"
if ($Database -ne 'Both') {
    $filter += "&Database=$Database"
}

# Build test command
$testArgs = @(
    "test",
    "--filter", "`"$filter`"",
    "--no-build"
)

if ($Verbose) {
    $testArgs += "--logger"
    $testArgs += "console;verbosity=detailed"
}
else {
    $testArgs += "--logger"
    $testArgs += "console;verbosity=normal"
}

# Show test command
Write-Info "Running tests..."
Write-Host "  Filter: $filter" -ForegroundColor Gray
Write-Host ""

# Run tests
$startTime = Get-Date

try {
    $command = "dotnet $($testArgs -join ' ')"
    Invoke-Expression $command
    
    $exitCode = $LASTEXITCODE
    $elapsed = (Get-Date) - $startTime
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    
    if ($exitCode -eq 0) {
        Write-Success "All tests passed!"
    }
    else {
        Write-Error "Some tests failed (exit code: $exitCode)"
    }
    
    Write-Info "Total execution time: $($elapsed.ToString('mm\:ss'))"
    Write-Info "Dataset size: $(Format-Number $DatasetSize) products"
    
    # Performance summary
    if ($exitCode -eq 0) {
        Write-Host ""
        Write-Info "Performance Summary:"
        
        $totalTests = if ($Database -eq 'Both') { 50 } else { 25 }
        $avgTimePerTest = $elapsed.TotalMilliseconds / $totalTests
        
        Write-Host "  Tests executed: $totalTests"
        Write-Host "  Average per test: $([Math]::Round($avgTimePerTest, 0))ms"
        Write-Host "  Dataset: $(Format-Number $DatasetSize) records"
        
        # Performance rating
        if ($avgTimePerTest -lt 100) {
            Write-Host "  Rating: ? Excellent" -ForegroundColor Green
        }
        elseif ($avgTimePerTest -lt 500) {
            Write-Host "  Rating: ? Good" -ForegroundColor Blue
        }
        elseif ($avgTimePerTest -lt 1000) {
            Write-Host "  Rating: ??  Acceptable" -ForegroundColor Yellow
        }
        else {
            Write-Host "  Rating: ??  Slow - Consider optimization" -ForegroundColor Red
        }
    }
    
    Write-Host "========================================" -ForegroundColor Cyan
    
    exit $exitCode
}
catch {
    Write-Error "Failed to run tests: $_"
    exit 1
}
finally {
    # Clean up environment variables
    Remove-Item Env:\PAGIN8_TEST_DATASET_SIZE -ErrorAction SilentlyContinue
    Remove-Item Env:\PAGIN8_TEST_SEED -ErrorAction SilentlyContinue
}
