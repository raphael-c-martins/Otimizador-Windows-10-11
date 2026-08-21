<#
.SYNOPSIS
    Módulo de Reversão e Restauração de Configurações do Sistema (Undo Engine).
.DESCRIPTION
    Permite restaurar serviços essenciais (SysMain, WSearch), menu de contexto nativo,
    protocolos do Xbox e configurações padrão caso o usuário necessite.
#>

function Invoke-SystemUndoTweaks {
    param(
        [bool]$IsWin11 = $true
    )

    Write-Host "`n [..] Iniciando processo de restauração das configurações padrão..." -ForegroundColor Yellow

    # 1. Restaurar serviços SysMain e WSearch
    try {
        Set-Service -Name "SysMain" -StartupType Automatic -ErrorAction SilentlyContinue | Out-Null
        Start-Service -Name "SysMain" -ErrorAction SilentlyContinue | Out-Null

        Set-Service -Name "WSearch" -StartupType Automatic -ErrorAction SilentlyContinue | Out-Null
        Start-Service -Name "WSearch" -ErrorAction SilentlyContinue | Out-Null
        Write-Host " [ OK ] Serviços SysMain e Windows Search reativados." -ForegroundColor Green
    } catch {}

    # 2. Restaurar menu de contexto nativo do Windows 11
    if ($IsWin11) {
        try {
            $Key = "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}"
            if (Test-Path $Key) {
                Remove-Item -Path $Key -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
                Write-Host " [ OK ] Menu de contexto moderno do Windows 11 restaurado." -ForegroundColor Green
            }
        } catch {}
    }

    # 3. Restaurar protocolos do Xbox Game Bar
    try {
        Restore-GameBarProtocols
        $GameConfig = "HKCU:\System\GameConfigStore"
        if (Test-Path $GameConfig) {
            Set-ItemProperty -Path $GameConfig -Name "GameDVR_Enabled" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        }
        $GameBar = "HKCU:\SOFTWARE\Microsoft\GameBar"
        if (Test-Path $GameBar) {
            Set-ItemProperty -Path $GameBar -Name "UseNexusForGameBarEnabled" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        }
        Write-Host " [ OK ] Protocolos e configurações do Xbox restaurados." -ForegroundColor Green
    } catch {}

    # 4. Reativar efeitos visuais padrão
    try {
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name "VisualFXSetting" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        $Theme = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
        if (Test-Path $Theme) {
            Set-ItemProperty -Path $Theme -Name "EnableTransparency" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null
        }
        Set-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "MinAnimate" -Value "1" -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Host " [ OK ] Efeitos visuais e transparências restaurados." -ForegroundColor Green
    } catch {}

    # 5. Reiniciar Explorer
    Restart-WindowsExplorer
    Write-Host " [ SUCESSO ] Restauração concluída com sucesso.`n" -ForegroundColor Green
}
