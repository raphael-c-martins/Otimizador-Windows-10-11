<#
.SYNOPSIS
    Módulo de Log de Auditoria (arquivo persistente + saída em tela).
.DESCRIPTION
    Toda mensagem vai para o arquivo de log em UTF-8 e, opcionalmente, para um "sink" (função de saída)
    definido pela interface ativa: console colorido no modo terminal ou fila de eventos na GUI.
    Os avisos emitidos durante uma etapa são contados para que a etapa seja reportada como "AVISO".
#>

$script:LogFilePath = $null
$script:LogSink = $null
$script:ProgressSink = $null
$script:StepWarningCount = 0

function Initialize-SecureDataDirectory {
    <#
    .SYNOPSIS
        Cria a pasta de dados e restringe a escrita a Administradores e SYSTEM.
    .DESCRIPTION
        O ProgramData permite, por padrão, que usuários comuns criem arquivos. Como o programa roda como
        Administrador, uma pasta gravável por usuário comum permitiria adulterar scripts ou logs.
    #>
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null }

    $null = & icacls.exe $Path /setowner '*S-1-5-32-544' /T /C /Q
    $null = & icacls.exe $Path /reset /T /C /Q
    $null = & icacls.exe $Path /inheritance:r /grant:r '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' /C /Q
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao proteger a pasta de dados '$Path' (icacls retornou $LASTEXITCODE)."
    }
}

function New-OptimizerLogFile {
    param([Parameter(Mandatory)][string]$Directory)

    if (-not (Test-Path -LiteralPath $Directory)) { New-Item -ItemType Directory -Path $Directory -Force | Out-Null }
    $Stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
    return (Join-Path $Directory "Otimizacao_$($env:COMPUTERNAME)_$Stamp.log")
}

function Set-OptimizerLogTarget {
    param(
        [string]$FilePath = $null,
        [scriptblock]$Sink = $null,
        [scriptblock]$ProgressSink = $null
    )
    $script:LogFilePath = $FilePath
    $script:LogSink = $Sink
    $script:ProgressSink = $ProgressSink
}

function Get-OptimizerLogFilePath {
    return $script:LogFilePath
}

function Write-OptimizerLog {
    param(
        [ValidateSet('INFO', 'OK', 'AVISO', 'ERRO', 'ETAPA')]
        [string]$Level = 'INFO',
        [Parameter(Mandatory)][AllowEmptyString()][string]$Message
    )

    if ($Level -eq 'AVISO') { $script:StepWarningCount++ }

    if (-not [string]::IsNullOrWhiteSpace($script:LogFilePath)) {
        $Line = '[{0}] [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
        try {
            [System.IO.File]::AppendAllText($script:LogFilePath, $Line + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($true))
        } catch {}
    }

    if ($null -ne $script:LogSink) {
        try { & $script:LogSink $Level $Message } catch {}
    }
}

function Write-OptimizerProgress {
    param(
        [int]$Current,
        [int]$Total,
        [string]$Activity
    )
    if ($null -ne $script:ProgressSink) {
        try { & $script:ProgressSink $Current $Total $Activity } catch {}
    }
}

function Reset-StepWarningCount {
    $script:StepWarningCount = 0
}

function Get-StepWarningCount {
    return $script:StepWarningCount
}
