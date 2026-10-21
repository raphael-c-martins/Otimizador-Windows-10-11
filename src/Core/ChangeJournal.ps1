<#
.SYNOPSIS
    Módulo de Alterações Rastreáveis (Registro, Serviços, Tarefas e Energia) com Diário para Reversão.
.DESCRIPTION
    Antes de cada alteração, o valor ORIGINAL é gravado em um diário (arquivo JSON por execução).
    O Undo lê esses diários do mais novo para o mais antigo e devolve o sistema ao estado exato anterior,
    em vez de aplicar "valores padrão" genéricos.
    Todas as funções lançam exceção quando a alteração falha, para que a etapa seja reportada como ERRO.
#>

$script:ChangeJournal = $null
$script:ChangeJournalPath = $null
$script:ChangeJournalKeys = $null
$script:UserRegistrySid = $null

#region Diário

function Start-ChangeJournal {
    param([Parameter(Mandatory)][string]$Directory)

    if (-not (Test-Path -LiteralPath $Directory)) { New-Item -ItemType Directory -Path $Directory -Force | Out-Null }
    $Stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss-fff'
    $script:ChangeJournalPath = Join-Path $Directory "estado_$Stamp.json"
    $script:ChangeJournal = [System.Collections.Generic.List[object]]::new()
    $script:ChangeJournalKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    return $script:ChangeJournalPath
}

function Stop-ChangeJournal {
    Save-ChangeJournal
    $script:ChangeJournal = $null
    $script:ChangeJournalKeys = $null
    $script:ChangeJournalPath = $null
}

function Save-ChangeJournal {
    if ($null -eq $script:ChangeJournal -or $script:ChangeJournal.Count -eq 0) { return }
    $Document = [ordered]@{
        Computador = $env:COMPUTERNAME
        CriadoEm   = (Get-Date).ToString('s')
        Entradas   = @($script:ChangeJournal)
    }
    $Json = $Document | ConvertTo-Json -Depth 6
    [System.IO.File]::WriteAllText($script:ChangeJournalPath, $Json, [System.Text.UTF8Encoding]::new($true))
}

function Add-ChangeJournalEntry {
    param(
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][object]$Entry
    )
    # Sem diário ativo (testes ou simulação): nada a registrar.
    if ($null -eq $script:ChangeJournal) { return }
    # Só o PRIMEIRO estado de cada item interessa: é ele que representa o valor original.
    if ($script:ChangeJournalKeys.Add($Key)) {
        $script:ChangeJournal.Add($Entry)
    }
}

function Set-UserRegistryTarget {
    param([string]$Sid = $null)
    $script:UserRegistrySid = $Sid
}

#endregion

#region Registro

function ConvertTo-RegistryLocation {
    param([Parameter(Mandatory)][string]$Path)

    if (-not ($Path -match '^(HKLM|HKCU|HKU):\\(.+)$')) {
        throw "Caminho de registro não suportado: '$Path'."
    }
    $Root = $Matches[1]
    $SubKey = $Matches[2].TrimEnd('\')

    switch ($Root) {
        'HKLM' { $Hive = 'LocalMachine' }
        'HKU'  { $Hive = 'Users' }
        'HKCU' {
            if ([string]::IsNullOrWhiteSpace($script:UserRegistrySid)) {
                $Hive = 'CurrentUser'
            } else {
                $Hive = 'Users'
                $SubKey = "$($script:UserRegistrySid)\$SubKey"
            }
        }
    }
    return [pscustomobject]@{ Hive = $Hive; SubKey = $SubKey }
}

function Open-BaseRegistryKey {
    param([Parameter(Mandatory)][string]$Hive)
    return [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]$Hive, [Microsoft.Win32.RegistryView]::Registry64)
}

function Test-RegistryKeyExists {
    param([Parameter(Mandatory)][string]$Path)
    $Location = ConvertTo-RegistryLocation -Path $Path
    $Base = Open-BaseRegistryKey -Hive $Location.Hive
    try {
        $Key = $Base.OpenSubKey($Location.SubKey)
        if ($null -eq $Key) { return $false }
        $Key.Dispose()
        return $true
    } finally {
        $Base.Dispose()
    }
}

