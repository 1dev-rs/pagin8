@echo off
REM Quick runner for Testcontainers integration tests

echo ========================================
echo Pagin8 Testcontainers Tests
echo ========================================
echo.

REM Check Docker
docker ps >nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker is not running
    echo Please start Docker Desktop and try again
    pause
    exit /b 1
)

echo Docker is running
echo.
echo Running all Testcontainer tests...
echo.

dotnet test --filter "Container=Testcontainers" --logger "console;verbosity=normal"

echo.
echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo All tests passed!
) else (
    echo Some tests failed
)
echo ========================================

pause
