<#
.SYNOPSIS
    Motor Único de Execução das Otimizações (usado pela GUI e pelo terminal).
.DESCRIPTION
    Recebe a lista de aplicativos e de ajustes (IDs dos catálogos em /Config) e executa cada item como
    uma etapa isolada: a falha de uma etapa não interrompe as demais e todas são contabilizadas no resultado.
    Cada ajuste do Tweaks.json indica no campo "Function" qual função o executa.
#>

#region Catálogos e Perfis

function Get-OptimizerCatalog {
    param([Parameter(Mandatory)][string]$ScriptDir)

    $AppsPath = Join-Path $ScriptDir 'Config\Apps.json'
    $TweaksPath = Join-Path $ScriptDir 'Config\Tweaks.json'
    foreach ($Path in @($AppsPath, $TweaksPath)) {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Catálogo não encontrado: '$Path'." }
    }

    $AppsDoc = Get-Content -LiteralPath $AppsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $TweaksDoc = Get-Content -LiteralPath $TweaksPath -Raw -Encoding UTF8 | ConvertFrom-Json

    return [pscustomobject]@{
        ScriptDir  = $ScriptDir
        Apps       = @($AppsDoc.Apps)
        Tweaks     = @($TweaksDoc.Tweaks)
        Categories = @($TweaksDoc.Categories)
    }
}

function Get-OptimizerPresetNames {
    param([Parameter(Mandatory)][string]$ScriptDir)
    $PresetDir = Join-Path $ScriptDir 'Config\Presets'
    return @(Get-ChildItem -LiteralPath $PresetDir -Filter '*.json' -File | Sort-Object Name | ForEach-Object { $_.BaseName })
}

function Test-OptimizerPreset {
    <#
    .SYNOPSIS
        Retorna a lista de problemas do perfil (vazia quando o perfil é válido).
    #>
    param(
        [Parameter(Mandatory)]$Catalog,
        [Parameter(Mandatory)]$Preset
    )
    $Problems = [System.Collections.Generic.List[string]]::new()
    $AppIds = @($Catalog.Apps.Id)
    $TweakIds = @($Catalog.Tweaks.Id)

    if ([string]::IsNullOrWhiteSpace($Preset.DisplayName)) { $Problems.Add('Campo "DisplayName" ausente.') }
    foreach ($Id in @($Preset.RemoveApps) + @($Preset.PreserveApps)) {
        if ($null -ne $Id -and $AppIds -notcontains $Id) { $Problems.Add("Aplicativo desconhecido: '$Id'.") }
    }
    foreach ($Id in @($Preset.ApplyTweaks)) {
        if ($null -ne $Id -and $TweakIds -notcontains $Id) { $Problems.Add("Ajuste desconhecido: '$Id'.") }
    }
    foreach ($Id in @($Preset.RemoveApps)) {
        if (@($Preset.PreserveApps) -contains $Id) { $Problems.Add("Aplicativo '$Id' está ao mesmo tempo em RemoveApps e PreserveApps.") }
    }
    return , $Problems.ToArray()
}

function Import-OptimizerPreset {
    <#
    .SYNOPSIS
        Carrega um perfil pelo nome (Config/Presets) ou por caminho de arquivo .json, validando seu conteúdo.
    #>
    param(
        [Parameter(Mandatory)]$Catalog,
        [Parameter(Mandatory)][string]$NameOrPath
    )

    if ($NameOrPath -like '*.json') {
        if (-not (Test-Path -LiteralPath $NameOrPath -PathType Leaf)) { throw "Arquivo de perfil não encontrado: '$NameOrPath'." }
        $Path = (Resolve-Path -LiteralPath $NameOrPath).ProviderPath
    } else {
        # Aceita somente nomes existentes na pasta de perfis (impede caminhos como "..\..\arquivo")
        $Names = Get-OptimizerPresetNames -ScriptDir $Catalog.ScriptDir
        $Match = $Names | Where-Object { $_ -ieq $NameOrPath } | Select-Object -First 1
        if (-not $Match) { throw "Perfil '$NameOrPath' inexistente. Perfis disponíveis: $($Names -join ', ')." }
        $Path = Join-Path $Catalog.ScriptDir "Config\Presets\$Match.json"
    }

    try {
        $Preset = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        throw "Perfil '$Path' não é um JSON válido: $($_.Exception.Message)"
    }

    $Problems = Test-OptimizerPreset -Catalog $Catalog -Preset $Preset
    if ($Problems.Count -gt 0) {
        throw "Perfil '$Path' inválido: $($Problems -join ' ')"
    }
    return $Preset
}

