@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

rem ==========================================================
rem  VRCFT Windows Installer
rem ==========================================================

set "MIN_DOTNET=8.0.404"
set "INSTALL_DIR=%LOCALAPPDATA%\VRCFaceTracking"
set "BUILD_PROJECT=src\VRCFaceTracking.Avalonia.Desktop\VRCFaceTracking.Avalonia.Desktop.csproj"

set "REPO_URL=https://github.com/dfgHiatus/VRCFaceTracking.Avalonia.git"

set "PAUSE_ON_SUCCESS=0"

title VRCFT Installer

echo.
echo ==========================================================
echo   VRCFT Installer for Windows
echo   InstallDir: "%INSTALL_DIR%"
echo   Require .NET SDK >= %MIN_DOTNET%
echo ==========================================================
echo.

call :EnsurePowerShell || goto :fail

call :EnsureWinget

call :EnsureGit || goto :fail
call :EnsureDotnet8 || goto :fail

call :PrepareRepo || goto :fail
call :UpdateIfNeeded || goto :fail
call :Build || goto :fail
call :RunApp || goto :fail

echo.
echo [OK] All done.
if "%PAUSE_ON_SUCCESS%"=="1" pause
exit /b 0


rem =========================
rem  Helpers
rem =========================

:EnsurePowerShell
where /q powershell.exe
if errorlevel 1 (
  echo [ERROR] PowerShell not found. This installer requires PowerShell.
  exit /b 1
)
exit /b 0

:EnsureWinget
where /q winget.exe
if errorlevel 1 (
  set "HAS_WINGET=0"
  echo [WARN] winget not found. Auto-install dependencies may not work.
  echo        Please install "App Installer" from Microsoft Store (which provides winget),
  echo        or install Git and .NET manually, then re-run this bat.
) else (
  set "HAS_WINGET=1"
)
exit /b 0

:RestartSelf
echo.
echo ======================================
echo  Dependencies installed/updated
echo  Restarting installer to refresh PATH
echo ======================================
echo.
start "" cmd /c "\"%~f0\""
exit /b 0

:ReloadPath
rem Reload User+Machine PATH from registry into current process (best-effort)
for /f "usebackq delims=" %%P in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$u=[Environment]::GetEnvironmentVariable('Path','User');" ^
  "$m=[Environment]::GetEnvironmentVariable('Path','Machine');" ^
  "if([string]::IsNullOrEmpty($u)){ $u='' };" ^
  "if([string]::IsNullOrEmpty($m)){ $m='' };" ^
  "Write-Output ($u + ';' + $m)"`) do set "PATH=%%P"

rem Add common install paths as a safety net
if exist "%ProgramFiles%\Git\cmd\git.exe" set "PATH=%ProgramFiles%\Git\cmd;%PATH%"
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"
if exist "%LocalAppData%\Microsoft\dotnet\dotnet.exe" set "PATH=%LocalAppData%\Microsoft\dotnet;%PATH%"

exit /b 0

:EnsureGit
where /q git.exe
if not errorlevel 1 (
  for /f "tokens=*" %%G in ('git --version') do echo [OK] %%G
  exit /b 0
)

echo [INFO] Git not found.
if "%HAS_WINGET%"=="1" (
  echo [INFO] Installing Git via winget...
  winget install --id Git.Git -e --source winget
  if errorlevel 1 (
    echo [ERROR] Failed to install Git via winget.
    exit /b 1
  )
  call :ReloadPath
  rem We purposely restart the script to ensure new process sees the updated PATH.
  call :RestartSelf
  exit /b 0
) else (
  echo [ERROR] Please install Git first, then rerun this script.
  exit /b 1
)

:EnsureDotnet8
where /q dotnet.exe
if errorlevel 1 (
  echo [INFO] dotnet not found.
  goto :InstallDotnet
)

set "DOTNET8_LATEST="
for /f "usebackq delims=" %%V in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$min = [version]'%MIN_DOTNET%';" ^
  "$sdks = & dotnet --list-sdks 2>$null;" ^
  "if(-not $sdks){ exit 2 }" ^
  "$v = $sdks | ForEach-Object { ($_ -split '\s+')[0] } | Where-Object { $_ -match '^8\.' } | ForEach-Object { [version]$_ } | Sort-Object | Select-Object -Last 1;" ^
  "if(-not $v){ exit 3 }" ^
  "if($v -lt $min){ exit 4 }" ^
  "$v.ToString()"`) do set "DOTNET8_LATEST=%%V"

if defined DOTNET8_LATEST (
  echo [OK] Using .NET SDK: %DOTNET8_LATEST%
  exit /b 0
)

echo [WARN] .NET 8 SDK missing or too old (need >= %MIN_DOTNET%).
goto :InstallDotnet

