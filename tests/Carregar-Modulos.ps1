<#
.SYNOPSIS
    Carrega os módulos do projeto no escopo do arquivo de teste (usar com ". .\Carregar-Modulos.ps1").
#>

$RepoRoot = Split-Path -Parent $PSScriptRoot
foreach ($ModuleFolder in @('src\Core', 'src\UI')) {
    foreach ($ModuleFile in (Get-ChildItem -LiteralPath (Join-Path $RepoRoot $ModuleFolder) -Filter '*.ps1' | Sort-Object Name)) {
        . $ModuleFile.FullName
    }
}
