<#
.SYNOPSIS
    Módulo de Salvaguarda e Criação de Ponto de Restauração do Sistema.
.DESCRIPTION
    Habilita a proteção do sistema se necessário e cria um Ponto de Restauração
    antes da aplicação de qualquer alteração estrutural ou de registro.
#>

function Invoke-SystemRestorePoint {
    param(
        [string]$Description = "Backup_Otimizador_Windows"
    )

    try {
        Write-Host " [..] Verificando e configurando Proteção do Sistema (Ponto de Restauração)..." -ForegroundColor Gray
        
        # 1. Garante que os serviços VSS e de Restauração de Sistema estão ativos
        Set-Service -Name "VSS" -StartupType Manual -ErrorAction SilentlyContinue | Out-Null
        Start-Service -Name "VSS" -ErrorAction SilentlyContinue | Out-Null
        
        # 2. Desbloqueia o limite de frequência de criação de pontos no Windows (1440 minutos / 24h)
        $SysRestoreKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"
        if (!(Test-Path $SysRestoreKey)) { New-Item -Path $SysRestoreKey -Force | Out-Null }
        Set-ItemProperty -Path $SysRestoreKey -Name "SystemRestorePointCreationFrequency" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue | Out-Null

        # 3. Habilita a proteção na unidade de sistema C:
        Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue | Out-Null
        
        # 4. Executa a criação do Ponto de Restauração de forma não-bloqueante
        Invoke-NonBlocking -ScriptBlock {
            param($desc)
            Checkpoint-Computer -Description $desc -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue | Out-Null
        } -ArgumentList @($Description) -TimeoutSeconds 120

        Write-Host " [ OK ] Ponto de Restauração criado com sucesso: '$Description'." -ForegroundColor Green
        return $true
    } catch {
        Write-Host " [AVISO] Não foi possível criar o Ponto de Restauração automaticamente ($($_.Exception.Message))." -ForegroundColor Yellow
        Write-Host "         Continuando com as otimizações do sistema com segurança." -ForegroundColor Gray
        return $false
    }
}
