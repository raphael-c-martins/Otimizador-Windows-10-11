<#
.SYNOPSIS
    Executa a suíte de testes automatizados (Pester 3.4, incluído no Windows).
.DESCRIPTION
    Rode no Windows PowerShell 5.1 (powershell.exe), que já traz o Pester 3.4 e executa WPF.
    Os testes não exigem Administrador e não alteram o sistema: usam pastas temporárias e
    uma chave de registro própria em HKCU, removida ao final.
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-Testes.ps1
#>

$ErrorActionPreference = 'Stop'
Import-Module Pester -RequiredVersion 3.4.0 -Force

$Result = Invoke-Pester -Path $PSScriptRoot -PassThru
if ($Result.FailedCount -gt 0) { exit 1 }
exit 0
