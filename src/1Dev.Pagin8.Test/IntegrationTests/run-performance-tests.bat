@echo off
REM Quick performance test launcher for Windows
REM Usage: run-performance-tests.bat [dataset_size] [database]
REM Examples:
REM   run-performance-tests.bat          (5k products, both databases)
REM   run-performance-tests.bat 50000    (50k products, both databases)
REM   run-performance-tests.bat 100000 SqlServer  (100k products, SQL Server only)

set DATASET_SIZE=%1
set DATABASE=%2

if "%DATASET_SIZE%"=="" set DATASET_SIZE=5000
if "%DATABASE%"=="" set DATABASE=Both

echo.
echo ========================================
echo Pagin8 Performance Tests
echo ========================================
echo Dataset Size: %DATASET_SIZE%
echo Database(s): %DATABASE%
echo ========================================
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0run-performance-tests.ps1" -DatasetSize %DATASET_SIZE% -Database %DATABASE%

pause
