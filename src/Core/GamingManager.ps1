<#
.SYNOPSIS
    Módulo de Otimizações de Jogos e Tratamento de Protocolos Xbox.
.DESCRIPTION
    Aplica otimizações de FPS (desativação de GameDVR de fundo) e resolve o problema de
    popups de erro ("ms-gamingoverlay" / "ms-gamebar") através de redirecionamento seguro para systray.exe.
#>

$script:GameBarProtocols = @('ms-gamebar', 'ms-gamebarservices', 'ms-gamingoverlay')

function Disable-GameDVRBackgroundCapture {
    # Desativa gravação de tela em segundo plano no GameDVR (ganho de CPU/I-O)
    $GameConfig = 'HKCU:\System\GameConfigStore'
    Set-RegistryValue -Path $GameConfig -Name 'GameDVR_Enabled' -Value 0
    Set-RegistryValue -Path $GameConfig -Name 'GameDVR_FSEBehaviorMode' -Value 2
    Set-RegistryValue -Path $GameConfig -Name 'GameDVR_HonorUserFSEBehaviorMode' -Value 1
    Set-RegistryValue -Path $GameConfig -Name 'GameDVR_DXGIHonorFSEWindowsCompatible' -Value 1

    $GameDVR = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR'
    foreach ($ValueName in @('AppCaptureEnabled', 'AudioCaptureEnabled', 'CursorCaptureEnabled', 'HistoricalCaptureEnabled')) {
        Set-RegistryValue -Path $GameDVR -Name $ValueName -Value 0
    }

    # Desativa a captura via GPO local
    Set-RegistryValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Name 'AllowGameDVR' -Value 0

    # Desativa abertura do Game Bar pelo botão Guia do controle
    Set-RegistryValue -Path 'HKCU:\SOFTWARE\Microsoft\GameBar' -Name 'UseNexusForGameBarEnabled' -Value 0
}

function Get-GameBarProtocolRoot {
    param([Parameter(Mandatory)][string]$Protocol)
    # Se o usuário já tem um registro próprio do protocolo, ele tem prioridade sobre o da máquina: altera lá.
    if (Test-RegistryKeyExists -Path "HKCU:\Software\Classes\$Protocol") {
        return "HKCU:\Software\Classes\$Protocol"
    }
    return "HKLM:\SOFTWARE\Classes\$Protocol"
}

function Set-GameBarProtocolNullRoute {
    <#
    .SYNOPSIS
        Neutraliza popups de erro ("Você precisará de um novo aplicativo...") ao abrir jogos.
    .DESCRIPTION
        Redireciona os manipuladores de protocolo do Xbox Game Bar para o systray.exe, silenciando requisições fantasmas.
    #>
    foreach ($Proto in $script:GameBarProtocols) {
        $RootKey = Get-GameBarProtocolRoot -Protocol $Proto
        Set-RegistryValue -Path $RootKey -Name '(default)' -Value "URL:$Proto" -Type String
        Set-RegistryValue -Path $RootKey -Name 'URL Protocol' -Value '' -Type String
        Set-RegistryValue -Path $RootKey -Name 'NoOpenWith' -Value '' -Type String
        Set-RegistryValue -Path "$RootKey\shell\open\command" -Name '(default)' -Value "$env:SystemRoot\System32\systray.exe" -Type String
    }
}

function Restore-GameBarProtocols {
    <#
    .SYNOPSIS
        Remove o redirecionamento dos protocolos (usado apenas pela restauração de padrões sem diário).
    #>
    foreach ($Proto in $script:GameBarProtocols) {
        foreach ($Path in @("HKLM:\SOFTWARE\Classes\$Proto\shell\open\command", "HKCU:\Software\Classes\$Proto\shell\open\command")) {
            $Location = ConvertTo-RegistryLocation -Path $Path
            $Base = Open-BaseRegistryKey -Hive $Location.Hive
            try {
                $Key = $Base.OpenSubKey($Location.SubKey)
                if ($null -eq $Key) { continue }
                $Target = [string]$Key.GetValue('')
                $Key.Dispose()
                # Só remove o protocolo se ele aponta para o systray.exe (ou seja, foi criado por este programa).
                if ($Target -like '*systray.exe') {
                    $ProtoSubKey = $Location.SubKey -replace '\\shell\\open\\command$', ''
                    $Base.DeleteSubKeyTree($ProtoSubKey, $false)
                }
            } finally {
                $Base.Dispose()
            }
        }
    }
}