function Register-MissingRegistryKey {
    param($Base, $Location)

    # Registra apenas a primeira chave inexistente do caminho: removê-la no Undo remove toda a árvore criada.
    $Parts = $Location.SubKey.Split('\')
    for ($i = 1; $i -le $Parts.Count; $i++) {
        $Partial = ($Parts[0..($i - 1)]) -join '\'
        $Probe = $Base.OpenSubKey($Partial)
        if ($null -eq $Probe) {
            Add-ChangeJournalEntry -Key "K|$($Location.Hive)|$Partial" -Entry ([ordered]@{
                Kind = 'RegistryKey'; Hive = $Location.Hive; SubKey = $Partial
            })
            return
        }
        $Probe.Dispose()
    }
}

function Register-RegistryValueSnapshot {
    param($Key, $Location, [string]$ValueName)

    $Existed = @($Key.GetValueNames()) -contains $ValueName
    $Entry = [ordered]@{
        Kind = 'RegistryValue'; Hive = $Location.Hive; SubKey = $Location.SubKey; Name = $ValueName
        Existed = $Existed; ValueKind = $null; Value = $null
    }
    if ($Existed) {
        $Kind = $Key.GetValueKind($ValueName)
        $Raw = $Key.GetValue($ValueName, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $Entry.ValueKind = [string]$Kind
        $Entry.Value = if ($Kind -eq [Microsoft.Win32.RegistryValueKind]::Binary) { [Convert]::ToBase64String([byte[]]$Raw) } else { $Raw }
    }
    Add-ChangeJournalEntry -Key "V|$($Location.Hive)|$($Location.SubKey)|$ValueName" -Entry $Entry
}

function Set-RegistryValue {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Name,
        [Parameter(Mandatory)][AllowEmptyString()][object]$Value,
        [ValidateSet('DWord', 'String')][string]$Type = 'DWord'
    )

    $Location = ConvertTo-RegistryLocation -Path $Path
    $ValueName = if ($Name -eq '(default)') { '' } else { $Name }

    $Base = Open-BaseRegistryKey -Hive $Location.Hive
    try {
        Register-MissingRegistryKey -Base $Base -Location $Location
        $Key = $Base.CreateSubKey($Location.SubKey, $true)
        if ($null -eq $Key) { throw "Não foi possível abrir ou criar a chave '$Path'." }
        try {
            Register-RegistryValueSnapshot -Key $Key -Location $Location -ValueName $ValueName
            if ($Type -eq 'DWord') {
                $Key.SetValue($ValueName, [int]$Value, [Microsoft.Win32.RegistryValueKind]::DWord)
            } else {
                $Key.SetValue($ValueName, [string]$Value, [Microsoft.Win32.RegistryValueKind]::String)
            }
        } finally {
            $Key.Dispose()
        }
    } finally {
        $Base.Dispose()
    }
}

function ConvertFrom-JournalRegistryValue {
    param($Entry)
    $Kind = [Microsoft.Win32.RegistryValueKind]$Entry.ValueKind
    switch ($Kind) {
        'Binary'      { return , [Convert]::FromBase64String([string]$Entry.Value) }
        'DWord'       { return [int]$Entry.Value }
        'QWord'       { return [long]$Entry.Value }
        'MultiString' { if ($null -eq $Entry.Value) { return , [string[]]@() } else { return , [string[]]@($Entry.Value) } }
        default       { return [string]$Entry.Value }
    }
}

#endregion

#region Serviços, Tarefas Agendadas e Energia

function Set-ServiceStartupTracked {
    param(
        [Parameter(Mandatory)][string]$Name,
        [ValidateSet('Disabled', 'Manual', 'Automatic')][string]$StartupType = 'Disabled',
        [switch]$Stop
    )

    $Service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $Service) {
        Write-OptimizerLog -Level INFO -Message "Serviço '$Name' não existe neste sistema (nada a fazer)."
        return
    }

    $Base = Open-BaseRegistryKey -Hive 'LocalMachine'
    try {
        $Key = $Base.OpenSubKey("SYSTEM\CurrentControlSet\Services\$Name")
        if ($null -ne $Key) {
            Add-ChangeJournalEntry -Key "S|$Name" -Entry ([ordered]@{
                Kind = 'Service'; Name = $Name
                Start = $Key.GetValue('Start'); DelayedAutostart = $Key.GetValue('DelayedAutostart')
            })
            $Key.Dispose()
        }
    } finally {
        $Base.Dispose()
    }

    Set-Service -Name $Name -StartupType $StartupType -ErrorAction Stop

    if ($Stop -and $Service.Status -ne 'Stopped') {
        try {
            Stop-Service -Name $Name -Force -ErrorAction Stop
        } catch {
            Write-OptimizerLog -Level AVISO -Message "Serviço '$Name' desativado, mas não pôde ser parado agora ($($_.Exception.Message)). Ficará parado após reiniciar."
        }
    }
}