:InstallDotnet
if "%HAS_WINGET%"=="1" (
  echo [INFO] Installing Microsoft .NET SDK 8 via winget...
  winget install --id Microsoft.DotNet.SDK.8 -e --source winget
  if errorlevel 1 (
    echo [ERROR] Failed to install .NET SDK 8 via winget.
    echo        Please install .NET SDK >= %MIN_DOTNET% manually and rerun.
    exit /b 1
  )
  call :ReloadPath
  call :RestartSelf
  exit /b 0
) else (
  echo [ERROR] winget not available. Please install .NET SDK >= %MIN_DOTNET% manually, then rerun.
  exit /b 1
)

:PrepareRepo
if not exist "%INSTALL_DIR%" (
  echo [INFO] Creating install directory...
  mkdir "%INSTALL_DIR%" >nul 2>&1
)

if exist "%INSTALL_DIR%\.git" (
  echo [OK] Repo found: "%INSTALL_DIR%"
  exit /b 0
)

if "%REPO_URL%"=="" (
  echo.
  echo [INPUT] "%INSTALL_DIR%" is not a git repo.
  echo         Please paste the repository clone URL and press Enter.
  echo         Example: https://github.com/OWNER/REPO.git
  set /p "REPO_URL=Repo URL: "
)

if "%REPO_URL%"=="" (
  echo [ERROR] No REPO_URL provided.
  exit /b 1
)

echo [INFO] Cloning repository into "%INSTALL_DIR%"...
git clone "%REPO_URL%" "%INSTALL_DIR%"
if errorlevel 1 (
  echo [ERROR] git clone failed.
  exit /b 1
)

pushd "%INSTALL_DIR%" >nul
git submodule update --init --recursive
popd >nul

echo [OK] Repo cloned.
exit /b 0

:UpdateIfNeeded
pushd "%INSTALL_DIR%" >nul

echo [INFO] Checking for updates...
git fetch origin main
if errorlevel 1 (
  echo [ERROR] git fetch failed.
  popd >nul
  exit /b 1
)

for /f "delims=" %%H in ('git rev-parse HEAD') do set "LOCAL_COMMIT=%%H"
for /f "delims=" %%H in ('git rev-parse origin/main') do set "REMOTE_COMMIT=%%H"

if /i "!LOCAL_COMMIT!"=="!REMOTE_COMMIT!" (
  echo [OK] Already latest commit: !LOCAL_COMMIT:~0,8!
  popd >nul
  exit /b 0
)

echo [INFO] New version available
echo        Current: !LOCAL_COMMIT:~0,8!
echo        Latest : !REMOTE_COMMIT:~0,8!
echo [INFO] Updating...
git checkout main
git pull origin main
if errorlevel 1 (
  echo [ERROR] git pull failed.
  popd >nul
  exit /b 1
)

git submodule update --init --recursive
if errorlevel 1 (
  echo [ERROR] submodule update failed.
  popd >nul
  exit /b 1
)

popd >nul
exit /b 0

:Build
pushd "%INSTALL_DIR%" >nul

set "RID=win-x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "RID=win-arm64"

echo [INFO] Building VRCFT for %RID% ...

if not exist "%BUILD_PROJECT%" (
  echo [ERROR] Project file not found: "%BUILD_PROJECT%"
  popd >nul
  exit /b 1
)

rem Try "Windows Release" first; fallback to "Release"
dotnet publish "%BUILD_PROJECT%" -r %RID% -c "Windows Release" --self-contained -f net8.0
if errorlevel 1 (
  echo [WARN] Build with config "Windows Release" failed, trying "Release"...
  dotnet publish "%BUILD_PROJECT%" -r %RID% -c "Release" --self-contained -f net8.0
  if errorlevel 1 (
    echo [ERROR] dotnet publish failed.
    popd >nul
    exit /b 1
  )
)

echo [OK] Build complete.
popd >nul
exit /b 0

:RunApp
pushd "%INSTALL_DIR%" >nul

set "APP_EXE="
for /f "usebackq delims=" %%P in (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$root = Resolve-Path '.'; " ^
  "$p = Get-ChildItem -Path $root -Recurse -File -Filter 'VRCFaceTracking.Avalonia.Desktop.exe' -ErrorAction SilentlyContinue | " ^
  "Sort-Object FullName | Select-Object -First 1; " ^
  "if($p){ $p.FullName }"`) do set "APP_EXE=%%P"

if "%APP_EXE%"=="" (
  echo [ERROR] VRCFT binary not found. Please run the installer again.
  popd >nul
  exit /b 1
)

echo [INFO] Starting VRCFT...
"%APP_EXE%"
popd >nul
exit /b 0

:fail
echo.
echo [FAILED] Installer stopped due to an error.
pause
exit /b 1
