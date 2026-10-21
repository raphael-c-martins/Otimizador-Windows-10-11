<#
.SYNOPSIS
    Constantes globais do Otimizador (versão e pastas de dados).
.DESCRIPTION
    A versão fica centralizada aqui para que GUI, terminal e logs exibam sempre o mesmo número.
#>

$script:OptimizerVersion = '14.0'

# Pasta de dados protegida (somente Administradores e SYSTEM escrevem): logs, diários de alteração e estado.
$script:OptimizerDataRoot = Join-Path $env:ProgramData 'OtimizadorWindows'

# Pasta usada pelo modo de simulação (-DryRun), que roda sem privilégio e não altera o sistema.
$script:OptimizerDryRunRoot = Join-Path $env:TEMP 'OtimizadorWindows-simulacao'

# Códigos de saída do Run.ps1 (úteis para automação via -Silent).
$script:ExitCodes = @{
    Sucesso           = 0
    ErroDeUso         = 1
    FalhasParciais    = 2
    AbortadoSemBackup = 3
}
