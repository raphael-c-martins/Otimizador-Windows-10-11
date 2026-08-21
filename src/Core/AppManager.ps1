<#
.SYNOPSIS
    Módulo de Gerenciamento e Remoção de Aplicativos e Bloatware de Alta Performance.
.DESCRIPTION
    Remove pacotes UWP/Appx e pacotes provisionados com pré-cache em memória para garantir
    execução instantânea sem sobrecarregar a engine DISM do Windows.
#>

function Remove-AppByDefinition {
    param(
        [object]$AppInfo,
        [array]$CachedProvisioned = $null
    )

    $FriendlyName = $AppInfo.FriendlyName
    $PackagePattern = $AppInfo.PackageName
    $WinGetId = $AppInfo.WinGetId

    $Patterns = $PackagePattern -split ','

    foreach ($Pattern in $Patterns) {
        $Pattern = $Pattern.Trim()
        if (-not [string]::IsNullOrWhiteSpace($Pattern)) {
            $WildPattern = "*$Pattern*"

            # 1. Remove pacotes Appx instalados para todos os usuários
            try {
                $InstalledPackages = Get-AppxPackage -Name $WildPattern -AllUsers -ErrorAction SilentlyContinue
                foreach ($Pkg in $InstalledPackages) {
                    try {
                        Remove-AppxPackage -Package $Pkg.PackageFullName -AllUsers -ErrorAction Stop | Out-Null
                    } catch {
                        Remove-AppxPackage -Package $Pkg.PackageFullName -ErrorAction SilentlyContinue | Out-Null
                    }
                }
            } catch {}

            # 2. Remove pacote provisionado usando o cache ou consulta direta rápida
            try {
                if ($null -ne $CachedProvisioned -and $CachedProvisioned.Count -gt 0) {
                    $ProvMatches = $CachedProvisioned | Where-Object { 
                        ($_.DisplayName -like $WildPattern) -or ($_.PackageName -like $WildPattern) 
                    }
                    foreach ($ProvPkg in $ProvMatches) {
                        Remove-AppxProvisionedPackage -Online -PackageName $ProvPkg.PackageName -ErrorAction SilentlyContinue | Out-Null
                    }
                } else {
                    $ProvMatches = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { 
                        ($_.DisplayName -like $WildPattern) -or ($_.PackageName -like $WildPattern) 
                    }
                    foreach ($ProvPkg in $ProvMatches) {
                        Remove-AppxProvisionedPackage -Online -PackageName $ProvPkg.PackageName -ErrorAction SilentlyContinue | Out-Null
                    }
                }
            } catch {}
        }
    }

    # 3. Se houver WinGetId e o winget estiver disponível, executa a desinstalação complementar silenciosa
    if (-not [string]::IsNullOrWhiteSpace($WinGetId)) {
        try {
            $WingetCmd = Get-Command "winget" -ErrorAction SilentlyContinue
            if ($null -ne $WingetCmd) {
                Start-Process -FilePath "winget.exe" -ArgumentList "uninstall --id `"$WinGetId`" --silent --accept-source-agreements --force" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue | Out-Null
            }
        } catch {}
    }
}

function Remove-AppListWithProgress {
    param(
        [array]$AppsToRemove,
        [scriptblock]$StatusCallback = $null
    )

    if ($null -eq $AppsToRemove -or $AppsToRemove.Count -eq 0) {
        Write-Host " [INFO] Nenhum aplicativo selecionado para remoção." -ForegroundColor Gray
        return
    }

    $Total = $AppsToRemove.Count
    $Current = 0

    # Pré-carregar lista provisionada uma única vez para ganho massivo de performance
    $CachedProvisioned = @()
    try {
        $CachedProvisioned = @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue)
    } catch {}

    foreach ($App in $AppsToRemove) {
        $Current++
        $Percent = [math]::Round(($Current / $Total) * 100)
        
        Write-Progress -Activity "Removendo Aplicativos e Bloatware" -Status "Processando: $($App.FriendlyName) ($Current/$Total)" -PercentComplete $Percent
        
        if ($null -ne $StatusCallback) {
            & $StatusCallback $App.FriendlyName $Current $Total
        }

        Remove-AppByDefinition -AppInfo $App -CachedProvisioned $CachedProvisioned
    }

    Write-Progress -Activity "Removendo Aplicativos e Bloatware" -Completed
}

function Remove-OneDriveDeep {
    try {
        Stop-Process -Name "OneDrive" -Force -ErrorAction SilentlyContinue | Out-Null
        $Setup = "$env:systemroot\System32\OneDriveSetup.exe"
        if (!(Test-Path $Setup)) { $Setup = "$env:systemroot\SysWOW64\OneDriveSetup.exe" }
        if (Test-Path $Setup) {
            Start-Process -FilePath $Setup -ArgumentList "/uninstall" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue | Out-Null
        }
    } catch {}
}
