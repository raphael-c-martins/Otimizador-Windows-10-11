<#
.SYNOPSIS
    Módulo de Interface de Terminal ANSI de Alta Densidade (Console TUI).
.DESCRIPTION
    Fornece funções de renderização cromática, cabeçalhos, validação de entrada e
    rastreamento temporal dinâmico de auditoria com timestamps [HH:mm:ss].
#>

$Esc = [char]27
$script:C_B   = "$Esc[38;2;52;152;219m"  # Azul (Primário)
$script:C_C   = "$Esc[38;2;26;188;156m"  # Turquesa (Destaques)
$script:C_G   = "$Esc[38;2;149;165;166m" # Cinza (Metadados)
$script:C_W   = "$Esc[38;2;236;240;241m" # Branco (Texto)
$script:C_GN  = "$Esc[38;2;46;204;113m"  # Verde (Sucesso)
$script:C_R   = "$Esc[38;2;231;76;60m"   # Vermelho (Erro)
$script:C_Y   = "$Esc[38;2;241;196;15m"  # Amarelo (Aviso)
$script:C_P   = "$Esc[38;2;155;89;182m"  # Roxo (Especial)
$script:C_RST = "$Esc[0m"

function Draw-MainHeader {
    param(
        [string]$SubTitle = "PROTOCOLO DE OTIMIZAÇÃO & DEBLOAT (v13.0 MODULAR)"
    )
    Clear-Host
    Write-Host ""
    Write-Host "$($script:C_B)   ____  ____  _____ ___ __  __ ___ _____ _____ ____  $($script:C_RST)"
    Write-Host "$($script:C_B)  / __ \|  _ \|_   _|_ _|  \/  |_ _|__  /| ____|  _ \ $($script:C_RST)"
    Write-Host "$($script:C_B) | |  | | |_) | | |  | || |\/| || |  / / |  _| | |_) |$($script:C_RST)"
    Write-Host "$($script:C_B) | |__| |  __/  | |  | || |  | || | / /_ | |___|  _ < $($script:C_RST)"
    Write-Host "$($script:C_B)  \____/|_|     |_| |___|_|  |_|___/____||_____|_| \_\$($script:C_RST)"
    Write-Host "$($script:C_G)  --------------------------------------------------------$($script:C_RST)"
    Write-Host "$($script:C_W)            $SubTitle            $($script:C_RST)"
    Write-Host "$($script:C_G)  --------------------------------------------------------$($script:C_RST)"
    Write-Host "$($script:C_G)  [STATUS] Data: $(Get-Date -Format 'dd/MM/yyyy HH:mm:ss')"
    Write-Host "  [STATUS] Host: $env:COMPUTERNAME | SysAdmin: $env:USERNAME"
    try {
        $OSInfo = (Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue).Caption
        Write-Host "  [STATUS] OS: $OSInfo"
    } catch {}
    Write-Host "$($script:C_G)  --------------------------------------------------------$($script:C_RST)`n"
}

function Executar-Etapa {
    param(
        [string]$Nome,
        [scriptblock]$Acao
    )
    $TStart = Get-Date -Format "HH:mm:ss"
    Write-Host "$($script:C_G) [..] [$TStart] $Nome...$($script:C_RST)" -NoNewline
    try {
        & $Acao
        $TEnd = Get-Date -Format "HH:mm:ss"
        Write-Host "`r$($script:C_GN) [ OK ] [$TEnd] $Nome                                              $($script:C_RST)"
    } catch {
        $TEnd = Get-Date -Format "HH:mm:ss"
        Write-Host "`r$($script:C_R) [ERRO] [$TEnd] $Nome                                              $($script:C_RST)"
        Write-Host "$($script:C_R)        Detalhe: $($_.Exception.Message)$($script:C_RST)"
    }
}
