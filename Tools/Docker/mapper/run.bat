@echo off
setlocal
cd /d "%~dp0"

if "%LOGIN_HOST_USER%"=="" (
  echo Set LOGIN_HOST_USER to your SS14 account name.
  exit /b 1
)

if not exist "data" mkdir data

docker run --name grosse-map-server --rm ^
  -p 1212:1212/tcp -p 1212:1212/udp ^
  -v "%cd%\data:/data" ^
  -e LOGIN_HOST_USER=%LOGIN_HOST_USER% ^
  -e WHITELIST_USERS=%WHITELIST_USERS% ^
  repo.a.backmen.ru/grosse/map-server:latest
