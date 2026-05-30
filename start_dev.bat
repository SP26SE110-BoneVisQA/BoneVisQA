@echo off
setlocal EnableExtensions

echo ============================================================
echo   BoneVisQA - Capstone Dev Stack (Python AI + .NET API)
echo ============================================================
echo.

set "ROOT=%~dp0"
set "PY_DIR=%ROOT%BoneVisQA.AI"
set "API_DIR=%ROOT%SP26SE110_BoneVisQA\BoneVisQA.API"

if not exist "%PY_DIR%\app\main.py" (
    echo [ERROR] Python service not found at: %PY_DIR%
    pause
    exit /b 1
)

if not exist "%API_DIR%\BoneVisQA.API.csproj" (
    echo [ERROR] .NET API not found at: %API_DIR%
    pause
    exit /b 1
)

if not exist "%PY_DIR%\.env" (
    echo [WARN]  %PY_DIR%\.env is missing. Copy .env.example and set secrets.
) else (
    echo [OK]    Python .env found.
)

echo.
echo [1/2] Starting Python AI microservice (uvicorn :8000, --reload)...
start "BoneVisQA AI (uvicorn)" cmd /k ^
  "cd /d "%PY_DIR%" ^&^& ^
   if exist .venv\Scripts\activate.bat (call .venv\Scripts\activate.bat) else if exist venv\Scripts\activate.bat (call venv\Scripts\activate.bat) ^&^& ^
   echo. ^&^& echo === BoneVisQA AI - FastAPI === ^&^& echo   http://localhost:8000/health ^&^& echo. ^&^& ^
   uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload"

timeout /t 2 /nobreak >nul

echo [2/2] Starting .NET 8 API (dotnet run)...
start "BoneVisQA API (dotnet)" cmd /k ^
  "cd /d "%API_DIR%" ^&^& ^
   echo. ^&^& echo === BoneVisQA.API - .NET 8 === ^&^& echo   http://localhost:5046/swagger ^&^& echo. ^&^& ^
   dotnet run"

echo.
echo Both services are booting in separate windows.
echo   Python AI : http://localhost:8000/health
echo   .NET API  : http://localhost:5046/swagger
echo.
echo Close those windows (or Ctrl+C in each) to stop the stack.
echo ============================================================
pause
