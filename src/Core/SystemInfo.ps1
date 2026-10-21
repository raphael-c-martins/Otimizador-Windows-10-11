<#
.SYNOPSIS
    Módulo de Detecção do Ambiente (privilégio, versão do Windows, tipo de equipamento e usuário conectado).
.DESCRIPTION
    Concentra as consultas ao sistema usadas pelo orquestrador para decidir quais ajustes são seguros
    (ex.: não desligar a hibernação em notebooks) e em qual perfil de usuário aplicar ajustes de HKCU.
#>

function Test-IsAdministrator {
    $Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$Identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-OSInfo {
    $Caption = 'Windows'
    $Build = [Environment]::OSVersion.Version.Build
    try {
        $OSObj = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
        $Caption = $OSObj.Caption
        $Build = [int]$OSObj.BuildNumber
    } catch {}

    return [pscustomobject]@{
        Caption = $Caption
        Build   = $Build
        IsWin11 = ($Build -ge 22000)
    }
}

function Test-IsPortableDevice {
    <#
    .SYNOPSIS
        Indica se o equipamento é um notebook/tablet (possui bateria ou chassi portátil).
    #>
    try {
        if (@(Get-CimInstance Win32_Battery -ErrorAction Stop).Count -gt 0) { return $true }

        # Tipos de chassi portáteis definidos pelo padrão SMBIOS (Notebook, Laptop, Tablet, Conversível etc.)
        $PortableChassis = @(8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32)
        $Chassis = @(Get-CimInstance Win32_SystemEnclosure -ErrorAction Stop | ForEach-Object { $_.ChassisTypes })
        return (@($Chassis | Where-Object { $PortableChassis -contains $_ }).Count -gt 0)
    } catch {
        return $false
    }
}

function Get-InteractiveUserSid {
    <#
    .SYNOPSIS
        Retorna o SID do usuário conectado à sessão (dono da área de trabalho), ou $null se não identificado.
    #>
    try {
        $UserName = (Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).UserName
        if (-not [string]::IsNullOrWhiteSpace($UserName)) {
            return ([Security.Principal.NTAccount]$UserName).Translate([Security.Principal.SecurityIdentifier]).Value
        }
    } catch {}

    # Sessões remotas (RDP) não preenchem Win32_ComputerSystem.UserName: usa o dono do Explorer da sessão atual.
    try {
        $SessionId = (Get-Process -Id $PID).SessionId
        $Explorer = Get-CimInstance Win32_Process -Filter "Name='explorer.exe' AND SessionId=$SessionId" -ErrorAction Stop | Select-Object -First 1
        if ($null -ne $Explorer) {
            $Owner = Invoke-CimMethod -InputObject $Explorer -MethodName GetOwnerSid -ErrorAction Stop
            if (-not [string]::IsNullOrWhiteSpace($Owner.Sid)) { return $Owner.Sid }
        }
    } catch {}

    return $null
}

function Resolve-UserRegistrySid {
    <#
    .SYNOPSIS
        Decide em qual perfil gravar os ajustes de HKCU.
    .DESCRIPTION
        Quando o usuário eleva o programa com OUTRA conta de administrador (comum em empresas), o HKCU do
        processo pertence ao administrador. Neste caso os ajustes são redirecionados para o perfil do usuário
        conectado (HKEY_USERS\<SID>). Retorna $null quando o HKCU do processo já é o perfil correto.
    #>
    $CurrentSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $InteractiveSid = Get-InteractiveUserSid

    if ([string]::IsNullOrWhiteSpace($InteractiveSid)) {
        return [pscustomobject]@{ Sid = $null; Message = 'Usuário conectado não identificado: ajustes de usuário serão aplicados ao perfil que executa o programa.'; IsWarning = $true }
    }
    if ($InteractiveSid -eq $CurrentSid) {
        return [pscustomobject]@{ Sid = $null; Message = 'Ajustes de usuário aplicados ao perfil atual.'; IsWarning = $false }
    }
    if (Test-Path -LiteralPath "Registry::HKEY_USERS\$InteractiveSid") {
        return [pscustomobject]@{ Sid = $InteractiveSid; Message = "Programa elevado com outra conta: ajustes de usuário serão aplicados ao usuário conectado ($InteractiveSid)."; IsWarning = $false }
    }
    return [pscustomobject]@{ Sid = $null; Message = "Perfil do usuário conectado ($InteractiveSid) não está carregado: ajustes de usuário irão para o perfil que executa o programa."; IsWarning = $true }
}
