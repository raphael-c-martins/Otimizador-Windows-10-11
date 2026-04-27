@echo off
setlocal EnableDelayedExpansion
:: Pushd garante suporte a caminhos de Rede (UNC \\)
pushd "%~dp0"
TITLE // OTIMIZADOR DE WINDOWS (Engine v12) //

:: Export path safely to native variables avoiding ampersands issues
set "batchPath=%~f0"

:: ==========================================================
:: 1. VERIFICACAO DE PRIVILEGIOS DE ADMINISTRADOR
:: ==========================================================
NET FILE 1>NUL 2>NUL
if '%errorlevel%' NEQ '0' (
    echo.
    echo  [!] Solicitando Nivel de Acesso Administrativo...
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "cmd.exe", "/c """"!batchPath!""""" , "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    exit /B
)
if exist "%temp%\getadmin.vbs" ( del "%temp%\getadmin.vbs" )

:: ==========================================================
:: 2. ENGINE POLIGLOTA (EXTRAI POWERSHELL) E CONTROLE CONSOLE
:: ==========================================================
reg add HKEY_CURRENT_USER\Console /v VirtualTerminalLevel /t REG_DWORD /d 1 /f >nul 2>&1

echo.
echo  [..] Bootstrapping Engine de Alta Performance...
set "PS_OUT=%temp%\Ultimate_Engine_!random!.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$start=$false; Get-Content -LiteralPath '!batchPath!' -Encoding UTF8 | ForEach-Object { if($start) { $_ } elseif($_ -eq '::: START_POWERSHELL_MARKER :::') { $start=$true } } | Set-Content -LiteralPath '!PS_OUT!' -Encoding UTF8; & '!PS_OUT!'"

if !errorlevel! NEQ 0 (
    echo.
    echo  [ERRO CRITICO] O executor fechou inesperadamente.
    pause
)

del "!PS_OUT!" >nul 2>&1
exit /b

:: ==========================================================
:: !!! NAO APAGUE A LINHA ABAIXO - MARCADOR (PS CORE) !!!
::: START_POWERSHELL_MARKER :::
:: ==========================================================

