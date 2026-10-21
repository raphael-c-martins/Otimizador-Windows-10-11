<#
.SYNOPSIS
    Módulo de Ajustes de Performance, Interface, Energia e Limpeza.
.DESCRIPTION
    Cada função corresponde a UM ajuste do catálogo (Config/Tweaks.json). Ajustes de energia respeitam
    notebooks: a hibernação e o plano de energia atual são preservados em equipamentos com bateria.
#>

$script:UltimatePerformanceTemplate = 'e9a42b02-d5df-448d-aa00-03f14749eb61'
$script:HighPerformanceScheme = '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'

#region Interface e Explorer

function Show-FileExtensions {
    Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'HideFileExt' -Value 0
}

function Hide-TaskbarExtraButtons {
    param([bool]$IsWin11 = $true)
    $ExpAdv = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
    Set-RegistryValue -Path $ExpAdv -Name 'ShowTaskViewButton' -Value 0
    if ($IsWin11) {
        Set-RegistryValue -Path $ExpAdv -Name 'TaskbarMn' -Value 0
    } else {
        Set-RegistryValue -Path $ExpAdv -Name 'PeopleBand' -Value 0
    }
}

function Enable-ClassicContextMenu {
    param([bool]$IsWin11 = $true)
    if (-not $IsWin11) {
        Write-OptimizerLog -Level INFO -Message 'O menu de contexto clássico já é o padrão no Windows 10 (nada a fazer).'
        return
    }
    $Key = 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32'
    Set-RegistryValue -Path $Key -Name '(default)' -Value '' -Type String
}

function Set-OptimizedVisualEffects {
    # Desativa animações e transparência mantendo a suavização de fontes (ClearType)
    Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' -Name 'VisualFXSetting' -Value 3
    Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name 'EnableTransparency' -Value 0
    Set-RegistryValue -Path 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name 'MinAnimate' -Value '0' -Type String
    Set-RegistryValue -Path 'HKCU:\Control Panel\Desktop' -Name 'FontSmoothing' -Value '2' -Type String
    Set-RegistryValue -Path 'HKCU:\Control Panel\Desktop' -Name 'FontSmoothingType' -Value 2
}

#endregion

#region Energia

function Disable-FastStartup {
    param([bool]$IsPortable = $false)

    # Inicialização Rápida: o desligamento passa a ser completo (evita drivers "presos" entre sessões)
    Set-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -Name 'HiberbootEnabled' -Value 0

    if ($IsPortable) {
        Write-OptimizerLog -Level INFO -Message 'Notebook detectado: a hibernação foi mantida (o Windows a usa quando a bateria fica crítica).'
        return
    }

    Register-PowerStateSnapshot
    & powercfg.exe /hibernate off | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "powercfg não conseguiu desativar a hibernação (código $LASTEXITCODE)." }
}

function Get-PowerSchemeGuids {
    $Guids = @()
    foreach ($Line in (& powercfg.exe /list)) {
        if ($Line -match '([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})') {
            $Guids += $Matches[1].ToLower()
        }
    }
    return $Guids
}

