<#
.SYNOPSIS
    Módulo de Interface de Terminal ANSI (Console TUI).
.DESCRIPTION
    Fornece cabeçalho, saída colorida dos logs do motor de execução e o resumo final das etapas.
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

function Show-MainHeader {
    param(
        [string]$SubTitle = 'PROTOCOLO DE OTIMIZAÇÃO & DEBLOAT'
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
    Write-Host "  [STATUS] Host: $env:COMPUTERNAME | Usuário: $env:USERNAME"
    try {
        $OSInfo = (Get-CimInstance Win32_OperatingSystem -ErrorAction Stop).Caption
        Write-Host "  [STATUS] OS: $OSInfo"
    } catch {}
    Write-Host "$($script:C_G)  --------------------------------------------------------$($script:C_RST)`n"
}

function Write-ConsoleLogEntry {
    <#
    .SYNOPSIS
        Saída em tela das mensagens do motor (usada como "sink" do Write-OptimizerLog no modo terminal).
    #>
    param([string]$Level, [string]$Message)
    $Time = Get-Date -Format 'HH:mm:ss'
    switch ($Level) {
        'ETAPA' { Write-Host "`n$($script:C_C) >> [$Time] $Message$($script:C_RST)" }
        'OK'    { Write-Host "$($script:C_GN)    [ OK ] $Message$($script:C_RST)" }
        'AVISO' { Write-Host "$($script:C_Y)    [AVISO] $Message$($script:C_RST)" }
        'ERRO'  { Write-Host "$($script:C_R)    [ERRO] $Message$($script:C_RST)" }
        default { Write-Host "$($script:C_G)    [INFO] $Message$($script:C_RST)" }
    }
}

function Write-ConsoleProgress {
    param([int]$Current, [int]$Total, [string]$Activity)
    if ($Total -le 0) { return }
    if ($Current -ge $Total) {
        Write-Progress -Activity 'Otimizando o Windows' -Completed
        return
    }
    Write-Progress -Activity 'Otimizando o Windows' -Status "$Activity ($Current/$Total)" -PercentComplete ([math]::Round(($Current / $Total) * 100))
}

function Show-ExecutionSummary {
    param(
        [Parameter(Mandatory)]$Result,
        [string]$LogPath
    )
    Write-Host "`n$($script:C_G)  --------------------------------------------------------$($script:C_RST)"
    if ($Result.DryRun) {
        Write-Host "$($script:C_C)  SIMULAÇÃO CONCLUÍDA: $($Result.Simulated) etapas seriam executadas (nada foi alterado).$($script:C_RST)"
    } else {
        $Color = if ($Result.Failed -gt 0) { $script:C_R } elseif ($Result.Warnings -gt 0) { $script:C_Y } else { $script:C_GN }
        Write-Host "$Color  RESULTADO: $($Result.Ok) OK | $($Result.Warnings) com aviso | $($Result.Failed) com erro$($script:C_RST)"
        foreach ($Step in $Result.Steps | Where-Object { $_.Status -eq 'ERRO' }) {
            Write-Host "$($script:C_R)   - $($Step.Name): $($Step.Detail)$($script:C_RST)"
        }
    }
    if ($LogPath) { Write-Host "$($script:C_C)  Log detalhado: $LogPath$($script:C_RST)" }
    Write-Host "$($script:C_G)  --------------------------------------------------------$($script:C_RST)"
}

function Read-YesNo {
    param([Parameter(Mandatory)][string]$Prompt)
    $Answer = Read-Host "$($script:C_C) [?] $Prompt (S/N)$($script:C_RST)"
    return ($Answer -match '^[SsYy]$')
}
