<#
.SYNOPSIS
    Módulo de Gerenciamento de Threads Assíncronas e Fluidez da Interface Gráfica (WPF).
.DESCRIPTION
    Fornece funções de bombeamento de mensagens da UI (Invoke-DoEvents a 60 FPS) e execução
    não-bloqueante de scripts em segundo plano (Invoke-NonBlocking) para impedir qualquer congelamento.
#>

function Invoke-DoEvents {
    <#
    .SYNOPSIS
        Processa todas as mensagens pendentes da fila da janela WPF (mouse, renderização, redimensionamento)
        mantendo a interface 100% responsiva e fluida.
    #>
    try {
        $frame = [System.Windows.Threading.DispatcherFrame]::new()
        $null = [System.Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke(
            [System.Windows.Threading.DispatcherPriority]::Background,
            [System.Windows.Threading.DispatcherOperationCallback]{
                param($f)
                $f.Continue = $false
                return $null
            },
            $frame
        )
        $null = [System.Windows.Threading.Dispatcher]::PushFrame($frame)
    } catch {}
}

function Invoke-NonBlocking {
    <#
    .SYNOPSIS
        Executa um ScriptBlock em um Runspace assíncrono em segundo plano enquanto mantém a UI fluida.
    #>
    param(
        [scriptblock]$ScriptBlock,
        [object[]]$ArgumentList = @(),
        [int]$TimeoutSeconds = 0
    )

    $ps = [powershell]::Create()
    try {
        $null = $ps.AddScript($ScriptBlock.ToString())
        foreach ($arg in $ArgumentList) {
            $null = $ps.AddArgument($arg)
        }

        $handle = $ps.BeginInvoke()

        $stopwatch = if ($TimeoutSeconds -gt 0) { [System.Diagnostics.Stopwatch]::StartNew() } else { $null }

        while (-not $handle.IsCompleted) {
            if ($stopwatch -and $stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                $ps.Stop()
                throw "Operação expirou o tempo limite de $TimeoutSeconds segundos"
            }
            Invoke-DoEvents
            Start-Sleep -Milliseconds 16 # 60 FPS UI Pumping
        }

        $result = $ps.EndInvoke($handle)
        return $result
    }
    finally {
        $ps.Dispose()
    }
}
