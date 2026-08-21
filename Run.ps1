<#
.SYNOPSIS
    Orquestrador Principal da Engine de Otimização e Debloat do Windows (v13.0).
.DESCRIPTION
    Ponto de entrada nativo em PowerShell com inicialização padrão na Janela Gráfica (GUI WPF),
    suporte a modo Terminal interativo (-CLI) e parâmetros de linha de comando para automação.
#>

[CmdletBinding()]
param(
    [switch]$CLI,
    [string]$Preset = "",
    [switch]$Silent,
    [switch]$SkipRestorePoint,
    [switch]$Undo,
    [string]$CustomApps = ""
)

# Forçar codificação UTF-8 no Console e no Pipeline do PowerShell
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    [Console]::InputEncoding  = [System.Text.Encoding]::UTF8
    $OutputEncoding           = [System.Text.Encoding]::UTF8
} catch {}

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# 1. Carregar Módulos do Core, UI e Threading
. "$ScriptDir\src\Core\ThreadingManager.ps1"
. "$ScriptDir\src\UI\ConsoleTUI.ps1"
. "$ScriptDir\src\UI\AppSelectionModal.ps1"
. "$ScriptDir\src\UI\MainWindowGUI.ps1"
. "$ScriptDir\src\Core\BackupManager.ps1"
. "$ScriptDir\src\Core\AppManager.ps1"
. "$ScriptDir\src\Core\GamingManager.ps1"
. "$ScriptDir\src\Core\PrivacyManager.ps1"
. "$ScriptDir\src\Core\PerformanceManager.ps1"
. "$ScriptDir\src\Core\UndoManager.ps1"

