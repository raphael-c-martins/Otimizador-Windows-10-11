<#
.SYNOPSIS
    Módulo de Proteção de Privacidade, Bloqueio de Telemetria e IA Invasiva.
.DESCRIPTION
    Cada função corresponde a UM ajuste do catálogo (Config/Tweaks.json), para que marcar ou desmarcar
    uma opção na interface tenha exatamente o efeito descrito. As alterações passam pelo diário de reversão.
#>

function Disable-SystemTelemetry {
    # 1. Serviços de telemetria (Experiência do Usuário Conectado e roteamento de push WAP)
    Set-ServiceStartupTracked -Name 'DiagTrack' -StartupType Disabled -Stop
    Set-ServiceStartupTracked -Name 'dmwappushservice' -StartupType Disabled -Stop

    # 2. Políticas de coleta de dados
    $DataCollection = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection'
    Set-RegistryValue -Path $DataCollection -Name 'AllowTelemetry' -Value 0
    Set-RegistryValue -Path $DataCollection -Name 'MaxTelemetryAllowed' -Value 0

    # 3. ID de anúncios e experiências personalizadas
    Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0
    Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy' -Name 'TailoredExperiencesWithDiagnosticDataEnabled' -Value 0

    # 4. Frequência de pedidos de feedback (Nunca)
    Set-RegistryValue -Path 'HKCU:\SOFTWARE\Microsoft\Siuf\Rules' -Name 'NumberOfSIUFInPeriod' -Value 0

    # 5. Tarefas agendadas de telemetria
    $Tasks = @(
        '\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser',
        '\Microsoft\Windows\Application Experience\ProgramDataUpdater',
        '\Microsoft\Windows\Autochk\Proxy',
        '\Microsoft\Windows\Customer Experience Improvement Program\Consolidator',
        '\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip',
        '\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector'
    )
    foreach ($Task in $Tasks) {
        Disable-ScheduledTaskTracked -FullName $Task
    }
}

function Disable-ActivityHistory {
    $SystemPolicy = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System'
    Set-RegistryValue -Path $SystemPolicy -Name 'EnableActivityFeed' -Value 0
    Set-RegistryValue -Path $SystemPolicy -Name 'PublishUserActivities' -Value 0
    Set-RegistryValue -Path $SystemPolicy -Name 'UploadUserActivities' -Value 0
}

function Disable-WindowsSuggestionsAndAds {
    $CDM = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
    $CdmValues = @(
        'SubscribedContent-310093Enabled', 'SubscribedContent-338388Enabled', 'SubscribedContent-338389Enabled',
        'SubscribedContent-338393Enabled', 'SubscribedContent-353694Enabled', 'SubscribedContent-353696Enabled',
        'SubscribedContent-353698Enabled', 'SystemPaneSuggestionsEnabled', 'SoftLandingEnabled', 'SilentInstalledAppsEnabled'
    )
    foreach ($ValueName in $CdmValues) {
        Set-RegistryValue -Path $CDM -Name $ValueName -Value 0
    }

    $ExpAdv = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
    foreach ($ValueName in @('Start_IrisRecommendations', 'ShowSyncProviderNotifications', 'Start_AccountNotifications', 'Start_TrackProgs')) {
        Set-RegistryValue -Path $ExpAdv -Name $ValueName -Value 0
    }
}

function Disable-StartMenuWebSearch {
    Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\SearchSettings' -Name 'IsDynamicSearchBoxEnabled' -Value 0
    Set-RegistryValue -Path 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' -Name 'DisableSearchBoxSuggestions' -Value 1
}

function Disable-WindowsRecallAndAI {
    param([bool]$IsWin11 = $true)

    if (-not $IsWin11) {
        Write-OptimizerLog -Level INFO -Message 'Recall e Click-to-Do existem apenas no Windows 11 (nada a fazer).'
        return
    }

    $AI = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI'
    Set-RegistryValue -Path $AI -Name 'DisableAIDataAnalysis' -Value 1
    Set-RegistryValue -Path $AI -Name 'TurnOffWindowsRecall' -Value 1
    Set-RegistryValue -Path $AI -Name 'DisableClickToDo' -Value 1

    # Serviço de base dos recursos de IA local (presente apenas no 24H2+)
    Set-ServiceStartupTracked -Name 'WSAIFabricSvc' -StartupType Disabled -Stop
}

function Disable-WindowsCopilot {
    Set-RegistryValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot' -Name 'TurnOffWindowsCopilot' -Value 1
    Set-RegistryValue -Path 'HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot' -Name 'TurnOffWindowsCopilot' -Value 1
    Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowCopilotButton' -Value 0
}

function Disable-EdgeBackgroundAndTelemetry {
    $Edge = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'
    $EdgePolicies = @(
        'HubsSidebarEnabled', 'SpotlightExperiencesAndRecommendationsEnabled', 'StartupBoostEnabled',
        'BackgroundModeEnabled', 'PersonalizationReportingEnabled', 'DiagnosticData', 'ComposeInlineEnabled'
    )
    foreach ($Policy in $EdgePolicies) {
        Set-RegistryValue -Path $Edge -Name $Policy -Value 0
    }
}
