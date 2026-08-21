<#
.SYNOPSIS
    Módulo de Ajustes de Performance, Efeitos Visuais, Sistema e Limpeza.
.DESCRIPTION
    Aplica otimizações no Explorer, gerenciamento de energia, desativação de SysMain/WSearch,
    limpeza profunda de temporários e remoção forçada do Microsoft Edge quando solicitado.
#>

function Set-ExplorerAndUITweaks {
    param([bool]$IsWin11 = $true)
    try {
        # 1. Exibir extensões de arquivos
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "HideFileExt" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # 2. Ocultar botões TaskView e Chat
        $ExpAdv = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
        Set-ItemProperty -Path $ExpAdv -Name "ShowTaskViewButton" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        if ($IsWin11) {
            Set-ItemProperty -Path $ExpAdv -Name "TaskbarMn" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        } else {
            Set-ItemProperty -Path $ExpAdv -Name "PeopleBand" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        }

        # 3. Restaurar menu de contexto clássico (estritamente no Windows 11)
        if ($IsWin11) {
            $Key = "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"
            if (!(Test-Path $Key)) { New-Item -Path $Key -Force | Out-Null }
            Set-ItemProperty -Path $Key -Name "(default)" -Value "" -Force -ErrorAction SilentlyContinue | Out-Null
        }
    } catch {}
}

function Set-PowerAndPerformanceTweaks {
    try {
        # 1. Desativar Fast Startup / Hibernação
        powercfg -h off | Out-Null

        # 2. Desbloquear e ativar o esquema Ultimate Performance
        $DupOutput = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 2>&1
        if ($DupOutput -match "([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})") {
            $Guid = $matches[1]
            powercfg -setactive $Guid | Out-Null
        } else {
            powercfg -duplicatescheme 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 2>&1 | Out-Null
            powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 2>&1 | Out-Null
        }

        # 3. Ajuste de modulação gráfica (Desativa sombras lentas, mantém fontes lisas)
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name "VisualFXSetting" -Value 3 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        
        $Theme = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
        if (!(Test-Path $Theme)) { New-Item -Path $Theme -Force | Out-Null }
        Set-ItemProperty -Path $Theme -Name "EnableTransparency" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        
        Set-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "MinAnimate" -Value "0" -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothing" -Value "2" -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothingType" -Value 2 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}

function Disable-SearchHighlightsAndWeb {
    try {
        $Search = "HKCU:\Software\Microsoft\Windows\CurrentVersion\SearchSettings"
        if (!(Test-Path $Search)) { New-Item -Path $Search -Force | Out-Null }
        Set-ItemProperty -Path $Search -Name "IsDynamicSearchBoxEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        $ExpSearch = "HKCU:\Software\Policies\Microsoft\Windows\Explorer"
        if (!(Test-Path $ExpSearch)) { New-Item -Path $ExpSearch -Force | Out-Null }
        Set-ItemProperty -Path $ExpSearch -Name "DisableSearchBoxSuggestions" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}

function Disable-LegacyHeavyServices {
    <#
    .SYNOPSIS
        Desativa SysMain e WSearch para hardware com HDD mecânico ou baixa densidade de RAM.
    #>
    try {
        Stop-Service -Name "WSearch" -Force -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "WSearch" -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null

        Stop-Service -Name "SysMain" -Force -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "SysMain" -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}

function Invoke-EdgeForceRemoveClean {
    <#
    .SYNOPSIS
        Desinstalação profunda do Microsoft Edge com limpeza de stubs e atalhos.
    #>
    try {
        $Key = "HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev"
        if (!(Test-Path $Key)) { New-Item -Path $Key -Force | Out-Null }
        Set-ItemProperty -Path $Key -Name "AllowUninstall" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # Stub de compatibilidade
        $EdgeStub = "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe"
        if (!(Test-Path $EdgeStub)) { New-Item $EdgeStub -ItemType Directory -Force | Out-Null }
        New-Item "$EdgeStub\MicrosoftEdge.exe" -ItemType File -Force -ErrorAction SilentlyContinue | Out-Null

        $UninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"
        if (Test-Path $UninstallKey) {
            $UnStr = (Get-ItemProperty -Path $UninstallKey -ErrorAction SilentlyContinue).UninstallString
            if (-not [string]::IsNullOrWhiteSpace($UnStr)) {
                $Cmd = "$UnStr --force-uninstall"
                Start-Process cmd.exe -ArgumentList "/c $Cmd" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue | Out-Null
            }
        }

        # Remoção de atalhos residuais
        $Shortcuts = @(
            "$env:PUBLIC\Desktop\Microsoft Edge.lnk",
            "$env:USERPROFILE\Desktop\Microsoft Edge.lnk",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk",
            "$env:APPDATA\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Microsoft Edge.lnk"
        )
        foreach ($Lnk in $Shortcuts) {
            if (Test-Path $Lnk) { Remove-Item $Lnk -Force -ErrorAction SilentlyContinue | Out-Null }
        }
    } catch {}
}

function Clear-SystemTempFiles {
    try {
        Remove-Item "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "C:\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}

function Restart-WindowsExplorer {
    try {
        Stop-Process -Name "explorer" -Force -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}
