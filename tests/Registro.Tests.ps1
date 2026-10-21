. (Join-Path $PSScriptRoot 'Carregar-Modulos.ps1')

# Os testes usam uma chave temporária própria em HKCU, removida ao final (nunca tocam em configurações reais).
Describe 'Diário de alterações do registro' {
    $TestRoot = "HKCU:\Software\OtimizadorWindows-Testes-$([guid]::NewGuid().ToString('N'))"
    $JournalDir = Join-Path $TestDrive 'estado'

    try {
        It 'registra a criação de chave e a remove ao reverter' {
            $Journal = Start-ChangeJournal -Directory $JournalDir
            Set-RegistryValue -Path "$TestRoot\Sub" -Name 'Valor' -Value 1
            (Get-ItemProperty -LiteralPath "$TestRoot\Sub").Valor | Should Be 1
            Stop-ChangeJournal

            (Restore-ChangeJournalFile -Path $Journal).Failed | Should Be 0
            Test-Path -LiteralPath $TestRoot | Should Be $false
        }

        It 'devolve os valores originais e remove valores que não existiam' {
            New-Item -Path $TestRoot -Force | Out-Null
            New-ItemProperty -Path $TestRoot -Name 'Texto' -Value 'original' -PropertyType String -Force | Out-Null
            New-ItemProperty -Path $TestRoot -Name 'Numero' -Value 7 -PropertyType DWord -Force | Out-Null

            $Journal = Start-ChangeJournal -Directory $JournalDir
            Set-RegistryValue -Path $TestRoot -Name 'Texto' -Value 'alterado' -Type String
            Set-RegistryValue -Path $TestRoot -Name 'Texto' -Value 'alterado-de-novo' -Type String
            Set-RegistryValue -Path $TestRoot -Name 'Numero' -Value 0
            Set-RegistryValue -Path $TestRoot -Name 'Novo' -Value 1
            Stop-ChangeJournal

            (Restore-ChangeJournalFile -Path $Journal).Failed | Should Be 0
            $Props = Get-ItemProperty -LiteralPath $TestRoot
            $Props.Texto | Should Be 'original'
            $Props.Numero | Should Be 7
            $Props.PSObject.Properties['Novo'] | Should BeNullOrEmpty
        }

        It 'o Undo reverte várias execuções da mais nova para a mais antiga' {
            $UndoDir = Join-Path $TestDrive 'estado-undo'
            New-Item -Path $TestRoot -Force | Out-Null
            New-ItemProperty -Path $TestRoot -Name 'Nivel' -Value 7 -PropertyType DWord -Force | Out-Null

            Start-ChangeJournal -Directory $UndoDir | Out-Null
            Set-RegistryValue -Path $TestRoot -Name 'Nivel' -Value 1
            Stop-ChangeJournal
            Start-Sleep -Milliseconds 20
            Start-ChangeJournal -Directory $UndoDir | Out-Null
            Set-RegistryValue -Path $TestRoot -Name 'Nivel' -Value 2
            Stop-ChangeJournal

            $Result = Invoke-SystemUndo -JournalDirectory $UndoDir -SkipExplorerRestart
            $Result.Mode | Should Be 'Diario'
            $Result.Failed | Should Be 0
            (Get-ItemProperty -LiteralPath $TestRoot).Nivel | Should Be 7
            @(Get-PendingChangeJournals -Directory $UndoDir).Count | Should Be 0
            @(Get-ChildItem -LiteralPath $UndoDir -Filter '*.restaurado.json').Count | Should Be 2
        }

        It 'redireciona HKCU para o perfil do usuário conectado quando indicado' {
            $Sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
            Set-UserRegistryTarget -Sid $Sid
            try {
                $Location = ConvertTo-RegistryLocation -Path 'HKCU:\Software\Teste'
                $Location.Hive | Should Be 'Users'
                $Location.SubKey | Should Be "$Sid\Software\Teste"
            } finally {
                Set-UserRegistryTarget -Sid $null
            }
            (ConvertTo-RegistryLocation -Path 'HKCU:\Software\Teste').Hive | Should Be 'CurrentUser'
        }

        It 'rejeita caminhos de registro fora do padrão' {
            { ConvertTo-RegistryLocation -Path 'C:\Windows' } | Should Throw
        }
    } finally {
        Remove-Item -LiteralPath $TestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
