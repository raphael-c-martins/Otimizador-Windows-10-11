@echo off
setlocal

:: Launcher do Otimizador de Windows (arquivo ASCII, sem BOM)
set "batchDir=%~dp0"
set "psScript=%batchDir%Run.ps1"

:: Se o usuario passou -CLI, mantem o terminal aberto
if /I "%~1"=="-CLI" goto :RunCLI

:: MODO PADRAO (GUI): abre o PowerShell como Administrador com a janela oculta.
:: -LaunchedHidden avisa o Run.ps1 que, se a GUI falhar, ele deve abrir o modo terminal em um console visivel.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p='%psScript:'=''%'; $q=[char]34; Start-Process powershell -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ' + $q + $p + $q + ' -LaunchedHidden') -Verb RunAs"
exit /b

:RunCLI
:: MODO TERMINAL (CLI INTERATIVO)
NET FILE 1>NUL 2>NUL
if errorlevel 1 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$p='%psScript:'=''%'; $q=[char]34; Start-Process powershell -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File ' + $q + $p + $q + ' -CLI') -Verb RunAs"
    exit /b
)
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%psScript%" %*
exit /b
