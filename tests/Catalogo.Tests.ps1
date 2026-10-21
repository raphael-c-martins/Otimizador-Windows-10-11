. (Join-Path $PSScriptRoot 'Carregar-Modulos.ps1')

Describe 'Catálogos de aplicativos e ajustes' {
    $Catalog = Get-OptimizerCatalog -ScriptDir $RepoRoot

    It 'carrega aplicativos, ajustes e categorias' {
        $Catalog.Apps.Count | Should BeGreaterThan 0
        $Catalog.Tweaks.Count | Should BeGreaterThan 0
        $Catalog.Categories.Count | Should BeGreaterThan 0
    }

    It 'não possui IDs duplicados' {
        @($Catalog.Apps.Id | Group-Object | Where-Object Count -gt 1).Count | Should Be 0
        @($Catalog.Tweaks.Id | Group-Object | Where-Object Count -gt 1).Count | Should Be 0
    }

    It 'cada ajuste aponta para uma função existente' {
        foreach ($Tweak in $Catalog.Tweaks) {
            Get-Command -Name $Tweak.Function -CommandType Function -ErrorAction SilentlyContinue | Should Not BeNullOrEmpty
        }
    }

    It 'cada ajuste usa uma categoria declarada' {
        $CategoryIds = @($Catalog.Categories.Id)
        @($Catalog.Tweaks | Where-Object { $CategoryIds -notcontains $_.Category }).Count | Should Be 0
    }

    It 'RequiresTweaks dos aplicativos referencia ajustes existentes' {
        foreach ($App in $Catalog.Apps) {
            foreach ($Required in @($App.RequiresTweaks)) {
                if ($Required) { (@($Catalog.Tweaks.Id) -contains $Required) | Should Be $true }
            }
        }
    }

    It 'o Teams de trabalho (MSTeams) e o pessoal (MicrosoftTeams) usam nomes exatos e distintos' {
        ($Catalog.Apps | Where-Object Id -eq 'MicrosoftTeams').PackageName | Should Be 'MSTeams'
        ($Catalog.Apps | Where-Object Id -eq 'MSTeamsLegacy').PackageName | Should Be 'MicrosoftTeams'
        ('MicrosoftTeams' -like (ConvertTo-PackageWildcard -Pattern 'MSTeams')) | Should Be $false
        ('MSTeams' -like (ConvertTo-PackageWildcard -Pattern 'MicrosoftTeams')) | Should Be $false
    }
}

Describe 'Perfis (Config/Presets)' {
    $Catalog = Get-OptimizerCatalog -ScriptDir $RepoRoot

    foreach ($Name in (Get-OptimizerPresetNames -ScriptDir $RepoRoot)) {
        It "perfil '$Name' é válido" {
            $Preset = Get-Content -LiteralPath (Join-Path $RepoRoot "Config\Presets\$Name.json") -Raw -Encoding UTF8 | ConvertFrom-Json
            (Test-OptimizerPreset -Catalog $Catalog -Preset $Preset).Count | Should Be 0
        }

        It "perfil '$Name' não remove o Teams de trabalho (pacote MSTeams)" {
            $Preset = Import-OptimizerPreset -Catalog $Catalog -NameOrPath $Name
            $Removed = @($Catalog.Apps | Where-Object { @($Preset.RemoveApps) -contains $_.Id })
            foreach ($App in $Removed) {
                foreach ($Pattern in ($App.PackageName -split ',')) {
                    ('MSTeams' -like (ConvertTo-PackageWildcard -Pattern $Pattern)) | Should Be $false
                }
            }
        }
    }

    It 'o perfil Corporativo preserva o OneDrive e os demais o removem' {
        foreach ($Name in (Get-OptimizerPresetNames -ScriptDir $RepoRoot)) {
            $Preset = Import-OptimizerPreset -Catalog $Catalog -NameOrPath $Name
            (@($Preset.ApplyTweaks) -contains 'RemoveOneDrive') | Should Be ($Name -ne 'Corporativo')
        }
    }
}
