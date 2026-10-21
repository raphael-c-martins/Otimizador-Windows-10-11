<#
.SYNOPSIS
    Orquestrador Principal do Otimizador de Windows.
.DESCRIPTION
    Abre a janela gráfica (GUI WPF) por padrão. Também oferece modo terminal interativo (-CLI),
    automação por perfil (-Preset/-Silent), simulação sem alterações (-DryRun) e reversão (-Undo).

    Códigos de saída: 0 = sucesso | 1 = erro de uso/permissão | 2 = concluído com etapas com erro |
                      3 = abortado porque o Ponto de Restauração não pôde ser criado.
.EXAMPLE
    .\Run.ps1 -Preset Gamer -Silent
.EXAMPLE
    .\Run.ps1 -Preset Padrao -DryRun
.EXAMPLE
    .\Run.ps1 -Preset C:\Perfis\MeuPerfil.json -CLI
#>

[CmdletBinding()]
param(
    [switch]$CLI,
    [string]$Preset = '',
    [switch]$Silent,
    [switch]$SkipRestorePoint,
    [switch]$ContinueWithoutRestorePoint,
    [switch]$Undo,
    [string]$CustomApps = '',
    [switch]$DryRun,
    [switch]$LaunchedHidden
)

# Forçar codificação UTF-8 no Console e no Pipeline do PowerShell
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    [Console]::InputEncoding  = [System.Text.Encoding]::UTF8
    $OutputEncoding           = [System.Text.Encoding]::UTF8
} catch {}

$ErrorActionPreference = 'Continue'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# 1. Carregar módulos (Core primeiro: a UI usa suas funções)
foreach ($ModuleFolder in @('src\Core', 'src\UI')) {
    foreach ($ModuleFile in (Get-ChildItem -LiteralPath (Join-Path $ScriptDir $ModuleFolder) -Filter '*.ps1' | Sort-Object Name)) {
        . $ModuleFile.FullName
    }
}

$Interactive = -not $Silent
$UseGui = (-not $CLI -and -not $Silent -and -not $Undo -and [string]::IsNullOrWhiteSpace($Preset))

# 2. Validação dos parâmetros
if ($Silent -and -not $Undo -and [string]::IsNullOrWhiteSpace($Preset)) {
    Write-Host '[ERRO] -Silent exige -Preset <nome ou arquivo .json> (ou -Undo). Nada foi alterado.' -ForegroundColor Red
    exit $script:ExitCodes.ErroDeUso
}

# 3. Privilégio de Administrador (a simulação não altera o sistema e dispensa elevação)
if (-not $DryRun -and -not (Test-IsAdministrator)) {
    if ($Silent) {
        Write-Host '[ERRO] Execute em um PowerShell aberto como Administrador. Nada foi alterado.' -ForegroundColor Red
        exit $script:ExitCodes.ErroDeUso
    }

    Write-Host ' [!] Solicitando permissão de Administrador...' -ForegroundColor Cyan
    $ArgList = [System.Collections.Generic.List[string]]::new()
    $ArgList.AddRange([string[]]@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`""))
    foreach ($Param in $PSBoundParameters.GetEnumerator()) {
        if ($Param.Value -is [System.Management.Automation.SwitchParameter]) {
            if ($Param.Value.IsPresent) { $ArgList.Add("-$($Param.Key)") }
        } else {
            $ArgList.Add("-$($Param.Key)")
            $ArgList.Add("`"$($Param.Value)`"")
        }
    }
    try {
        Start-Process -FilePath 'powershell.exe' -ArgumentList ($ArgList -join ' ') -Verb RunAs -ErrorAction Stop
        exit $script:ExitCodes.Sucesso
    } catch {
        Write-Host '[ERRO] A permissão de Administrador foi negada. Nada foi alterado.' -ForegroundColor Red
        exit $script:ExitCodes.ErroDeUso
    }
}