function Assert-OptimizerSelection {
    param(
        [Parameter(Mandatory)]$Catalog,
        [string[]]$AppIds = @(),
        [string[]]$TweakIds = @()
    )
    $UnknownApps = @($AppIds | Where-Object { @($Catalog.Apps.Id) -notcontains $_ })
    $UnknownTweaks = @($TweakIds | Where-Object { @($Catalog.Tweaks.Id) -notcontains $_ })
    if ($UnknownApps.Count -gt 0) { throw "Aplicativos desconhecidos: $($UnknownApps -join ', ')." }
    if ($UnknownTweaks.Count -gt 0) { throw "Ajustes desconhecidos: $($UnknownTweaks -join ', ')." }

    foreach ($Tweak in $Catalog.Tweaks | Where-Object { $TweakIds -contains $_.Id }) {
        if ($null -eq (Get-Command -Name $Tweak.Function -CommandType Function -ErrorAction SilentlyContinue)) {
            throw "O ajuste '$($Tweak.Id)' aponta para a função '$($Tweak.Function)', que não existe."
        }
    }
}

#endregion

#region Execução

function Get-TweakArguments {
    <#
    .SYNOPSIS
        Monta os parâmetros da função do ajuste a partir do contexto (somente os que ela declara).
    #>
    param(
        [Parameter(Mandatory)][string]$FunctionName,
        [hashtable]$Context = @{}
    )
    $Command = Get-Command -Name $FunctionName -CommandType Function
    $Splat = @{}
    foreach ($Key in $Context.Keys) {
        if ($Command.Parameters.ContainsKey($Key)) { $Splat[$Key] = $Context[$Key] }
    }
    return $Splat
}

function Invoke-OptimizerStep {
    param(
        [Parameter(Mandatory)][string]$StepName,
        [Parameter(Mandatory)][scriptblock]$StepAction,
        [switch]$DryRun
    )

    $Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $StepDetail = ''
    Write-OptimizerLog -Level ETAPA -Message $StepName

    if ($DryRun) {
        $StepStatus = 'SIMULADO'
        Write-OptimizerLog -Level INFO -Message '[SIMULAÇÃO] Nenhuma alteração aplicada.'
    } else {
        Reset-StepWarningCount
        try {
            $null = & $StepAction
            $StepStatus = if ((Get-StepWarningCount) -gt 0) { 'AVISO' } else { 'OK' }
        } catch {
            $StepStatus = 'ERRO'
            $StepDetail = $_.Exception.Message
            Write-OptimizerLog -Level ERRO -Message "$StepName`: $StepDetail"
        }
    }

    $Stopwatch.Stop()
    if ($StepStatus -eq 'OK') { Write-OptimizerLog -Level OK -Message "$StepName (concluído)" }

    return [pscustomobject]@{
        Name     = $StepName
        Status   = $StepStatus
        Detail   = $StepDetail
        Duration = [math]::Round($Stopwatch.Elapsed.TotalSeconds, 1)
    }
}

function Add-StepResult {
    param($Result, $Step)
    $Result.Steps.Add($Step)
    switch ($Step.Status) {
        'OK'       { $Result.Ok++ }
        'AVISO'    { $Result.Warnings++ }
        'ERRO'     { $Result.Failed++ }
        'SIMULADO' { $Result.Simulated++ }
    }
}

