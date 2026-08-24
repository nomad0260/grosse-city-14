@echo off
setlocal EnableExtensions
cd /d "%~dp0\..\..\.."

set IMAGE=repo.a.backmen.ru/grosse/map-server:latest

for /f "delims=" %%i in ('git rev-parse HEAD') do set CONTENT_VERSION=%%i
for /f "delims=" %%i in ('git -C RobustToolbox describe --tags --abbrev^=0') do set ENGINE_VERSION=%%i

if "%CONTENT_VERSION%"=="" set CONTENT_VERSION=unknown
if "%ENGINE_VERSION%"=="" set ENGINE_VERSION=unknown

echo Building %IMAGE%
echo   fork_id=grosse
echo   version=%CONTENT_VERSION%
echo   engine_version=%ENGINE_VERSION%

docker build -f Tools/Docker/mapper/Dockerfile ^
  --build-arg CONTENT_VERSION=%CONTENT_VERSION% ^
  --build-arg ENGINE_VERSION=%ENGINE_VERSION% ^
  -t %IMAGE% .

if errorlevel 1 exit /b 1

echo Pushing %IMAGE%
docker push %IMAGE%