# 4. Pastas de dados (logs e diários de alteração) e log da sessão
$DataRoot = if ($DryRun) { $script:OptimizerDryRunRoot } else { $script:OptimizerDataRoot }
try {
    if ($DryRun) {
        if (-not (Test-Path -LiteralPath $DataRoot)) { New-Item -ItemType Directory -Path $DataRoot -Force | Out-Null }
    } else {
        Initialize-SecureDataDirectory -Path $DataRoot
    }
} catch {
    Write-Host "[ERRO] Não foi possível preparar a pasta de dados '$DataRoot': $($_.Exception.Message)" -ForegroundColor Red
    exit $script:ExitCodes.ErroDeUso
}

$LogDir = Join-Path $DataRoot 'logs'
$JournalDir = Join-Path $DataRoot 'estado'
$LogPath = New-OptimizerLogFile -Directory $LogDir

$ConsoleSink = { param($Level, $Message) Write-ConsoleLogEntry -Level $Level -Message $Message }
$ConsoleProgress = { param($Current, $Total, $Activity) Write-ConsoleProgress -Current $Current -Total $Total -Activity $Activity }
if ($UseGui) {
    Set-OptimizerLogTarget -FilePath $LogPath
} else {
    Set-OptimizerLogTarget -FilePath $LogPath -Sink $ConsoleSink -ProgressSink $ConsoleProgress
}

try {
    # 5. Ambiente
    $OS = Get-OSInfo
    $IsPortable = Test-IsPortableDevice
    $UserTarget = if ($DryRun) {
        [pscustomobject]@{ Sid = $null; Message = 'Simulação: nenhum perfil de usuário será alterado.'; IsWarning = $false }
    } else {
        Resolve-UserRegistrySid
    }

    Write-OptimizerLog -Level INFO -Message "Otimizador de Windows v$script:OptimizerVersion | $($OS.Caption) (build $($OS.Build)) | $(if ($IsPortable) { 'Notebook' } else { 'Desktop' })$(if ($DryRun) { ' | MODO SIMULAÇÃO' })"
    Write-OptimizerLog -Level $(if ($UserTarget.IsWarning) { 'AVISO' } else { 'INFO' }) -Message $UserTarget.Message

    $Context = @{
        IsWin11          = $OS.IsWin11
        IsPortable       = $IsPortable
        ProtectedPaths   = @($ScriptDir, $DataRoot)
        StateDirectory   = $DataRoot
        JournalDirectory = $JournalDir
        UserSid          = $UserTarget.Sid
    }

    $Catalog = Get-OptimizerCatalog -ScriptDir $ScriptDir
} catch {
    Write-OptimizerLog -Level ERRO -Message "Falha ao inicializar: $($_.Exception.Message)"
    exit $script:ExitCodes.ErroDeUso
}

# 6. Modo Desfazer (Undo)
function Invoke-UndoFlow {
    Show-MainHeader -SubTitle 'RESTAURAÇÃO DO SISTEMA (UNDO)'
    Set-UserRegistryTarget -Sid $Context.UserSid
    $UndoResult = Invoke-SystemUndo -JournalDirectory $JournalDir -IsWin11 $OS.IsWin11 -DryRun:$DryRun
    $Color = if ($UndoResult.Failed -gt 0) { 'Yellow' } else { 'Green' }
    Write-Host "`n Restauração: $($UndoResult.Restored) itens revertidos, $($UndoResult.Failed) com erro. Log: $LogPath" -ForegroundColor $Color
    if ($Interactive) { Read-Host "`n Pressione ENTER para sair" | Out-Null }
    if ($UndoResult.Failed -gt 0) { exit $script:ExitCodes.FalhasParciais }
    exit $script:ExitCodes.Sucesso
}

if ($Undo) { Invoke-UndoFlow }