# 2. Inicializar Diretório de Logs
$LogDir = "$ScriptDir\logs"
if (!(Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
$HostName = $env:COMPUTERNAME
$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$LogPath = "$LogDir\Otimizacao_${HostName}_${Timestamp}.log"

Start-Transcript -Path $LogPath -Append -Force | Out-Null

# 3. Detectar Sistema Operacional com Precisão de Build
$IsWin11 = $false
$OSCaption = "Windows"
try {
    $OSObj = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
    $OSCaption = $OSObj.Caption
    $BuildNumber = [int]$OSObj.BuildNumber
    if ($BuildNumber -ge 22000) { 
        $IsWin11 = $true 
    } else {
        if ($OSCaption -like "*Windows 11*") { $IsWin11 = $true }
    }
} catch {
    if ([Environment]::OSVersion.Version.Build -ge 22000) { $IsWin11 = $true }
}

# 4. Modo Desfazer (Undo via CLI)
if ($Undo) {
    Draw-MainHeader -SubTitle "MODO DE RESTAURAÇÃO DO SISTEMA (UNDO)"
    Invoke-SystemUndoTweaks -IsWin11 $IsWin11
    Stop-Transcript | Out-Null
    exit 0
}

# 5. Carregar Catálogo de Apps e Tweaks
$AppsJsonPath = "$ScriptDir\Config\Apps.json"
$TweaksJsonPath = "$ScriptDir\Config\Tweaks.json"

$AllApps = @()
if (Test-Path $AppsJsonPath) {
    $AppsConfig = Get-Content -LiteralPath $AppsJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $AllApps = $AppsConfig.Apps
}

$AllTweaks = @()
if (Test-Path $TweaksJsonPath) {
    $TweaksConfig = Get-Content -LiteralPath $TweaksJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $AllTweaks = $TweaksConfig.Tweaks
}

# 6. DECISÃO DE MODO: GUI (PADRÃO) vs CLI vs SILENT
if (-not $CLI -and [string]::IsNullOrWhiteSpace($Preset) -and -not $Silent) {
    try {
        # Inicialização da Janela Gráfica Principal (GUI Moderna)
        Show-MainOptimizerWindow -AllApps $AllApps -AllTweaks $AllTweaks -CurrentOS $OSCaption -ScriptDir $ScriptDir
        Stop-Transcript | Out-Null
        exit 0
    } catch {
        Write-Host "`n [AVISO] A interface gráfica não pôde ser iniciada ($($_.Exception.Message))." -ForegroundColor Yellow
        Write-Host "         Iniciando automaticamente no modo de console (TUI)...`n" -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}

# 7. MODO CONSOLE (TUI / CLI)
$SelectedPresetName = "Padrao"
$SelectedAppsIds = @()
$AgressividadeExtrema = $false

if ($CLI -or ([string]::IsNullOrWhiteSpace($Preset) -and -not $Silent)) {
    $MenuLoop = $true
    while ($MenuLoop) {
        Draw-MainHeader
        Write-Host "$($script:C_GN)>> PASSO 1: SELECIONE O PERFIL DE OTIMIZAÇÃO$($script:C_RST)`n"
        Write-Host "$($script:C_W) [1] PADRÃO (Recomendado / Equilibrado)$($script:C_RST)"
        Write-Host "     - Limpeza de bloatware, desativação de telemetria e otimizações de CPU/RAM.`n"
        Write-Host "$($script:C_C) [2] GAMER (Otimizado p/ Jogos & FPS)$($script:C_RST)"
        Write-Host "     - Preserva Game Pass e Xbox App, desativa GameDVR de fundo e ativa Ultimate Performance.`n"
        Write-Host "$($script:C_P) [3] CORPORATIVO (Escritório & Produtividade)$($script:C_RST)"
        Write-Host "     - Foco em trabalho: Preserva Teams e Office, remove jogos e desativa telemetria/IA.`n"
        Write-Host "$($script:C_R) [4] EXTREMO (Desempenho Máximo p/ Hardware Antigo)$($script:C_RST)"
        Write-Host "     - Desativa SysMain/WSearch, remove Microsoft Edge e bloqueia todos os extras.`n"
        Write-Host "$($script:C_Y) [5] RESTAURAR / DESFAZER OTIMIZAÇÕES (Undo)$($script:C_RST)"
        Write-Host "     - Reativa serviços padrão, restaura menu do Win11 e protocolos do Xbox.`n"

        $Escolha = Read-Host "$($script:C_C) [?] Digite sua opção (1-5)$($script:C_RST)"
        
        switch ($Escolha) {
            '1' { $SelectedPresetName = "Padrao"; $MenuLoop = $false }
            '2' { $SelectedPresetName = "Gamer"; $MenuLoop = $false }
            '3' { $SelectedPresetName = "Corporativo"; $MenuLoop = $false }
            '4' { $SelectedPresetName = "Extremo"; $AgressividadeExtrema = $true; $MenuLoop = $false }
            '5' { 
                Draw-MainHeader -SubTitle "RESTAURAÇÃO DO SISTEMA"
                Invoke-SystemUndoTweaks -IsWin11 $IsWin11
                Stop-Transcript | Out-Null
                Write-Host "`n$($script:C_GN)[ SUCESSO ] Configurações restauradas com êxito.$($script:C_RST)"
                Write-Host "$($script:C_Y)>> Pressione ENTER para sair...$($script:C_RST)"
                Read-Host
                exit 0
            }
            default {
                Write-Host "`n$($script:C_R) [!] Opção inválida, por favor selecione um número de 1 a 5.$($script:C_RST)"
                Start-Sleep -Seconds 2
            }
        }
    }

    # Carregar Preset Escolhido
    $PresetFile = "$ScriptDir\Config\Presets\$SelectedPresetName.json"
    if (Test-Path $PresetFile) {
        $PresetData = Get-Content -LiteralPath $PresetFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $SelectedAppsIds = @($PresetData.RemoveApps)
    }

    # PASSO 2: Pergunta de Personalização Granular (Abrir Janela WPF de Seleção)
    Draw-MainHeader
    Write-Host "$($script:C_C) [ INFO ] Perfil selecionado: $($SelectedPresetName.ToUpper())$($script:C_RST)`n"
    Write-Host "$($script:C_GN)>> PASSO 2: PERSONALIZAÇÃO DE APLICATIVOS (DEBLOAT)$($script:C_RST)`n"
    Write-Host "$($script:C_W) Deseja abrir a janela gráfica para marcar/desmarcar aplicativos individualmente?"
    Write-Host " (Ex: escolher manter ou remover Xbox Game Bar, Teams, Spotify, etc.)$($script:C_RST)`n"
    Write-Host "  [S] Sim, abrir seletor gráfico de aplicativos (WPF)"
    Write-Host "  [N] Não, aplicar a lista padrão recomendada do perfil`n"
    
    $CustomOpt = Read-Host "$($script:C_C) [?] Deseja personalizar? (S/N)$($script:C_RST)"
    if ($CustomOpt -match '^[SsYy]$') {
        Write-Host "`n$($script:C_G) [..] Abrindo interface gráfica de seleção...$($script:C_RST)"
        $CustomSelection = Show-AppSelectionDialog -AllApps $AllApps -InitiallySelectedIds $SelectedAppsIds
        if ($null -ne $CustomSelection) {
            $SelectedAppsIds = $CustomSelection
        }
    }
} else {
    # Modo CLI / Não-Interativo
    if (-not [string]::IsNullOrWhiteSpace($Preset)) {
        $SelectedPresetName = $Preset
        $PresetFile = "$ScriptDir\Config\Presets\$SelectedPresetName.json"
        if (Test-Path $PresetFile) {
            $PresetData = Get-Content -LiteralPath $PresetFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $SelectedAppsIds = @($PresetData.RemoveApps)
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($CustomApps)) {
        $SelectedAppsIds = $CustomApps -split ',' | ForEach-Object { $_.Trim() }
    }
}

# 8. EXECUÇÃO DAS OTIMIZAÇÕES (FLUXO CONSOLE/CLI)
Draw-MainHeader -SubTitle "EXECUTANDO OTIMIZAÇÃO: $($SelectedPresetName.ToUpper())"

# A. Criar Ponto de Restauração
if (-not $SkipRestorePoint) {
    Executar-Etapa "Criar Ponto de Restauração do Sistema" {
        Invoke-SystemRestorePoint -Description "Pre_Debloat_$SelectedPresetName"
    }
}

# B. Remoção de Aplicativos Selecionados
$AppsToRemoveObjects = $AllApps | Where-Object { $SelectedAppsIds -contains $_.Id }
if ($AppsToRemoveObjects.Count -gt 0) {
    Write-Host "`n$($script:C_Y) >> FASE 1: REMOÇÃO DE BLOATWARE E APLICATIVOS ($($AppsToRemoveObjects.Count) selecionados)$($script:C_RST)"
    Executar-Etapa "Desinstalação de Pacotes Appx e WinGet" {
        Remove-AppListWithProgress -AppsToRemove $AppsToRemoveObjects
    }
}

# Remoção de OneDrive
Executar-Etapa "Remoção e Limpeza do Microsoft OneDrive" {
    Remove-OneDriveDeep
}

# C. Privacidade, Telemetria e IA
Write-Host "`n$($script:C_Y) >> FASE 2: PRIVACIDADE, TELEMETRIA E BLOQUEIO DE IA$($script:C_RST)"
Executar-Etapa "Desativar Telemetria (DiagTrack & dmwappushservice)" {
    Disable-SystemTelemetry
}
Executar-Etapa "Bloquear Windows AI (Recall, Click-to-Do, Copilot e WSAIFabricSvc)" {
    Disable-WindowsAIAndRecall -IsWin11 $IsWin11
}
Executar-Etapa "Desativar Sugestões e Anúncios do Windows" {
    Disable-WindowsSuggestionsAndAds
}
Executar-Etapa "Desativar Background e Telemetria do Edge" {
    Disable-EdgeBackgroundAndTelemetry
}

# D. Jogos e Protocolos Xbox
Write-Host "`n$($script:C_Y) >> FASE 3: OTIMIZAÇÕES DE JOGOS & TRATAMENTO DE PROTOCOLOS$($script:C_RST)"
Executar-Etapa "Desativar Gravação em Segundo Plano (GameDVR)" {
    Disable-GameDVRBackgroundCapture
}

# Se o usuário escolheu remover o XboxGamingOverlay, aplica o fix de protocolos anti-popups
if ($SelectedAppsIds -contains "XboxGamingOverlay") {
    Executar-Etapa "Neutralizar Protocolos ms-gamingoverlay (Anti-Popup Fix)" {
        Apply-GameBarProtocolNullRoute
    }
}

# E. Performance, Interface e Sistema
Write-Host "`n$($script:C_Y) >> FASE 4: PERFORMANCE DE SISTEMA & EXPLORER$($script:C_RST)"
Executar-Etapa "Otimizar Explorer (Extensões de Arquivo & Context Menu)" {
    Set-ExplorerAndUITweaks -IsWin11 $IsWin11
}
Executar-Etapa "Aplicar Esquema Ultimate Performance e Efeitos Visuais" {
    Set-PowerAndPerformanceTweaks
}
Executar-Etapa "Desativar Pesquisa Web Dinâmica no Menu Iniciar" {
    Disable-SearchHighlightsAndWeb
}

# F. Agressividade Extrema (Se selecionada)
if ($AgressividadeExtrema -or $SelectedPresetName -eq "Extremo") {
    Write-Host "`n$($script:C_R) >> FASE EXTRA: OTIMIZAÇÕES EXTREMAS DE BAIXO NÍVEL$($script:C_RST)"
    Executar-Etapa "Desativar SysMain (Superfetch) e Windows Search" {
        Disable-LegacyHeavyServices
    }
    Executar-Etapa "Remoção Profunda do Microsoft Edge" {
        Invoke-EdgeForceRemoveClean
    }
}

# G. Limpeza e Reinicialização do Explorer
Write-Host "`n$($script:C_Y) >> FASE FINAL: LIMPEZA DE DISCO E FINALIZAÇÃO$($script:C_RST)"
Executar-Etapa "Limpar Arquivos Temporários (%TEMP%)" {
    Clear-SystemTempFiles
}
Executar-Etapa "Reiniciando Interface Gráfica (Explorer)" {
    Restart-WindowsExplorer
}

Stop-Transcript | Out-Null

Write-Host "`n$($script:C_GN)[ SUCESSO ] Otimização concluída com alta integridade operacional!$($script:C_RST)"
Write-Host "$($script:C_C)[ LOG     ] Relatório detalhado salvo em: $LogPath$($script:C_RST)"

if (-not $Silent) {
    Write-Host "`n$($script:C_Y) >> Pressione ENTER para finalizar...$($script:C_RST)"
    Read-Host
}