function Disable-ScheduledTaskTracked {
    param([Parameter(Mandatory)][string]$FullName)

    $TaskPath = $FullName.Substring(0, $FullName.LastIndexOf('\') + 1)
    $TaskName = $FullName.Substring($FullName.LastIndexOf('\') + 1)

    $Task = Get-ScheduledTask -TaskPath $TaskPath -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($null -eq $Task) {
        Write-OptimizerLog -Level INFO -Message "Tarefa '$FullName' não existe neste sistema (nada a fazer)."
        return
    }

    Add-ChangeJournalEntry -Key "T|$FullName" -Entry ([ordered]@{
        Kind = 'Task'; TaskPath = $TaskPath; TaskName = $TaskName; WasEnabled = ($Task.State -ne 'Disabled')
    })

    try {
        Disable-ScheduledTask -TaskPath $TaskPath -TaskName $TaskName -ErrorAction Stop | Out-Null
    } catch {
        Write-OptimizerLog -Level AVISO -Message "Tarefa '$FullName' não pôde ser desativada ($($_.Exception.Message))."
    }
}

function Get-ActivePowerSchemeGuid {
    $Output = (& powercfg.exe /getactivescheme) -join ' '
    if ($Output -match '([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})') {
        return $Matches[1]
    }
    return $null
}

function Register-PowerStateSnapshot {
    $Hibernate = $null
    $Base = Open-BaseRegistryKey -Hive 'LocalMachine'
    try {
        $Key = $Base.OpenSubKey('SYSTEM\CurrentControlSet\Control\Power')
        if ($null -ne $Key) { $Hibernate = $Key.GetValue('HibernateEnabled'); $Key.Dispose() }
    } finally {
        $Base.Dispose()
    }

    Add-ChangeJournalEntry -Key 'P|Estado' -Entry ([ordered]@{
        Kind = 'Power'; ActiveScheme = (Get-ActivePowerSchemeGuid); HibernateEnabled = $Hibernate
    })
}

function Register-PowerSchemeCreated {
    param([Parameter(Mandatory)][string]$Guid)
    Add-ChangeJournalEntry -Key "P|Criado|$Guid" -Entry ([ordered]@{ Kind = 'PowerSchemeCreated'; Guid = $Guid })
}

#endregion

#region Reversão

function Restore-ChangeJournalEntry {
    param([Parameter(Mandatory)]$Entry)

    switch ($Entry.Kind) {
        'RegistryValue' {
            $Base = Open-BaseRegistryKey -Hive $Entry.Hive
            try {
                $Key = $Base.OpenSubKey($Entry.SubKey, $true)
                if ($null -eq $Key) {
                    if (-not $Entry.Existed) { return }
                    $Key = $Base.CreateSubKey($Entry.SubKey, $true)
                }
                try {
                    if ($Entry.Existed) {
                        $Value = ConvertFrom-JournalRegistryValue -Entry $Entry
                        $Key.SetValue([string]$Entry.Name, $Value, [Microsoft.Win32.RegistryValueKind]$Entry.ValueKind)
                    } elseif (@($Key.GetValueNames()) -contains [string]$Entry.Name) {
                        $Key.DeleteValue([string]$Entry.Name, $false)
                    }
                } finally {
                    $Key.Dispose()
                }
            } finally {
                $Base.Dispose()
            }
        }
        'RegistryKey' {
            $Base = Open-BaseRegistryKey -Hive $Entry.Hive
            try { $Base.DeleteSubKeyTree($Entry.SubKey, $false) } finally { $Base.Dispose() }
        }
        'Service' {
            $Map = @{ 2 = 'Automatic'; 3 = 'Manual'; 4 = 'Disabled' }
            if ($null -eq $Entry.Start -or -not $Map.ContainsKey([int]$Entry.Start)) { return }
            Set-Service -Name $Entry.Name -StartupType $Map[[int]$Entry.Start] -ErrorAction Stop
            if ($null -ne $Entry.DelayedAutostart) {
                $Base = Open-BaseRegistryKey -Hive 'LocalMachine'
                try {
                    $Key = $Base.OpenSubKey("SYSTEM\CurrentControlSet\Services\$($Entry.Name)", $true)
                    if ($null -ne $Key) { $Key.SetValue('DelayedAutostart', [int]$Entry.DelayedAutostart, [Microsoft.Win32.RegistryValueKind]::DWord); $Key.Dispose() }
                } finally {
                    $Base.Dispose()
                }
            }
            if ([int]$Entry.Start -eq 2) { Start-Service -Name $Entry.Name -ErrorAction SilentlyContinue }
        }
        'Task' {
            if ($Entry.WasEnabled) {
                Enable-ScheduledTask -TaskPath $Entry.TaskPath -TaskName $Entry.TaskName -ErrorAction Stop | Out-Null
            }
        }
        'Power' {
            if (-not [string]::IsNullOrWhiteSpace($Entry.ActiveScheme)) {
                & powercfg.exe /setactive $Entry.ActiveScheme | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "powercfg não conseguiu reativar o plano $($Entry.ActiveScheme)." }
            }
            if ($null -ne $Entry.HibernateEnabled -and [int]$Entry.HibernateEnabled -eq 1) {
                & powercfg.exe /hibernate on | Out-Null
            }
        }
        'PowerSchemeCreated' {
            & powercfg.exe /delete $Entry.Guid | Out-Null
        }
        default {
            throw "Tipo de entrada desconhecido no diário: '$($Entry.Kind)'."
        }
    }
}

function Get-PendingChangeJournals {
    param([Parameter(Mandatory)][string]$Directory)
    if (-not (Test-Path -LiteralPath $Directory)) { return @() }
    return @(Get-ChildItem -LiteralPath $Directory -Filter 'estado_*.json' -File |
        Where-Object { $_.Name -notlike '*.restaurado.json' } |
        Sort-Object Name -Descending)
}

function Restore-ChangeJournalFile {
    <#
    .SYNOPSIS
        Desfaz todas as alterações de um diário, na ordem inversa em que foram feitas.
    #>
    param([Parameter(Mandatory)][string]$Path)

    $Document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $Entries = @($Document.Entradas)
    [array]::Reverse($Entries)

    $Restored = 0
    $Failed = 0
    foreach ($Entry in $Entries) {
        try {
            Restore-ChangeJournalEntry -Entry $Entry
            $Restored++
        } catch {
            $Failed++
            Write-OptimizerLog -Level ERRO -Message "Falha ao reverter $($Entry.Kind) [$($Entry.SubKey)$($Entry.Name)$($Entry.TaskName)]: $($_.Exception.Message)"
        }
    }
    return [pscustomobject]@{ Restored = $Restored; Failed = $Failed }
}

#endregion