function Enable-UltimatePerformancePlan {
    param(
        [bool]$IsPortable = $false,
        [string]$StateDirectory = $script:OptimizerDataRoot
    )

    if ($IsPortable) {
        Write-OptimizerLog -Level INFO -Message 'Notebook detectado: o plano Ultimate Performance não foi ativado para preservar a bateria.'
        return
    }

    Register-PowerStateSnapshot
    $Existing = Get-PowerSchemeGuids
    $GuidFile = Join-Path $StateDirectory 'plano-ultimate.guid'
    $Target = $null

    # 1. Reaproveita o plano criado em execuções anteriores (evita acumular cópias a cada execução)
    if (Test-Path -LiteralPath $GuidFile) {
        $Saved = (Get-Content -LiteralPath $GuidFile -Raw).Trim().ToLower()
        if ($Existing -contains $Saved) { $Target = $Saved }
    }
    # 2. Algumas edições já trazem o plano Ultimate visível
    if (-not $Target -and $Existing -contains $script:UltimatePerformanceTemplate) {
        $Target = $script:UltimatePerformanceTemplate
    }
    # 3. Cria uma cópia a partir do modelo oculto
    if (-not $Target) {
        $DupOutput = (& powercfg.exe /duplicatescheme $script:UltimatePerformanceTemplate 2>&1) -join ' '
        if ($LASTEXITCODE -eq 0 -and $DupOutput -match '([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})') {
            $Target = $Matches[1].ToLower()
            Register-PowerSchemeCreated -Guid $Target
            if (-not (Test-Path -LiteralPath $StateDirectory)) { New-Item -ItemType Directory -Path $StateDirectory -Force | Out-Null }
            Set-Content -LiteralPath $GuidFile -Value $Target -Encoding ASCII
        }
    }
    # 4. Edições com Modern Standby bloqueiam o Ultimate: usa Alto Desempenho se existir
    if (-not $Target) {
        if ($Existing -contains $script:HighPerformanceScheme) {
            Write-OptimizerLog -Level AVISO -Message 'Plano Ultimate Performance indisponível nesta edição; ativado o plano Alto Desempenho.'
            $Target = $script:HighPerformanceScheme
        } else {
            throw 'Nem o plano Ultimate Performance nem o Alto Desempenho estão disponíveis neste equipamento.'
        }
    }

    & powercfg.exe /setactive $Target | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "powercfg não conseguiu ativar o plano $Target (código $LASTEXITCODE)." }
}

#endregion

#region Serviços e Aplicativos de Sistema

function Disable-LegacyHeavyServices {
    <#
    .SYNOPSIS
        Desativa SysMain e WSearch para hardware com HDD mecânico ou pouca memória RAM.
    #>
    Set-ServiceStartupTracked -Name 'WSearch' -StartupType Disabled -Stop
    Set-ServiceStartupTracked -Name 'SysMain' -StartupType Disabled -Stop
}

function Remove-MicrosoftEdge {
    <#
    .SYNOPSIS
        Desinstalação profunda do Microsoft Edge com limpeza de atalhos. NÃO é revertida pelo Undo.
    #>
    Set-RegistryValue -Path 'HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev' -Name 'AllowUninstall' -Value 1

    # Stub de compatibilidade: o instalador do Edge recusa a remoção se não encontrar este arquivo
    try {
        $EdgeStub = "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe"
        if (-not (Test-Path -LiteralPath $EdgeStub)) { New-Item $EdgeStub -ItemType Directory -Force -ErrorAction Stop | Out-Null }
        New-Item "$EdgeStub\MicrosoftEdge.exe" -ItemType File -Force -ErrorAction Stop | Out-Null
    } catch {
        Write-OptimizerLog -Level AVISO -Message "Não foi possível criar o arquivo de compatibilidade do Edge ($($_.Exception.Message))."
    }

    $UninstallKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge'
    $WowUninstallKey = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge'
    $KeyPath = @($UninstallKey, $WowUninstallKey) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $KeyPath) {
        Write-OptimizerLog -Level INFO -Message 'Microsoft Edge não encontrado (já removido ou não instalado).'
        return
    }

    $UnStr = (Get-ItemProperty -LiteralPath $KeyPath -ErrorAction Stop).UninstallString
    if ([string]::IsNullOrWhiteSpace($UnStr)) { throw 'Comando de desinstalação do Edge não encontrado no registro.' }

    # Separa o executável (entre aspas) dos argumentos para não depender das regras de aspas do cmd.exe
    if ($UnStr -match '^\s*"([^"]+)"\s*(.*)$') {
        $Exe = $Matches[1]; $Arguments = $Matches[2]
    } elseif ($UnStr -match '^\s*(\S+)\s*(.*)$') {
        $Exe = $Matches[1]; $Arguments = $Matches[2]
    } else {
        throw "Comando de desinstalação do Edge em formato inesperado: '$UnStr'."
    }
    $Process = Start-Process -FilePath $Exe -ArgumentList "$Arguments --force-uninstall" -WindowStyle Hidden -Wait -PassThru -ErrorAction Stop
    Write-OptimizerLog -Level INFO -Message "Desinstalador do Edge finalizado (código $($Process.ExitCode))."

    if (Test-Path -LiteralPath $KeyPath) {
        Write-OptimizerLog -Level AVISO -Message 'O Edge continua registrado: o Windows pode ter bloqueado a remoção nesta região/edição.'
    }

    $Shortcuts = @(
        "$env:PUBLIC\Desktop\Microsoft Edge.lnk",
        "$env:USERPROFILE\Desktop\Microsoft Edge.lnk",
        "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk",
        "$env:APPDATA\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Microsoft Edge.lnk"
    )
    foreach ($Lnk in $Shortcuts) {
        if (Test-Path -LiteralPath $Lnk) { Remove-Item -LiteralPath $Lnk -Force -ErrorAction SilentlyContinue }
    }
}

