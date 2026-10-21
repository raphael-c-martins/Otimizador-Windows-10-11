<#
.SYNOPSIS
    Módulo de Execução em Segundo Plano (mantém a janela gráfica responsiva).
.DESCRIPTION
    A GUI não executa nenhuma otimização na sua própria thread: cada operação roda em um runspace
    (uma instância separada do PowerShell, em outra thread). O runspace envia logs e progresso por uma
    fila segura entre threads, que a janela lê periodicamente com um temporizador.
#>

$script:BackgroundTaskScript = @'
param($TaskScriptDir, $TaskLogFile, $TaskOperation, $TaskArguments, $TaskQueue)

# Os módulos declaram variáveis de escopo "script" (ex.: $script:LogFilePath). Como este script roda no
# escopo global do runspace, os parâmetros usam o prefixo "Task" para não serem sobrescritos ao carregar os módulos.
foreach ($ModuleFile in (Get-ChildItem -LiteralPath (Join-Path $TaskScriptDir 'src\Core') -Filter '*.ps1' | Sort-Object Name)) {
    . $ModuleFile.FullName
}

$LogSink = {
    param($Level, $Message)
    $TaskQueue.Enqueue([pscustomobject]@{ Type = 'Log'; Level = $Level; Message = $Message; Time = (Get-Date) })
}.GetNewClosure()
$ProgressSink = {
    param($Current, $Total, $Activity)
    $TaskQueue.Enqueue([pscustomobject]@{ Type = 'Progress'; Current = $Current; Total = $Total; Activity = $Activity })
}.GetNewClosure()
Set-OptimizerLogTarget -FilePath $TaskLogFile -Sink $LogSink -ProgressSink $ProgressSink

try {
    switch ($TaskOperation) {
        'RestorePoint' {
            try {
                Invoke-SystemRestorePoint -Description $TaskArguments.Description | Out-Null
                [pscustomobject]@{ Success = $true; Message = '' }
            } catch {
                Write-OptimizerLog -Level ERRO -Message "Ponto de Restauração não criado: $($_.Exception.Message)"
                [pscustomobject]@{ Success = $false; Message = $_.Exception.Message }
            }
        }
        'Optimize' {
            $Catalog = Get-OptimizerCatalog -ScriptDir $TaskScriptDir
            Invoke-OptimizationPlan -Catalog $Catalog -AppIds $TaskArguments.AppIds -TweakIds $TaskArguments.TweakIds -Context $TaskArguments.Context -DryRun:([bool]$TaskArguments.DryRun)
        }
        'Undo' {
            Set-UserRegistryTarget -Sid $TaskArguments.UserSid
            Invoke-SystemUndo -JournalDirectory $TaskArguments.JournalDirectory -IsWin11 ([bool]$TaskArguments.IsWin11) -DryRun:([bool]$TaskArguments.DryRun)
        }
    }
} catch {
    Write-OptimizerLog -Level ERRO -Message "Falha inesperada na operação '$TaskOperation': $($_.Exception.Message)"
    [pscustomobject]@{ Success = $false; Message = $_.Exception.Message; Unexpected = $true }
}
'@

function Start-OptimizerBackgroundTask {
    param(
        [Parameter(Mandatory)][string]$ScriptDir,
        [string]$LogFilePath = $null,
        [Parameter(Mandatory)][ValidateSet('Optimize', 'RestorePoint', 'Undo')][string]$Operation,
        [hashtable]$Arguments = @{}
    )

    $Queue = [System.Collections.Concurrent.ConcurrentQueue[object]]::new()

    $SessionState = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault()
    $SessionState.ExecutionPolicy = [Microsoft.PowerShell.ExecutionPolicy]::Bypass
    $Runspace = [runspacefactory]::CreateRunspace($SessionState)
    $Runspace.Open()

    $PowerShell = [powershell]::Create()
    $PowerShell.Runspace = $Runspace
    $null = $PowerShell.AddScript($script:BackgroundTaskScript).
        AddArgument($ScriptDir).
        AddArgument($LogFilePath).
        AddArgument($Operation).
        AddArgument($Arguments).
        AddArgument($Queue)

    return [pscustomobject]@{
        Operation  = $Operation
        PowerShell = $PowerShell
        Runspace   = $Runspace
        Handle     = $PowerShell.BeginInvoke()
        Queue      = $Queue
    }
}

function Receive-OptimizerBackgroundEvents {
    param([Parameter(Mandatory)]$Task)
    $Items = [System.Collections.Generic.List[object]]::new()
    $Item = $null
    while ($Task.Queue.TryDequeue([ref]$Item)) { $Items.Add($Item) }
    return , $Items.ToArray()
}

function Test-OptimizerBackgroundTaskCompleted {
    param([Parameter(Mandatory)]$Task)
    return $Task.Handle.IsCompleted
}

function Complete-OptimizerBackgroundTask {
    <#
    .SYNOPSIS
        Coleta o resultado de uma tarefa concluída e libera o runspace.
    #>
    param([Parameter(Mandatory)]$Task)
    try {
        $Output = $Task.PowerShell.EndInvoke($Task.Handle)
        $Errors = @($Task.PowerShell.Streams.Error | ForEach-Object { $_.ToString() })
        $Result = if ($Output.Count -gt 0) { $Output[$Output.Count - 1] } else { $null }
        return [pscustomobject]@{ Result = $Result; Errors = $Errors }
    } catch {
        return [pscustomobject]@{ Result = $null; Errors = @($_.Exception.Message) }
    } finally {
        $Task.PowerShell.Dispose()
        $Task.Runspace.Dispose()
    }
}
