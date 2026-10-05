@echo off
setlocal EnableExtensions EnableDelayedExpansion
title Configuracion GestionCitas - User Secrets

echo ============================================================
echo       CONFIGURACION LOCAL - APP WEB GESTION DE CITAS
echo ============================================================
echo.
echo Este asistente configurara:
echo   - ConnectionStrings:DefaultConnection
echo   - Jwt:Key
echo.
echo Los secretos se guardaran SOLO en tu PC mediante .NET User Secrets.
echo NO se escribiran en appsettings.json ni se subiran a GitHub.
echo.

set "SCRIPT_DIR=%~dp0"
set "BACKEND_DIR=%SCRIPT_DIR%backend\GestionCitas.Api"

if not exist "%BACKEND_DIR%\GestionCitas.Api.csproj" (
    echo [ERROR] No se encontro:
    echo %BACKEND_DIR%\GestionCitas.Api.csproj
    echo.
    echo Coloca este archivo BAT en la raiz del repositorio:
    echo App-Web-Gestion-Citas\
    echo.
    pause
    exit /b 1
)

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] No se encontro el comando dotnet.
    echo Instala el .NET SDK y vuelve a ejecutar este archivo.
    echo.
    pause
    exit /b 1
)

echo Selecciona tu usuario:
echo.
echo   1. Cote
echo   2. Dan
echo   3. Cristhian
echo   4. Nancy
echo   5. Antony
echo.
set /p OPCION="Opcion [1-5]: "

if "%OPCION%"=="1" (
    set "DEV_NAME=Cote"
    set "SQL_USER=GestionCitas_Cote"
) else if "%OPCION%"=="2" (
    set "DEV_NAME=Dan"
    set "SQL_USER=GestionCitas_Dan"
) else if "%OPCION%"=="3" (
    set "DEV_NAME=Cristhian"
    set "SQL_USER=GestionCitas_Cristhian"
) else if "%OPCION%"=="4" (
    set "DEV_NAME=Nancy"
    set "SQL_USER=GestionCitas_Nancy"
) else if "%OPCION%"=="5" (
    set "DEV_NAME=Antony"
    set "SQL_USER=GestionCitas_Antony"
) else (
    echo.
    echo [ERROR] Opcion no valida.
    pause
    exit /b 1
)

echo.
echo Usuario seleccionado: %DEV_NAME%
echo Usuario SQL:         %SQL_USER%
echo Base de datos:        GestionCitasDb
echo Servidor:             15.204.246.255,1433
echo.

for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "$p=Read-Host 'Ingresa la contrasena SQL asignada' -AsSecureString; $b=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($p); try {[Runtime.InteropServices.Marshal]::PtrToStringBSTR($b)} finally {[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b)}"`) do set "SQL_PASSWORD=%%P"

if not defined SQL_PASSWORD (
    echo.
    echo [ERROR] No se ingreso una contrasena.
    pause
    exit /b 1
)

set "JWT_KEY=GESTION_CITAS_LOCAL_DEV_2026_CLAVE_SEGURA_123456"
set "CONNECTION_STRING=Server=15.204.246.255,1433;Database=GestionCitasDb;User Id=%SQL_USER%;Password=%SQL_PASSWORD%;Encrypt=True;TrustServerCertificate=True;"

echo.
echo Configurando User Secrets...
pushd "%BACKEND_DIR%"

dotnet user-secrets init >nul 2>&1

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "%CONNECTION_STRING%"
if errorlevel 1 (
    echo.
    echo [ERROR] No se pudo guardar la cadena de conexion.
    set "SQL_PASSWORD="
    set "CONNECTION_STRING="
    popd
    pause
    exit /b 1
)

dotnet user-secrets set "Jwt:Key" "%JWT_KEY%"
if errorlevel 1 (
    echo.
    echo [ERROR] No se pudo guardar Jwt:Key.
    set "SQL_PASSWORD="
    set "CONNECTION_STRING="
    popd
    pause
    exit /b 1
)

popd

set "SQL_PASSWORD="
set "CONNECTION_STRING="

echo.
echo ============================================================
echo CONFIGURACION COMPLETADA
echo ============================================================
echo.
echo Desarrollador : %DEV_NAME%
echo Usuario SQL   : %SQL_USER%
echo Base de datos : GestionCitasDb
echo Servidor      : 15.204.246.255,1433
echo.
echo Los secretos quedaron almacenados localmente mediante
echo .NET User Secrets y NO dentro del repositorio.
echo.
echo Para iniciar el backend:
echo.
echo   cd backend\GestionCitas.Api
echo   dotnet run
echo.
echo Para iniciar Angular en otra terminal:
echo.
echo   cd frontend
echo   npm start
echo.
pause
endlocal