function Invoke-OptimizationPlan {
    <#
    .SYNOPSIS
        Executa a remoção de aplicativos e os ajustes selecionados.
    .PARAMETER Context
        Hashtable com: IsWin11, IsPortable, ProtectedPaths, StateDirectory, JournalDirectory, UserSid e
        RestartExplorer (padrão: verdadeiro).
    #>
    param(
        [Parameter(Mandatory)]$Catalog,
        [string[]]$AppIds = @(),
        [string[]]$TweakIds = @(),
        [hashtable]$Context = @{},
        [switch]$DryRun
    )

    $AppIds = @($AppIds | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $TweakIds = @($TweakIds | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    Assert-OptimizerSelection -Catalog $Catalog -AppIds $AppIds -TweakIds $TweakIds

    # Ajustes exigidos por aplicativos removidos (ex.: sem o Game Bar, os protocolos ms-gamebar geram popups)
    foreach ($App in $Catalog.Apps | Where-Object { $AppIds -contains $_.Id }) {
        foreach ($Required in @($App.RequiresTweaks)) {
            if ($Required -and $TweakIds -notcontains $Required) {
                $TweakIds += $Required
                Write-OptimizerLog -Level INFO -Message "Ajuste '$Required' incluído automaticamente porque '$($App.FriendlyName)' será removido."
            }
        }
    }
    Assert-OptimizerSelection -Catalog $Catalog -AppIds $AppIds -TweakIds $TweakIds

    # A ordem segue o catálogo (e não a ordem recebida), garantindo execução previsível
    $Apps = @($Catalog.Apps | Where-Object { $AppIds -contains $_.Id })
    $Tweaks = @($Catalog.Tweaks | Where-Object { $TweakIds -contains $_.Id })

    $Result = [pscustomobject]@{
        Steps       = [System.Collections.Generic.List[object]]::new()
        Ok          = 0
        Warnings    = 0
        Failed      = 0
        Simulated   = 0
        DryRun      = [bool]$DryRun
        JournalPath = $null
    }

    if ($Apps.Count -eq 0 -and $Tweaks.Count -eq 0) {
        Write-OptimizerLog -Level AVISO -Message 'Nenhum aplicativo ou ajuste selecionado: nada a executar.'
        return $Result
    }

    $RestartExplorer = ($Context.RestartExplorer -ne $false)
    $Total = $Apps.Count + $Tweaks.Count + $(if ($RestartExplorer) { 1 } else { 0 })
    $Index = 0
    Write-OptimizerLog -Level INFO -Message "Plano: $($Apps.Count) aplicativo(s) e $($Tweaks.Count) ajuste(s)$(if ($DryRun) { ' [SIMULAÇÃO]' })."

    if (-not $DryRun) {
        $Result.JournalPath = Start-ChangeJournal -Directory $Context.JournalDirectory
        Set-UserRegistryTarget -Sid $Context.UserSid
    }

    try {
        # 1. Remoção de aplicativos
        $Cache = @()
        if ($Apps.Count -gt 0 -and -not $DryRun) { $Cache = Get-ProvisionedPackageCache }
        foreach ($App in $Apps) {
            $Index++
            Write-OptimizerProgress -Current $Index -Total $Total -Activity "Removendo: $($App.FriendlyName)"
            $Step = Invoke-OptimizerStep -StepName "Remover aplicativo: $($App.FriendlyName)" -DryRun:$DryRun -StepAction {
                Remove-AppByDefinition -AppInfo $App -CachedProvisioned $Cache
            }
            Add-StepResult -Result $Result -Step $Step
        }

        # 2. Ajustes (um por função, com parâmetros vindos do contexto)
        foreach ($Tweak in $Tweaks) {
            $Index++
            Write-OptimizerProgress -Current $Index -Total $Total -Activity $Tweak.Name
            $TweakFunction = $Tweak.Function
            $TweakSplat = Get-TweakArguments -FunctionName $TweakFunction -Context $Context
            $Step = Invoke-OptimizerStep -StepName $Tweak.Name -DryRun:$DryRun -StepAction {
                & $TweakFunction @TweakSplat
            }
            Add-StepResult -Result $Result -Step $Step
            if (-not $DryRun) { Save-ChangeJournal }
        }

        # 3. Explorer reiniciado para aplicar ajustes de interface
        if ($RestartExplorer) {
            $Index++
            Write-OptimizerProgress -Current $Index -Total $Total -Activity 'Reiniciando o Windows Explorer'
            $Step = Invoke-OptimizerStep -StepName 'Reiniciar o Windows Explorer' -DryRun:$DryRun -StepAction { Restart-WindowsExplorer }
            Add-StepResult -Result $Result -Step $Step
        }
    } finally {
        if (-not $DryRun) {
            Stop-ChangeJournal
            Set-UserRegistryTarget -Sid $null
        }
    }

    $Summary = "Resumo: $($Result.Ok) OK, $($Result.Warnings) com aviso, $($Result.Failed) com erro"
    if ($DryRun) { $Summary = "Resumo da simulação: $($Result.Simulated) etapas seriam executadas" }
    $SummaryLevel = if ($Result.Failed -gt 0) { 'AVISO' } else { 'OK' }
    Write-OptimizerLog -Level $SummaryLevel -Message "$Summary."
    if ($Result.JournalPath) { Write-OptimizerLog -Level INFO -Message "Diário para reversão (Undo): $($Result.JournalPath)" }

    return $Result
}

#endregion
