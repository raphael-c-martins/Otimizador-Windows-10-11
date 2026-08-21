<#
.SYNOPSIS
    Módulo de Proteção de Privacidade, Bloqueio de Telemetria e IA Invasiva.
.DESCRIPTION
    Desativa coleta de dados diagnósticos, anúncios direcionados, Windows Recall, Click-to-Do,
    Copilot, busca online no menu Iniciar e serviços de telemetria do Edge.
#>

function Disable-SystemTelemetry {
    try {
        # 1. Serviço DiagTrack
        Stop-Service -Name "DiagTrack" -Force -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "DiagTrack" -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null

        # 2. Serviço dmwappushservice
        Stop-Service -Name "dmwappushservice" -Force -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "dmwappushservice" -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null

        # 3. Políticas de Telemetria e Coleta de Dados
        $DataCollection = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection"
        if (!(Test-Path $DataCollection)) { New-Item -Path $DataCollection -Force | Out-Null }
        Set-ItemProperty -Path $DataCollection -Name "AllowTelemetry" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $DataCollection -Name "MaxTelemetryAllowed" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # 4. Desativar ID de Anúncios e Experiências Personalizadas
        $Adv = "HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo"
        if (!(Test-Path $Adv)) { New-Item -Path $Adv -Force | Out-Null }
        Set-ItemProperty -Path $Adv -Name "Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        $Privacy = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy"
        if (!(Test-Path $Privacy)) { New-Item -Path $Privacy -Force | Out-Null }
        Set-ItemProperty -Path $Privacy -Name "TailoredExperiencesWithDiagnosticDataEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # 5. Histórico de Atividades
        $SystemPolicy = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System"
        if (!(Test-Path $SystemPolicy)) { New-Item -Path $SystemPolicy -Force | Out-Null }
        Set-ItemProperty -Path $SystemPolicy -Name "PublishUserActivities" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $SystemPolicy -Name "UploadUserActivities" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # 6. Frequência de Feedback (Nunca)
        $Siuf = "HKCU:\SOFTWARE\Microsoft\Siuf\Rules"
        if (!(Test-Path $Siuf)) { New-Item -Path $Siuf -Force | Out-Null }
        Set-ItemProperty -Path $Siuf -Name "NumberOfSIUFInPeriod" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # 7. Desativar Tarefas Agendadas de Telemetria
        $Tasks = @(
            "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            "\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            "\Microsoft\Windows\Autochk\Proxy",
            "\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            "\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            "\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"
        )
        foreach ($Task in $Tasks) {
            Disable-ScheduledTask -TaskPath ($Task.Substring(0, $Task.LastIndexOf('\') + 1)) -TaskName ($Task.Substring($Task.LastIndexOf('\') + 1)) -ErrorAction SilentlyContinue | Out-Null
        }
    } catch {}
}

function Disable-WindowsAIAndRecall {
    param([bool]$IsWin11 = $true)
    try {
        # 1. Desativar Windows Recall e Análise de IA Local
        $AI = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI"
        if (!(Test-Path $AI)) { New-Item -Path $AI -Force | Out-Null }
        Set-ItemProperty -Path $AI -Name "DisableAIDataAnalysis" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $AI -Name "TurnOffWindowsRecall" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $AI -Name "DisableClickToDo" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # 2. Desativar Microsoft Copilot
        $CopilotLM = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot"
        if (!(Test-Path $CopilotLM)) { New-Item -Path $CopilotLM -Force | Out-Null }
        Set-ItemProperty -Path $CopilotLM -Name "TurnOffWindowsCopilot" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        $CopilotCU = "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot"
        if (!(Test-Path $CopilotCU)) { New-Item -Path $CopilotCU -Force | Out-Null }
        Set-ItemProperty -Path $CopilotCU -Name "TurnOffWindowsCopilot" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # Ocultar botão do Copilot na barra de tarefas
        $ExpAdv = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
        if (!(Test-Path $ExpAdv)) { New-Item -Path $ExpAdv -Force | Out-Null }
        Set-ItemProperty -Path $ExpAdv -Name "ShowCopilotButton" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # 3. Desativar inicialização automática do serviço WSAIFabricSvc
        Set-Service -Name "WSAIFabricSvc" -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null
        Stop-Service -Name "WSAIFabricSvc" -Force -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}

function Disable-WindowsSuggestionsAndAds {
    try {
        $CDM = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
        if (!(Test-Path $CDM)) { New-Item -Path $CDM -Force | Out-Null }
        Set-ItemProperty -Path $CDM -Name "SubscribedContent-310093Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SubscribedContent-338388Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SubscribedContent-338389Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SubscribedContent-338393Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SubscribedContent-353694Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SubscribedContent-353696Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SubscribedContent-353698Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SystemPaneSuggestionsEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SoftLandingEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $CDM -Name "SilentInstalledAppsEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        $ExpAdv = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
        if (!(Test-Path $ExpAdv)) { New-Item -Path $ExpAdv -Force | Out-Null }
        Set-ItemProperty -Path $ExpAdv -Name "Start_IrisRecommendations" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $ExpAdv -Name "ShowSyncProviderNotifications" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $ExpAdv -Name "Start_AccountNotifications" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $ExpAdv -Name "Start_TrackProgs" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}

function Disable-EdgeBackgroundAndTelemetry {
    try {
        $Edge = "HKLM:\SOFTWARE\Policies\Microsoft\Edge"
        if (!(Test-Path $Edge)) { New-Item -Path $Edge -Force | Out-Null }
        Set-ItemProperty -Path $Edge -Name "HubsSidebarEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $Edge -Name "SpotlightExperiencesAndRecommendationsEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $Edge -Name "StartupBoostEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $Edge -Name "BackgroundModeEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $Edge -Name "PersonalizationReportingEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $Edge -Name "DiagnosticData" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $Edge -Name "ComposeInlineEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}