# 7. Janela gráfica (padrão)
if ($UseGui) {
    try {
        Show-MainOptimizerWindow -Catalog $Catalog -Context $Context -ScriptDir $ScriptDir -LogFilePath $LogPath -OSCaption $OS.Caption -DryRun:$DryRun
        exit $script:ExitCodes.Sucesso
    } catch {
        Write-OptimizerLog -Level ERRO -Message "A interface gráfica não pôde ser iniciada: $($_.Exception.Message)"
        if ($LaunchedHidden) {
            # Esta janela está oculta (aberta pelo Otimizador.cmd): reabre o modo terminal em um console visível
            $FallbackArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -CLI$(if ($DryRun) { ' -DryRun' })"
            Start-Process -FilePath 'powershell.exe' -ArgumentList $FallbackArgs
            exit $script:ExitCodes.ErroDeUso
        }
        Write-Host "`n [AVISO] Iniciando no modo terminal...`n" -ForegroundColor Yellow
        Set-OptimizerLogTarget -FilePath $LogPath -Sink $ConsoleSink -ProgressSink $ConsoleProgress
        Start-Sleep -Seconds 2
    }
}

# 8. Modo terminal (interativo ou automatizado)
try {
    if ([string]::IsNullOrWhiteSpace($Preset)) {
        $PresetMenu = [ordered]@{ '1' = 'Padrao'; '2' = 'Gamer'; '3' = 'Corporativo'; '4' = 'Extremo' }
        $SelectedName = $null
        while (-not $SelectedName) {
            Show-MainHeader -SubTitle "PROTOCOLO DE OTIMIZAÇÃO & DEBLOAT (v$script:OptimizerVersion)$(if ($DryRun) { ' [SIMULAÇÃO]' })"
            Write-Host "$($script:C_GN)>> PASSO 1: SELECIONE O PERFIL DE OTIMIZAÇÃO$($script:C_RST)`n"
            Write-Host "$($script:C_W) [1] PADRÃO (Recomendado / Equilibrado)$($script:C_RST)"
            Write-Host "     - Limpeza de bloatware, desativação de telemetria e ajustes de interface e energia.`n"
            Write-Host "$($script:C_C) [2] GAMER (Otimizado p/ Jogos & FPS)$($script:C_RST)"
            Write-Host "     - Preserva Game Pass, Xbox App e players de mídia, desativa GameDVR e ativa Ultimate Performance.`n"
            Write-Host "$($script:C_P) [3] CORPORATIVO (Escritório & Produtividade)$($script:C_RST)"
            Write-Host "     - Preserva Teams, Outlook e OneDrive, remove jogos e desativa telemetria/IA.`n"
            Write-Host "$($script:C_R) [4] EXTREMO (Desempenho Máximo p/ Hardware Antigo)$($script:C_RST)"
            Write-Host "     - Desativa SysMain/WSearch, remove OneDrive e Microsoft Edge.`n"
            Write-Host "$($script:C_Y) [5] RESTAURAR / DESFAZER OTIMIZAÇÕES (Undo)$($script:C_RST)"
            Write-Host "     - Devolve registro, serviços, tarefas e energia ao estado anterior às otimizações.`n"

            $Choice = Read-Host "$($script:C_C) [?] Digite sua opção (1-5)$($script:C_RST)"
            if ($Choice -eq '5') { Invoke-UndoFlow }
            if ($PresetMenu.Contains($Choice)) {
                $SelectedName = $PresetMenu[$Choice]
            } else {
                Write-Host "`n$($script:C_R) [!] Opção inválida, por favor selecione um número de 1 a 5.$($script:C_RST)"
                Start-Sleep -Seconds 2
            }
        }
        $SelectedPreset = Import-OptimizerPreset -Catalog $Catalog -NameOrPath $SelectedName
        $AppIds = @($SelectedPreset.RemoveApps)

        # Passo 2: personalização granular dos aplicativos (janela WPF de seleção)
        Show-MainHeader
        Write-Host "$($script:C_C) [ INFO ] Perfil selecionado: $($SelectedPreset.DisplayName)$($script:C_RST)`n"
        Write-Host "$($script:C_GN)>> PASSO 2: PERSONALIZAÇÃO DE APLICATIVOS (DEBLOAT)$($script:C_RST)`n"
        if (Read-YesNo -Prompt 'Deseja abrir a janela para marcar/desmarcar aplicativos individualmente?') {
            $CustomSelection = Show-AppSelectionDialog -AllApps $Catalog.Apps -InitiallySelectedIds $AppIds
            if ($null -ne $CustomSelection) { $AppIds = @($CustomSelection) }
        }
    } else {
        $SelectedPreset = Import-OptimizerPreset -Catalog $Catalog -NameOrPath $Preset
        $AppIds = @($SelectedPreset.RemoveApps)
    }

    if (-not [string]::IsNullOrWhiteSpace($CustomApps)) {
        $AppIds = @($CustomApps -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
    $TweakIds = @($SelectedPreset.ApplyTweaks)
    Assert-OptimizerSelection -Catalog $Catalog -AppIds $AppIds -TweakIds $TweakIds
} catch {
    Write-OptimizerLog -Level ERRO -Message $_.Exception.Message
    exit $script:ExitCodes.ErroDeUso
}

$PresetLabel = if ([string]::IsNullOrWhiteSpace($SelectedPreset.PresetName)) { 'Personalizado' } else { $SelectedPreset.PresetName }

# 9. Confirmação (modo interativo)
if ($Interactive) {
    Show-MainHeader -SubTitle "CONFIRMAÇÃO: $($SelectedPreset.DisplayName)"
    Write-Host "$($script:C_W) Aplicativos a remover ($($AppIds.Count)):$($script:C_RST)"
    foreach ($App in $Catalog.Apps | Where-Object { $AppIds -contains $_.Id }) { Write-Host "$($script:C_G)   - $($App.FriendlyName)$($script:C_RST)" }
    Write-Host "`n$($script:C_W) Ajustes a aplicar ($($TweakIds.Count)):$($script:C_RST)"
    foreach ($Tweak in $Catalog.Tweaks | Where-Object { $TweakIds -contains $_.Id }) { Write-Host "$($script:C_G)   - $($Tweak.Name)$($script:C_RST)" }
    Write-Host ''
    if (-not (Read-YesNo -Prompt "Confirmar a execução$(if ($DryRun) { ' da SIMULAÇÃO' })?")) {
        Write-OptimizerLog -Level INFO -Message 'Execução cancelada pelo usuário. Nada foi alterado.'
        exit $script:ExitCodes.Sucesso
    }
    Show-MainHeader -SubTitle "EXECUTANDO: $($SelectedPreset.DisplayName)$(if ($DryRun) { ' [SIMULAÇÃO]' })"
}

# 10. Ponto de Restauração
if ($DryRun) {
    Write-OptimizerLog -Level INFO -Message '[SIMULAÇÃO] Ponto de Restauração não criado.'
} elseif ($SkipRestorePoint) {
    Write-OptimizerLog -Level AVISO -Message 'Ponto de Restauração ignorado a pedido (-SkipRestorePoint).'
} else {
    try {
        Invoke-SystemRestorePoint -Description (New-RestorePointDescription -Label $PresetLabel) | Out-Null
    } catch {
        Write-OptimizerLog -Level ERRO -Message "Ponto de Restauração não criado: $($_.Exception.Message)"
        $ContinueAnyway = [bool]$ContinueWithoutRestorePoint
        if (-not $ContinueAnyway -and $Interactive) {
            $ContinueAnyway = Read-YesNo -Prompt 'Continuar SEM ponto de restauração?'
        }
        if (-not $ContinueAnyway) {
            Write-OptimizerLog -Level AVISO -Message 'Otimização abortada para manter a segurança do sistema. Nada foi alterado.'
            exit $script:ExitCodes.AbortadoSemBackup
        }
        Write-OptimizerLog -Level AVISO -Message 'Continuando sem Ponto de Restauração por decisão do usuário.'
    }
}

# 11. Execução pelo motor único
try {
    $Result = Invoke-OptimizationPlan -Catalog $Catalog -AppIds $AppIds -TweakIds $TweakIds -Context $Context -DryRun:$DryRun
} catch {
    Write-OptimizerLog -Level ERRO -Message "Falha inesperada na execução: $($_.Exception.Message)"
    exit $script:ExitCodes.ErroDeUso
}

Show-ExecutionSummary -Result $Result -LogPath $LogPath
if ($Interactive) { Read-Host "`n Pressione ENTER para finalizar" | Out-Null }

if ($Result.Failed -gt 0) { exit $script:ExitCodes.FalhasParciais }
exit $script:ExitCodes.Sucesso
