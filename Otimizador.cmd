@echo off
setlocal EnableDelayedExpansion

:: Remove BOM artifacts and guarantee clean CMD execution
set "batchDir=%~dp0"
set "psScript=%batchDir%Run.ps1"

:: Se o usuario passou -CLI, mantem o terminal aberto
if "%1"=="-CLI" goto :RunCLI
if "%1"=="-cli" goto :RunCLI

:: MODO PADRAO (GUI MODERNA): Lanca o PowerShell como Administrador em segundo plano e fecha este CMD
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p='%psScript:'=''%'; $q=[char]34; Start-Process powershell -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ' + $q + $p + $q) -Verb RunAs"
exit /b

:RunCLI
:: MODO TERMINAL (CLI INTERATIVO)
NET FILE 1>NUL 2>NUL
if '%errorlevel%' NEQ '0' (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$p='%psScript:'=''%'; $q=[char]34; Start-Process powershell -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File ' + $q + $p + $q + ' -CLI') -Verb RunAs"
    exit /b
)
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%psScript%" -CLI %*
exit /b
