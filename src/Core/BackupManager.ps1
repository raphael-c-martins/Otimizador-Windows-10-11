<#
.SYNOPSIS
    Módulo de Salvaguarda e Criação de Ponto de Restauração do Sistema.
.DESCRIPTION
    Habilita a proteção do sistema se necessário, cria o Ponto de Restauração e CONFIRMA que o Windows
    realmente o registrou. Em caso de falha lança exceção: quem chama decide se continua ou aborta.
#>

function Invoke-SystemRestorePoint {
    param(
        [Parameter(Mandatory)][string]$Description
    )

    Write-OptimizerLog -Level INFO -Message 'Verificando a Proteção do Sistema e criando o Ponto de Restauração...'

    # 1. Serviço de Cópias de Sombra de Volume (necessário para o ponto de restauração)
    Set-Service -Name 'VSS' -StartupType Manual -ErrorAction Stop
    Start-Service -Name 'VSS' -ErrorAction Stop

    # 2. Proteção na unidade do sistema
    Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction Stop

    # 3. O Windows só aceita um ponto a cada 24 h: libera temporariamente e devolve o valor original depois
    $SubKey = 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore'
    $ValueName = 'SystemRestorePointCreationFrequency'
    $Base = Open-BaseRegistryKey -Hive 'LocalMachine'
    try {
        $Key = $Base.CreateSubKey($SubKey, $true)
        $Existed = @($Key.GetValueNames()) -contains $ValueName
        $Original = if ($Existed) { $Key.GetValue($ValueName) } else { $null }
        $Key.SetValue($ValueName, 0, [Microsoft.Win32.RegistryValueKind]::DWord)
        try {
            Checkpoint-Computer -Description $Description -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop
        } finally {
            if ($Existed) { $Key.SetValue($ValueName, [int]$Original, [Microsoft.Win32.RegistryValueKind]::DWord) }
            else { $Key.DeleteValue($ValueName, $false) }
            $Key.Dispose()
        }
    } finally {
        $Base.Dispose()
    }

    # 4. Confirma que o ponto existe de fato (o Checkpoint-Computer pode "passar" sem criar nada)
    $Created = @(Get-ComputerRestorePoint -ErrorAction Stop | Where-Object { $_.Description -eq $Description })
    if ($Created.Count -eq 0) {
        throw 'O Windows não registrou o Ponto de Restauração (a Proteção do Sistema pode estar bloqueada por política).'
    }

    Write-OptimizerLog -Level OK -Message "Ponto de Restauração criado e confirmado: '$Description'."
    return $true
}

function New-RestorePointDescription {
    param([string]$Label = 'Otimizador')
    return "Otimizador_${Label}_$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss')"
}