#endregion

#region Limpeza e Explorer

function Test-PathIsWithin {
    <#
    .SYNOPSIS
        Indica se $Path é igual a $Parent ou está dentro dele, comparando por componentes do caminho.
    .DESCRIPTION
        Os caminhos são normalizados (resolve "..") e comparados pasta a pasta, evitando o erro clássico de
        comparar texto cru (ex.: "C:\Temp\App" não pode ser considerado dentro de "C:\Temp\Ap").
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Parent
    )
    $PathParts = [System.IO.Path]::GetFullPath($Path).TrimEnd('\').Split('\')
    $ParentParts = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\').Split('\')
    if ($PathParts.Count -lt $ParentParts.Count) { return $false }
    for ($i = 0; $i -lt $ParentParts.Count; $i++) {
        if (-not [string]::Equals($PathParts[$i], $ParentParts[$i], [StringComparison]::OrdinalIgnoreCase)) { return $false }
    }
    return $true
}

function Clear-SystemTempFiles {
    param(
        [string[]]$ProtectedPaths = @(),
        [string[]]$TempRoots = @($env:TEMP, (Join-Path $env:SystemRoot 'Temp'))
    )

    $Removed = 0
    $InUse = 0
    $Protected = 0

    foreach ($Root in $TempRoots) {
        if ([string]::IsNullOrWhiteSpace($Root) -or -not (Test-Path -LiteralPath $Root)) { continue }

        foreach ($Item in (Get-ChildItem -LiteralPath $Root -Force -ErrorAction SilentlyContinue)) {
            # Nunca apaga a pasta do próprio programa nem a de logs (nem as pastas que as contêm)
            $IsProtected = $false
            foreach ($Keep in $ProtectedPaths) {
                if ([string]::IsNullOrWhiteSpace($Keep)) { continue }
                if ((Test-PathIsWithin -Path $Keep -Parent $Item.FullName) -or (Test-PathIsWithin -Path $Item.FullName -Parent $Keep)) {
                    $IsProtected = $true
                    break
                }
            }
            if ($IsProtected) { $Protected++; continue }

            try {
                Remove-Item -LiteralPath $Item.FullName -Recurse -Force -ErrorAction Stop
                $Removed++
            } catch {
                $InUse++
            }
        }
    }

    Write-OptimizerLog -Level INFO -Message "Temporários: $Removed itens removidos, $InUse em uso mantidos, $Protected protegidos."
}

function Restart-WindowsExplorer {
    # Reinicia apenas o Explorer da sessão atual (não afeta outros usuários conectados)
    $SessionId = (Get-Process -Id $PID).SessionId
    $Explorers = @(Get-Process -Name 'explorer' -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $SessionId })
    if ($Explorers.Count -eq 0) { return }

    $Explorers | Stop-Process -Force -ErrorAction Stop

    # O Windows reinicia o Explorer sozinho; se não reiniciar em 5 s, inicia com privilégio de usuário comum
    for ($i = 0; $i -lt 10; $i++) {
        Start-Sleep -Milliseconds 500
        if (@(Get-Process -Name 'explorer' -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $SessionId }).Count -gt 0) { return }
    }
    Start-Process -FilePath "$env:SystemRoot\System32\runas.exe" -ArgumentList '/trustlevel:0x20000', "`"$env:SystemRoot\explorer.exe`"" -WindowStyle Hidden
    Write-OptimizerLog -Level AVISO -Message 'O Explorer não reiniciou sozinho e foi iniciado manualmente.'
}

#endregion
