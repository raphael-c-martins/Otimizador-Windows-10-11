<#
.SYNOPSIS
    Módulo de Reversão e Restauração de Configurações do Sistema (Undo Engine).
.DESCRIPTION
    Modo principal: lê os diários de alteração (estado_*.json) do mais novo para o mais antigo e devolve
    cada valor de registro, serviço, tarefa e plano de energia ao estado exato anterior à otimização.
    Modo alternativo (sem diário, ex.: otimizações feitas pela v13): restaura os padrões do Windows.
    Aplicativos removidos e o Microsoft Edge NÃO são reinstalados (devem ser obtidos na Microsoft Store).
#>

function Invoke-SystemUndo {
    param(
        [Parameter(Mandatory)][string]$JournalDirectory,
        [bool]$IsWin11 = $true,
        [switch]$DryRun,
        [switch]$SkipExplorerRestart
    )

    $Journals = Get-PendingChangeJournals -Directory $JournalDirectory
    $Result = [pscustomobject]@{ Mode = ''; Files = $Journals.Count; Restored = 0; Failed = 0 }

    if ($Journals.Count -gt 0) {
        $Result.Mode = 'Diario'
        Write-OptimizerLog -Level ETAPA -Message "Revertendo $($Journals.Count) execução(ões) registradas no diário de alterações"

        foreach ($File in $Journals) {
            if ($DryRun) {
                $Count = @((Get-Content -LiteralPath $File.FullName -Raw -Encoding UTF8 | ConvertFrom-Json).Entradas).Count
                Write-OptimizerLog -Level INFO -Message "[SIMULAÇÃO] $($File.Name): $Count alterações seriam revertidas."
                continue
            }
            $Partial = Restore-ChangeJournalFile -Path $File.FullName
            $Result.Restored += $Partial.Restored
            $Result.Failed += $Partial.Failed
            if ($Partial.Failed -eq 0) {
                Rename-Item -LiteralPath $File.FullName -NewName ($File.Name -replace '\.json$', '.restaurado.json')
                Write-OptimizerLog -Level OK -Message "$($File.Name): $($Partial.Restored) alterações revertidas."
            } else {
                Write-OptimizerLog -Level AVISO -Message "$($File.Name): $($Partial.Restored) revertidas, $($Partial.Failed) falharam (o diário foi mantido para nova tentativa)."
            }
        }
    } else {
        $Result.Mode = 'Padroes'
        if ($DryRun) {
            Write-OptimizerLog -Level INFO -Message '[SIMULAÇÃO] Nenhum diário de alterações encontrado: os padrões do Windows seriam restaurados para os itens conhecidos.'
        } else {
            Write-OptimizerLog -Level AVISO -Message 'Nenhum diário de alterações encontrado: restaurando os padrões do Windows para os itens conhecidos.'
            $Partial = Invoke-WindowsDefaultsRestore -IsWin11 $IsWin11
            $Result.Restored = $Partial.Restored
            $Result.Failed = $Partial.Failed
        }
    }

    if (-not $DryRun -and -not $SkipExplorerRestart) {
        try { Restart-WindowsExplorer } catch { Write-OptimizerLog -Level AVISO -Message "Explorer não reiniciado: $($_.Exception.Message)" }
    }
    Write-OptimizerLog -Level INFO -Message 'Aplicativos removidos não são reinstalados automaticamente: use a Microsoft Store se precisar de algum.'
    return $Result
}

function Invoke-WindowsDefaultsRestore {
    <#
    .SYNOPSIS
        Restauração genérica para sistemas otimizados por versões sem diário de alterações.
    #>
    param([bool]$IsWin11 = $true)

    $Actions = [ordered]@{
        'Serviços SysMain e Windows Search' = {
            Set-Service -Name 'SysMain' -StartupType Automatic -ErrorAction Stop
            Start-Service -Name 'SysMain' -ErrorAction SilentlyContinue
            Set-Service -Name 'WSearch' -StartupType Automatic -ErrorAction Stop
            Start-Service -Name 'WSearch' -ErrorAction SilentlyContinue
        }
        'Serviços de telemetria' = {
            Set-Service -Name 'DiagTrack' -StartupType Automatic -ErrorAction Stop
            Set-Service -Name 'dmwappushservice' -StartupType Manual -ErrorAction SilentlyContinue
        }
        'Menu de contexto do Windows 11' = {
            if ($IsWin11) {
                $Base = Open-BaseRegistryKey -Hive (ConvertTo-RegistryLocation -Path 'HKCU:\Software').Hive
                try { $Base.DeleteSubKeyTree((ConvertTo-RegistryLocation -Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}').SubKey, $false) } finally { $Base.Dispose() }
            }
        }
        'Protocolos e gravação do Xbox' = {
            Restore-GameBarProtocols
            Set-RegistryValue -Path 'HKCU:\System\GameConfigStore' -Name 'GameDVR_Enabled' -Value 1
            Set-RegistryValue -Path 'HKCU:\SOFTWARE\Microsoft\GameBar' -Name 'UseNexusForGameBarEnabled' -Value 1
        }
        'Efeitos visuais e transparência' = {
            Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' -Name 'VisualFXSetting' -Value 0
            Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name 'EnableTransparency' -Value 1
            Set-RegistryValue -Path 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name 'MinAnimate' -Value '1' -Type String
        }
        'Hibernação e Inicialização Rápida' = {
            & powercfg.exe /hibernate on | Out-Null
            Set-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -Name 'HiberbootEnabled' -Value 1
        }
    }

    $Restored = 0
    $Failed = 0
    foreach ($Name in $Actions.Keys) {
        try {
            & $Actions[$Name]
            $Restored++
            Write-OptimizerLog -Level OK -Message "$Name restaurado(s)."
        } catch {
            $Failed++
            Write-OptimizerLog -Level ERRO -Message "$Name`: $($_.Exception.Message)"
        }
    }
    return [pscustomobject]@{ Restored = $Restored; Failed = $Failed }
}
