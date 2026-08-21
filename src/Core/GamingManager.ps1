<#
.SYNOPSIS
    Módulo de Otimizações de Jogos e Tratamento de Protocolos Xbox.
.DESCRIPTION
    Aplica otimizações de FPS (desativação de GameDVR de fundo) e resolve o problema de
    popups de erro ("ms-gamingoverlay" / "ms-gamebar") através de redirecionamento seguro para systray.exe.
#>

function Disable-GameDVRBackgroundCapture {
    try {
        # Desativa gravação de tela em segundo plano no GameDVR (ganho de CPU/I-O)
        $GameConfig = "HKCU:\System\GameConfigStore"
        if (!(Test-Path $GameConfig)) { New-Item -Path $GameConfig -Force | Out-Null }
        Set-ItemProperty -Path $GameConfig -Name "GameDVR_Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $GameConfig -Name "GameDVR_FSEBehaviorMode" -Value 2 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $GameConfig -Name "GameDVR_HonorUserFSEBehaviorMode" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $GameConfig -Name "GameDVR_DXGIHonorFSEWindowsCompatible" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        $GameDVR = "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR"
        if (!(Test-Path $GameDVR)) { New-Item -Path $GameDVR -Force | Out-Null }
        Set-ItemProperty -Path $GameDVR -Name "AppCaptureEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $GameDVR -Name "AudioCaptureEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $GameDVR -Name "CursorCaptureEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty -Path $GameDVR -Name "HistoricalCaptureEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # Desativa a captura via GPO local
        $PolicyGameDVR = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR"
        if (!(Test-Path $PolicyGameDVR)) { New-Item -Path $PolicyGameDVR -Force | Out-Null }
        Set-ItemProperty -Path $PolicyGameDVR -Name "AllowGameDVR" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # Desativa abertura do Game Bar pelo botão Guia do controle
        $GameBar = "HKCU:\SOFTWARE\Microsoft\GameBar"
        if (!(Test-Path $GameBar)) { New-Item -Path $GameBar -Force | Out-Null }
        Set-ItemProperty -Path $GameBar -Name "UseNexusForGameBarEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
    } catch {}
}

function Apply-GameBarProtocolNullRoute {
    <#
    .SYNOPSIS
        Neutraliza popups de erro ("Você precisará de um novo aplicativo...") ao abrir jogos.
    .DESCRIPTION
        Redireciona os manipuladores de protocolo do Xbox Game Bar para o systray.exe, silenciando requisições fantasmas.
    #>
    try {
        $Protocols = @("ms-gamebar", "ms-gamebarservices", "ms-gamingoverlay")

        foreach ($Proto in $Protocols) {
            $RootKey = "Registry::HKEY_CLASSES_ROOT\$Proto"
            if (!(Test-Path $RootKey)) { New-Item -Path $RootKey -Force | Out-Null }
            Set-ItemProperty -Path $RootKey -Name "(default)" -Value "URL:$Proto" -Force -ErrorAction SilentlyContinue | Out-Null
            Set-ItemProperty -Path $RootKey -Name "URL Protocol" -Value "" -Force -ErrorAction SilentlyContinue | Out-Null
            Set-ItemProperty -Path $RootKey -Name "NoOpenWith" -Value "" -Force -ErrorAction SilentlyContinue | Out-Null

            $CommandKey = "$RootKey\shell\open\command"
            if (!(Test-Path $CommandKey)) { New-Item -Path $CommandKey -Force | Out-Null }
            Set-ItemProperty -Path $CommandKey -Name "(default)" -Value "$env:SystemRoot\System32\systray.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        }
    } catch {}
}

function Restore-GameBarProtocols {
    <#
    .SYNOPSIS
        Restaura o comportamento padrão dos protocolos caso o usuário queira reinstalar o Xbox Game Bar.
    #>
    try {
        $Protocols = @("ms-gamebar", "ms-gamebarservices", "ms-gamingoverlay")
        foreach ($Proto in $Protocols) {
            $RootKey = "Registry::HKEY_CLASSES_ROOT\$Proto"
            if (Test-Path $RootKey) {
                Remove-Item -Path $RootKey -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
            }
        }
    } catch {}
}
