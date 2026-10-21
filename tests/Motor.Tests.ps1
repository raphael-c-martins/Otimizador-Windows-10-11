. (Join-Path $PSScriptRoot 'Carregar-Modulos.ps1')

# Funções falsas usadas como "ajustes" de teste (não tocam no sistema)
function global:Test-AjusteQueFalha { throw 'falha simulada' }
function global:Test-AjusteOk { }
function global:Test-AjusteComAviso { Write-OptimizerLog -Level AVISO -Message 'aviso simulado' }

Describe 'Validação de perfis e seleções' {
    $Catalog = Get-OptimizerCatalog -ScriptDir $RepoRoot

    It 'rejeita perfil inexistente' {
        { Import-OptimizerPreset -Catalog $Catalog -NameOrPath 'NaoExiste' } | Should Throw
    }

    It 'rejeita nome de perfil com caminho relativo' {
        { Import-OptimizerPreset -Catalog $Catalog -NameOrPath '..\..\Config\Apps' } | Should Throw
    }

    It 'carrega perfil personalizado a partir de arquivo .json' {
        $Path = Join-Path $TestDrive 'MeuPerfil.json'
        '{ "PresetName": "Meu", "DisplayName": "Meu Perfil", "RemoveApps": ["BingNews"], "PreserveApps": [], "ApplyTweaks": ["ShowFileExtensions"] }' |
            Set-Content -LiteralPath $Path -Encoding UTF8
        (Import-OptimizerPreset -Catalog $Catalog -NameOrPath $Path).DisplayName | Should Be 'Meu Perfil'
    }

    It 'rejeita perfil .json com ajuste inexistente' {
        $Path = Join-Path $TestDrive 'Invalido.json'
        '{ "DisplayName": "Invalido", "RemoveApps": [], "ApplyTweaks": ["AjusteQueNaoExiste"] }' |
            Set-Content -LiteralPath $Path -Encoding UTF8
        { Import-OptimizerPreset -Catalog $Catalog -NameOrPath $Path } | Should Throw
    }

    It 'rejeita IDs desconhecidos antes de executar qualquer etapa' {
        { Invoke-OptimizationPlan -Catalog $Catalog -AppIds @('AppQueNaoExiste') -DryRun } | Should Throw
        { Invoke-OptimizationPlan -Catalog $Catalog -TweakIds @('AjusteQueNaoExiste') -DryRun } | Should Throw
    }
}

Describe 'Simulação (-DryRun)' {
    $Catalog = Get-OptimizerCatalog -ScriptDir $RepoRoot

    It 'não executa nada, não cria diário e conta as etapas' {
        $Result = Invoke-OptimizationPlan -Catalog $Catalog -AppIds @('BingNews') -TweakIds @('ShowFileExtensions') -DryRun
        $Result.Simulated | Should Be 3
        $Result.Failed | Should Be 0
        $Result.JournalPath | Should BeNullOrEmpty
    }

    It 'inclui a correção de protocolos ao remover o Xbox Game Bar' {
        $Result = Invoke-OptimizationPlan -Catalog $Catalog -AppIds @('XboxGamingOverlay') -DryRun
        $FixName = ($Catalog.Tweaks | Where-Object Id -eq 'FixGameBarProtocols').Name
        (@($Result.Steps.Name) -contains $FixName) | Should Be $true
    }
}

Describe 'Execução com falhas isoladas' {
    $FakeCatalog = [pscustomobject]@{
        ScriptDir  = $RepoRoot
        Apps       = @()
        Categories = @([pscustomobject]@{ Id = 'Teste'; Name = 'Teste' })
        Tweaks     = @(
            [pscustomobject]@{ Id = 'Falha'; Function = 'Test-AjusteQueFalha'; Name = 'Ajuste que falha'; Category = 'Teste' },
            [pscustomobject]@{ Id = 'Ok'; Function = 'Test-AjusteOk'; Name = 'Ajuste ok'; Category = 'Teste' },
            [pscustomobject]@{ Id = 'Aviso'; Function = 'Test-AjusteComAviso'; Name = 'Ajuste com aviso'; Category = 'Teste' }
        )
    }
    $Context = @{ JournalDirectory = (Join-Path $TestDrive 'estado'); RestartExplorer = $false }

    It 'reporta a falha sem interromper as demais etapas' {
        $Result = Invoke-OptimizationPlan -Catalog $FakeCatalog -TweakIds @('Falha', 'Ok', 'Aviso') -Context $Context
        $Result.Steps.Count | Should Be 3
        $Result.Failed | Should Be 1
        $Result.Ok | Should Be 1
        $Result.Warnings | Should Be 1
        ($Result.Steps | Where-Object Name -eq 'Ajuste que falha').Detail | Should Be 'falha simulada'
    }

    It 'rejeita ajuste que aponta para função inexistente' {
        $Broken = [pscustomobject]@{
            ScriptDir = $RepoRoot; Apps = @(); Categories = @()
            Tweaks = @([pscustomobject]@{ Id = 'X'; Function = 'Funcao-QueNaoExiste'; Name = 'X'; Category = 'Teste' })
        }
        { Invoke-OptimizationPlan -Catalog $Broken -TweakIds @('X') -Context $Context } | Should Throw
    }
}
