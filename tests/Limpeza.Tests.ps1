. (Join-Path $PSScriptRoot 'Carregar-Modulos.ps1')

Describe 'Test-PathIsWithin (comparação por componentes)' {
    It 'aceita o próprio caminho e subpastas' {
        Test-PathIsWithin -Path 'C:\Temp\App' -Parent 'C:\Temp\App' | Should Be $true
        Test-PathIsWithin -Path 'C:\Temp\App\logs\x.log' -Parent 'C:\Temp\App' | Should Be $true
        Test-PathIsWithin -Path 'c:\temp\app\' -Parent 'C:\Temp\App' | Should Be $true
    }

    It 'rejeita pastas com prefixo parecido' {
        Test-PathIsWithin -Path 'C:\Temp\Aplicativo' -Parent 'C:\Temp\App' | Should Be $false
        Test-PathIsWithin -Path 'C:\Temp\App' -Parent 'C:\Temp\Ap' | Should Be $false
    }

    It 'resolve ".." antes de comparar' {
        Test-PathIsWithin -Path 'C:\Temp\App\..\Outro' -Parent 'C:\Temp\App' | Should Be $false
        Test-PathIsWithin -Path 'C:\Temp\Outro\..\App\x' -Parent 'C:\Temp\App' | Should Be $true
    }
}

Describe 'Clear-SystemTempFiles' {
    It 'apaga temporários e preserva a pasta do programa e a de logs' {
        $Temp = Join-Path $TestDrive 'temp'
        $AppDir = Join-Path $Temp 'Otimizador-Windows-Package\app'
        $LogDir = Join-Path $Temp 'logs-otimizador'
        New-Item -ItemType Directory -Path $AppDir, $LogDir, (Join-Path $Temp 'Lixo') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $Temp 'a.tmp') -Value 'x'
        Set-Content -LiteralPath (Join-Path $Temp 'Lixo\b.tmp') -Value 'x'
        Set-Content -LiteralPath (Join-Path $AppDir 'Run.ps1') -Value 'x'
        Set-Content -LiteralPath (Join-Path $LogDir 'sessao.log') -Value 'x'

        Clear-SystemTempFiles -ProtectedPaths @($AppDir, $LogDir) -TempRoots @($Temp)

        Test-Path -LiteralPath (Join-Path $Temp 'a.tmp') | Should Be $false
        Test-Path -LiteralPath (Join-Path $Temp 'Lixo') | Should Be $false
        Test-Path -LiteralPath (Join-Path $AppDir 'Run.ps1') | Should Be $true
        Test-Path -LiteralPath (Join-Path $LogDir 'sessao.log') | Should Be $true
    }
}
