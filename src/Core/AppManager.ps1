<#
.SYNOPSIS
    Módulo de Gerenciamento e Remoção de Aplicativos e Bloatware.
.DESCRIPTION
    Remove pacotes UWP/Appx (instalados e provisionados) e, quando houver, faz a desinstalação
    complementar via WinGet. Falhas reais lançam exceção para que a etapa seja reportada como ERRO.
#>

$script:WinGetTimeoutSeconds = 300
$script:WinGetNoPackageFound = -1978335212  # 0x8A150014: nenhum pacote instalado corresponde ao ID

function Get-ProvisionedPackageCache {
    # Leitura única dos pacotes provisionados (consulta DISM lenta) reaproveitada por todas as remoções
    try {
        return @(Get-AppxProvisionedPackage -Online -ErrorAction Stop)
    } catch {
        Write-OptimizerLog -Level AVISO -Message "Não foi possível listar os pacotes provisionados ($($_.Exception.Message))."
        return @()
    }
}

function ConvertTo-PackageWildcard {
    <#
    .SYNOPSIS
        Padrões com "*" são usados como estão; nomes sem "*" exigem correspondência exata do pacote
        (ex.: "MSTeams" não pode remover "MicrosoftTeams" e vice-versa).
    #>
    param([Parameter(Mandatory)][string]$Pattern)
    return $Pattern.Trim()
}

function Remove-AppByDefinition {
    param(
        [Parameter(Mandatory)][object]$AppInfo,
        [array]$CachedProvisioned = $null
    )

    if ($null -eq $CachedProvisioned) { $CachedProvisioned = Get-ProvisionedPackageCache }

    $Errors = [System.Collections.Generic.List[string]]::new()
    $Found = 0

    foreach ($Pattern in ($AppInfo.PackageName -split ',')) {
        if ([string]::IsNullOrWhiteSpace($Pattern)) { continue }
        $WildPattern = ConvertTo-PackageWildcard -Pattern $Pattern

        # 1. Pacotes instalados para todos os usuários
        foreach ($Pkg in @(Get-AppxPackage -Name $WildPattern -AllUsers -ErrorAction SilentlyContinue)) {
            $Found++
            try {
                Remove-AppxPackage -Package $Pkg.PackageFullName -AllUsers -ErrorAction Stop
            } catch {
                try {
                    Remove-AppxPackage -Package $Pkg.PackageFullName -ErrorAction Stop
                } catch {
                    $Errors.Add("$($Pkg.Name): $($_.Exception.Message)")
                }
            }
        }

        # 2. Pacotes provisionados (reinstalados automaticamente em novos perfis de usuário)
        $ProvMatches = @($CachedProvisioned | Where-Object { ($_.DisplayName -like $WildPattern) -or ($_.PackageName -like $WildPattern) })
        foreach ($ProvPkg in $ProvMatches) {
            $Found++
            try {
                Remove-AppxProvisionedPackage -Online -PackageName $ProvPkg.PackageName -ErrorAction Stop | Out-Null
            } catch {
                $Errors.Add("$($ProvPkg.DisplayName) (provisionado): $($_.Exception.Message)")
            }
        }
    }

    # 3. Desinstalação complementar via WinGet (versões instaladas fora da Microsoft Store)
    if (-not [string]::IsNullOrWhiteSpace($AppInfo.WinGetId)) {
        Invoke-WinGetUninstall -WinGetId $AppInfo.WinGetId
    }

    if ($Found -eq 0) {
        Write-OptimizerLog -Level INFO -Message "$($AppInfo.FriendlyName): não instalado (nada a remover)."
    }
    if ($Errors.Count -gt 0) {
        throw ($Errors -join ' | ')
    }
}

function Invoke-WinGetUninstall {
    param([Parameter(Mandatory)][string]$WinGetId)

    if ($null -eq (Get-Command 'winget.exe' -ErrorAction SilentlyContinue)) { return }

    $Arguments = "uninstall --id `"$WinGetId`" --exact --silent --force --disable-interactivity --accept-source-agreements"
    $Process = Start-Process -FilePath 'winget.exe' -ArgumentList $Arguments -WindowStyle Hidden -PassThru -ErrorAction Stop

    if (-not $Process.WaitForExit($script:WinGetTimeoutSeconds * 1000)) {
        try { $Process.Kill() } catch {}
        Write-OptimizerLog -Level AVISO -Message "WinGet excedeu $($script:WinGetTimeoutSeconds) s ao remover '$WinGetId' e foi interrompido."
        return
    }

    if ($Process.ExitCode -eq 0) {
        Write-OptimizerLog -Level INFO -Message "WinGet removeu '$WinGetId'."
    } elseif ($Process.ExitCode -ne $script:WinGetNoPackageFound) {
        Write-OptimizerLog -Level AVISO -Message "WinGet retornou código $($Process.ExitCode) ao remover '$WinGetId'."
    }
}

function Remove-OneDriveDeep {
    Stop-Process -Name 'OneDrive' -Force -ErrorAction SilentlyContinue

    # Instalação por usuário (versões recentes) registra o desinstalador no perfil do usuário
    $Setup = $null
    $Arguments = '/uninstall'
    $UserUninstall = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe'
    $Location = ConvertTo-RegistryLocation -Path $UserUninstall
    $Base = Open-BaseRegistryKey -Hive $Location.Hive
    try {
        $Key = $Base.OpenSubKey($Location.SubKey)
        if ($null -ne $Key) {
            $UnStr = [string]$Key.GetValue('UninstallString')
            $Key.Dispose()
            if ($UnStr -match '^\s*"([^"]+)"\s*(.*)$') { $Setup = $Matches[1]; $Arguments = $Matches[2] }
        }
    } finally {
        $Base.Dispose()
    }

    if (-not $Setup) {
        foreach ($Candidate in @("$env:SystemRoot\System32\OneDriveSetup.exe", "$env:SystemRoot\SysWOW64\OneDriveSetup.exe")) {
            if (Test-Path -LiteralPath $Candidate) { $Setup = $Candidate; break }
        }
    }

    if (-not $Setup -or -not (Test-Path -LiteralPath $Setup)) {
        Write-OptimizerLog -Level INFO -Message 'OneDrive não encontrado (já removido ou não instalado).'
        return
    }

    $Process = Start-Process -FilePath $Setup -ArgumentList $Arguments -Wait -PassThru -WindowStyle Hidden -ErrorAction Stop
    if ($Process.ExitCode -ne 0) {
        Write-OptimizerLog -Level AVISO -Message "Desinstalador do OneDrive retornou código $($Process.ExitCode)."
    } else {
        Write-OptimizerLog -Level INFO -Message 'OneDrive desinstalado. Os arquivos locais da pasta OneDrive foram preservados.'
    }
}
