. (Join-Path $PSScriptRoot 'Carregar-Modulos.ps1')

Describe 'Execução em segundo plano (runspace usado pela GUI)' {
    It 'roda a simulação em outra thread e entrega logs, progresso e resultado' {
        $LogFile = Join-Path $TestDrive 'segundo-plano.log'
        $Arguments = @{ AppIds = @('BingNews'); TweakIds = @('ShowFileExtensions'); Context = @{}; DryRun = $true }
        $Task = Start-OptimizerBackgroundTask -ScriptDir $RepoRoot -LogFilePath $LogFile -Operation Optimize -Arguments $Arguments

        $Events = [System.Collections.Generic.List[object]]::new()
        $Timer = [System.Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-OptimizerBackgroundTaskCompleted -Task $Task) -and $Timer.Elapsed.TotalSeconds -lt 60) {
            foreach ($Item in (Receive-OptimizerBackgroundEvents -Task $Task)) { $Events.Add($Item) }
            Start-Sleep -Milliseconds 100
        }
        foreach ($Item in (Receive-OptimizerBackgroundEvents -Task $Task)) { $Events.Add($Item) }
        $Completion = Complete-OptimizerBackgroundTask -Task $Task

        $Completion.Errors.Count | Should Be 0
        $Completion.Result.Simulated | Should Be 3
        @($Events | Where-Object Type -eq 'Log').Count | Should BeGreaterThan 0
        @($Events | Where-Object Type -eq 'Progress').Count | Should BeGreaterThan 0
        Test-Path -LiteralPath $LogFile | Should Be $true
    }
}

Describe 'Janela principal (WPF)' {
    It 'carrega o XAML com todos os controles nomeados' {
        $Ui = New-MainWindow
        $Ui.Controls.Count | Should BeGreaterThan 30
        $Ui.Window.Close()
    }
}