$ErrorActionPreference = "Continue" 
$NomePC = $env:COMPUTERNAME
$DataArquivo = Get-Date -Format "yyyy-MM-dd_HH-mm"
$LogDir = "$PSScriptRoot\logs"
if (!(Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }
$LogPath = "$LogDir\Relatorio_Otimizacao_${NomePC}_${DataArquivo}.txt"

Start-Transcript -Path $LogPath -Append -Force | Out-Null

# Define Variaveis Globais (High-Density UI Configs)
$Esc = [char]27
$C_B  = "$Esc[38;2;52;152;219m"  # Azul (Principal)
$C_C  = "$Esc[38;2;26;188;156m"  # Turquesa (Destaques)
$C_G  = "$Esc[38;2;149;165;166m" # Cinza (Metadados)
$C_W  = "$Esc[38;2;236;240;241m" # Branco (Texto)
$C_GN = "$Esc[38;2;46;204;113m"  # Verde (Sucesso)
$C_R  = "$Esc[38;2;231;76;60m"   # Vermelho (Erro)
$C_Y  = "$Esc[38;2;241;196;15m"  # Amarelo (Aviso)
$C_RST = "$Esc[0m"

function Draw-Header {
    Clear-Host
    Write-Host ""
    Write-Host "$C_B   ____  ____  _____ ___ __  __ ___ _____ _____ ____  $C_RST"
    Write-Host "$C_B  / __ \|  _ \|_   _|_ _|  \/  |_ _|__  /| ____|  _ \ $C_RST"
    Write-Host "$C_B | |  | | |_) | | |  | || |\/| || |  / / |  _| | |_) |$C_RST"
    Write-Host "$C_B | |__| |  __/  | |  | || |  | || | / /_ | |___|  _ < $C_RST"
    Write-Host "$C_B  \____/|_|     |_| |___|_|  |_|___/____||_____|_| \_\$C_RST"
    Write-Host "$C_G  --------------------------------------------------------$C_RST"
    Write-Host "$C_W            PROTOCOLO DE OTIMIZAÇÃO DE SISTEMA            $C_RST"
    Write-Host "$C_G  --------------------------------------------------------$C_RST"
    Write-Host "$C_G  [STATUS] Data: $(Get-Date -Format 'dd/MM/yyyy HH:mm')"
    Write-Host "  [STATUS] SysAdmin: $env:USERNAME | Target: $NomePC"
    Write-Host "  [STATUS] Host OS: $((Get-CimInstance Win32_OperatingSystem).Caption)"
    Write-Host "$C_G  --------------------------------------------------------$C_RST`n"
}

function Executar-Item {
    param([string]$Nome, [scriptblock]$Acao)
    $TStart = Get-Date -Format "HH:mm:ss"
    Write-Host "$C_G [..] [$TStart] $Nome...$C_RST" -NoNewline
    try {
        & $Acao
        $TEnd = Get-Date -Format "HH:mm:ss"
        Write-Host "`r$C_GN [ OK ] [$TEnd] $Nome                                        $C_RST"
    } catch {
        $TEnd = Get-Date -Format "HH:mm:ss"
        Write-Host "`r$C_R [ERRO] [$TEnd] $Nome                                        $C_RST"
        Write-Host "$C_R        ERRO: $_.Exception.Message$C_RST"
    }
}

# --- MODULOS DE SISTEMA ---
function Mod-Limpeza-Bloatware {
    param([bool]$Win11)
    Write-Host "`n$C_Y >> FASE 1: REMOÇÃO DE APLICAÇÕES REDUNDANTES E BLOATWARE$C_RST"

    # Apps que sempre sao bloqueados/removidos
    $Apps = @(
        "*Microsoft.3DBuilder*", "*Microsoft.549981C3F5F10*", "*BingFinance*", "*BingFoodAndDrink*",
        "*BingHealthAndFitness*", "*BingNews*", "*BingSports*", "*BingTranslator*",
        "*BingTravel*", "*BingWeather*", "*Getstarted*", "*Messaging*", "*Microsoft3DViewer*", 
        "*MicrosoftOfficeHub*", "*MicrosoftPowerBIForWindows*", "*MicrosoftSolitaireCollection*", 
        "*MicrosoftStickyNotes*", "*MixedReality.Portal*", "*NetworkSpeedTest*", "*News*", "*Office.Sway*", 
        "*OneConnect*", "*Print3D*", "*SkypeApp*", "*Todos*", "*WindowsAlarms*", "*WindowsFeedbackHub*", 
        "*WindowsMaps*", "*WindowsSoundRecorder*", "*XboxApp*", "*ZuneVideo*", "*ZuneMusic*",
        "*MicrosoftFamily*", "*QuickAssist*", "*MicrosoftTeams*", "*MSTeams*", "*People*", "*YourPhone*",
        "*XboxGamingOverlay*", "*Clipchamp*", "*Disney*", "*Facebook*", "*Fitbit*", "*Instagram*", "*Netflix*", 
        "*Spotify*", "*TikTok*", "*Twitter*", "*Viber*", "*WinZipUniversal*", "*HPSupportAssistant*", 
        "*HPJumpStarts*", "*HPPowerManager*", "*OutlookForWindows*", "*Copilot*", "*Windows.DevHome*"
    )

    Executar-Item "Iniciando processo de desinstalação (Lote de $($Apps.Count))" {
        $Count = 0
        $Total = $Apps.Count
        foreach ($App in $Apps) {
            $Count++
            # Visual High Density Progress
            Write-Progress -Activity "Removendo Bloatware" -Status "Analisando: $App" -PercentComplete (($Count / $Total) * 100)
            try {
                Get-AppxPackage -Name $App -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction Stop *> $null
            } catch {}
            
            try {
                Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object {$_.DisplayName -like $App} | Remove-AppxProvisionedPackage -Online -ErrorAction Stop *> $null
            } catch {}
        }
    }
    
    Executar-Item "Remoção completa do OneDrive" {
        Stop-Process -Name "OneDrive" -Force -ErrorAction SilentlyContinue
        $Setup = "$env:systemroot\System32\OneDriveSetup.exe"
        if (!(Test-Path $Setup)) { $Setup = "$env:systemroot\SysWOW64\OneDriveSetup.exe" }
        if (Test-Path $Setup) { Start-Process $Setup -ArgumentList "/uninstall" -Wait }
    }
}

function Mod-Privacidade {
    param([bool]$Win11)
    Write-Host "`n$C_Y >> FASE 2: OTIMIZAÇÃO DE PRIVACIDADE E SERVIÇOS DE FUNDO$C_RST"

    Executar-Item "Desativar Telemetria (DiagTrack)" {
        Stop-Service DiagTrack -Force -ErrorAction SilentlyContinue | Out-Null
        Set-Service DiagTrack -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null
    }

    if ($Win11) {
        Executar-Item "Bloquear Windows AI (Recall, ClickToDo, SearchAI)" {
            $AI = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI"
            if(!(Test-Path $AI)){ New-Item -Path $AI -Force | Out-Null }
            Set-ItemProperty -Path $AI -Name "DisableAIDataAnalysis" -Value 1
            Set-ItemProperty -Path $AI -Name "TurnOffWindowsRecall" -Value 1

            $Copilot = "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot"
            if(!(Test-Path $Copilot)){ New-Item -Path $Copilot -Force | Out-Null }
            Set-ItemProperty -Path $Copilot -Name "TurnOffWindowsCopilot" -Value 1
            
            $Exp = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
            Set-ItemProperty -Path $Exp -Name "ShowCopilotButton" -Value 0 -ErrorAction SilentlyContinue
        }
    } else {
        Executar-Item "Bloquear Copilot Legacy (Win10 Mode)" {
            $Copilot = "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot"
            if(!(Test-Path $Copilot)){ New-Item -Path $Copilot -Force | Out-Null }
            Set-ItemProperty -Path $Copilot -Name "TurnOffWindowsCopilot" -Value 1
            
            $Exp = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
            Set-ItemProperty -Path $Exp -Name "ShowCopilotButton" -Value 0 -ErrorAction SilentlyContinue
        }
    }

    Executar-Item "Limpar Edge, Sidebar e Extensoes de Background" {
        $Edge = "HKLM:\SOFTWARE\Policies\Microsoft\Edge"
        if(!(Test-Path $Edge)){ New-Item -Path $Edge -Force | Out-Null }
        Set-ItemProperty -Path $Edge -Name "HubsSidebarEnabled" -Value 0
        Set-ItemProperty -Path $Edge -Name "SpotlightExperiencesAndRecommendationsEnabled" -Value 0
        Set-ItemProperty -Path $Edge -Name "StartupBoostEnabled" -Value 0
        Set-ItemProperty -Path $Edge -Name "BackgroundModeEnabled" -Value 0
    }

    Executar-Item "Desabilitar Telemetria Xbox/GameBar (Economia de CPU/I-O)" {
        $GameConfig = "HKCU:\System\GameConfigStore"
        if(!(Test-Path $GameConfig)){ New-Item -Path $GameConfig -Force | Out-Null }
        Set-ItemProperty -Path $GameConfig -Name "GameDVR_Enabled" -Value 0

        $GameBar = "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR"
        if(!(Test-Path $GameBar)){ New-Item -Path $GameBar -Force | Out-Null }
        Set-ItemProperty -Path $GameBar -Name "AppCaptureEnabled" -Value 0
    }
}

function Mod-Performance {
    param([bool]$Win11, [bool]$NivelExtremo)
    Write-Host "`n$C_Y >> FASE 3: TUNING DE PERFORMANCE DE BAIXO NIVEL (I/O & UX)$C_RST"

    Executar-Item "Otimizar UI: Mostrar Extensoes de Arquivo (Dev Mode)" {
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "HideFileExt" -Value 0
    }



    Executar-Item "Limpeza Grafica: Esconder TaskView, Chat e 3D Objects" {
        $Exp = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
        Set-ItemProperty -Path $Exp -Name "ShowTaskViewButton" -Value 0 -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $Exp -Name "TaskbarMn" -Value 0 -ErrorAction SilentlyContinue # Chat
    }

    Executar-Item "Estabilidade Classica: Desativar Fast Startup (Hibernacao)" {
        powercfg -h off | Out-Null
    }

    if ($Win11) {


        Executar-Item "Restaurar Menu de Contexto Clássico" {
            $Key = "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"
            if(!(Test-Path $Key)){ New-Item -Path $Key -Force | Out-Null }
            Set-ItemProperty -Path $Key -Name "(default)" -Value ""
        }
    }

    Executar-Item "Injetar Perfil de Energia Ultimate Performance" {
        powercfg -duplicatescheme 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c | Out-Null
        powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c | Out-Null
    }

    Executar-Item "Configurar Modulacao Grafica (Desativar Sombras Lentas e Transparencias)" {
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name "VisualFXSetting" -Value 3 
        $Theme = "HKCU:\Software\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
        if(!(Test-Path $Theme)){New-Item -Path $Theme -Force | Out-Null}
        Set-ItemProperty -Path $Theme -Name "EnableTransparency" -Value 0
        Set-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "MinAnimate" -Value "0"
        # Mantem fontes lisas 
        Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothing" -Value "2"
        Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothingType" -Value "2"
    }

    if ($NivelExtremo) {
        Write-Host "$C_B    >> CONFIGURAÇÃO AVANÇADA: Aplicando modificações de baixo nível...$C_RST"
        Executar-Item "Matar Coleta Dinamica (Search Highlights e Web Search)" {
            $Search = "HKCU:\Software\Microsoft\Windows\CurrentVersion\SearchSettings"
            if(!(Test-Path $Search)){ New-Item -Path $Search -Force | Out-Null }
            Set-ItemProperty -Path $Search -Name "IsDynamicSearchBoxEnabled" -Value 0 -ErrorAction SilentlyContinue
            
            $ExpSearch = "HKCU:\Software\Policies\Microsoft\Windows\Explorer"
            if(!(Test-Path $ExpSearch)){ New-Item -Path $ExpSearch -Force | Out-Null }
            Set-ItemProperty -Path $ExpSearch -Name "DisableSearchBoxSuggestions" -Value 1 -ErrorAction SilentlyContinue
        }

        Executar-Item "Desativar SysMain (Superfetch) e WSearch (Limit. Caches HDDs)" {
            Stop-Service WSearch -Force -ErrorAction SilentlyContinue | Out-Null
            Set-Service WSearch -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null
            Stop-Service SysMain -Force -ErrorAction SilentlyContinue | Out-Null
            Set-Service SysMain -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null
        }

        Executar-Item "Remoção profunda do Microsoft Edge" {
            $Key = "HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev"
            if(!(Test-Path $Key)){ New-Item -Path $Key -Force | Out-Null }
            Set-ItemProperty -Path $Key -Name "AllowUninstall" -Value 1

            $UninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"
            if (Test-Path $UninstallKey) {
                $UnStr = (Get-ItemProperty -Path $UninstallKey).UninstallString
                if ($UnStr) {
                    $Cmd = "$UnStr --force-uninstall"
                    Start-Process cmd.exe -ArgumentList "/c $Cmd" -WindowStyle Hidden -Wait | Out-Null
                }
            }
            Remove-Item "$env:PUBLIC\Desktop\Microsoft Edge.lnk" -Force -ErrorAction SilentlyContinue
            Remove-Item "$env:USERPROFILE\Desktop\Microsoft Edge.lnk" -Force -ErrorAction SilentlyContinue
        }
    }
}

function Finalizar-Sweep {
    Executar-Item "Limpar Filesystem Temporario (Pasta TEMP)" {
        Remove-Item "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "C:\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "$C_G [..] Reiniciando Interface Grafica (Explorer)...$C_RST"
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
}


# --- BOOTSTRAP: SELECAO ---
$SO_Windows11 = $false
$NivelExtremo = $false

# PASSO 1: Seleção de SO com Validação
$ValidSO = $false
while (-not $ValidSO) {
    Draw-Header
    Write-Host "$C_GN>> PASSO 1: CONFIRME SEU SISTEMA OPERACIONAL$C_RST`n"
    Write-Host "$C_W [1] Windows 10$C_RST"
    Write-Host "$C_W [2] Windows 11$C_RST`n"
    $Opt1 = Read-Host "$C_C [?] Digite 1 ou 2$C_RST"
    
    if ($Opt1 -eq '1') {
        $SO_Windows11 = $false
        $ValidSO = $true
    } elseif ($Opt1 -eq '2') {
        $SO_Windows11 = $true
        $ValidSO = $true
    } else {
        Write-Host "`n$C_R [!] Essa opção não existe, por favor se atente as opções disponíveis.$C_RST"
        Start-Sleep -Seconds 2
    }
}

# PASSO 2: Seleção de Grau com Validação
$ValidOpt = $false
while (-not $ValidOpt) {
    Draw-Header
    $LabelSO = if ($SO_Windows11) { "Windows 11" } else { "Windows 10" }
    Write-Host "$C_C [ INFO ] SO Selecionado: $LabelSO$C_RST`n"

    Write-Host "$C_GN>> PASSO 2: SELECIONE O GRAU DE OTIMIZAÇÃO$C_RST`n"
    Write-Host "$C_W [1] PADRÃO (Otimizado / Leve)$C_RST"
    Write-Host "     - Foco em Performance: Remove bloatware, desativa XboxBar, Fast Startup e processos inúteis.`n"
    Write-Host "$C_R [2] EXTREMO (Desempenho Máximo p/ Hardware Antigo)$C_RST"
    Write-Host "     - Tudo do Padrão + Remoção do Edge, bloqueio de Highlights, Indexação e Sysmain.`n"
    $Opt2 = Read-Host "$C_C [?] Digite 1 ou 2$C_RST"

    if ($Opt2 -eq '1') {
        $NivelExtremo = $false
        $ValidOpt = $true
    } elseif ($Opt2 -eq '2') {
        $NivelExtremo = $true
        $ValidOpt = $true
    } else {
        Write-Host "`n$C_R [!] Essa opção não existe, por favor se atente as opções disponíveis.$C_RST"
        Start-Sleep -Seconds 2
    }
}

# INICIAR
Draw-Header
Write-Host "$C_W>> INICIANDO OTIMIZAÇÃO DO SISTEMA...$C_RST"
Mod-Limpeza-Bloatware -Win11 $SO_Windows11
Mod-Privacidade -Win11 $SO_Windows11
Mod-Performance -Win11 $SO_Windows11 -NivelExtremo $NivelExtremo

Finalizar-Sweep
Stop-Transcript | Out-Null

Write-Host "`n$C_GN[ SUCESSO ] $C_W Servicos e Engine finalizados com alta integridade.$C_RST"
Write-Host "$C_C[ LOG     ] $C_G Relatório gravado em: $LogPath$C_RST"

Write-Host "`n$C_Y >> Pressione ENTER para sair...$C_RST"
Read-Host
Exit
