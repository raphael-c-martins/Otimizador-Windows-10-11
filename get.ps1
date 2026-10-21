# Bootstrap web do Otimizador de Windows: irm "https://raw.githubusercontent.com/raphael-c-martins/Otimizador-Windows-10-11/main/get.ps1" | iex
# Mantenha este arquivo somente com caracteres ASCII (sem acentos e sem BOM): ele e executado via Invoke-Expression.

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$Repo = 'raphael-c-martins/Otimizador-Windows-10-11'
$AssetName = 'Otimizador-Windows.zip'

# 1. Auto-elevacao de Administrador ("return" em vez de "exit" para nao fechar o terminal do usuario)
$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $IsAdmin) {
    Write-Host ' [!] Solicitando permissao de Administrador...' -ForegroundColor Cyan
    $BootstrapCommand = "irm 'https://raw.githubusercontent.com/$Repo/main/get.ps1' | iex"
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"$BootstrapCommand`"" -Verb RunAs
    return
}

# 2. Pasta protegida: somente Administradores e SYSTEM escrevem (evita adulteracao antes da execucao)
$DataRoot = Join-Path $env:ProgramData 'OtimizadorWindows'
$AppDir = Join-Path $DataRoot 'app'
$DownloadDir = Join-Path $DataRoot 'download'
$PreviousProgress = $ProgressPreference
$ProgressPreference = 'SilentlyContinue'

try {
    if (-not (Test-Path -LiteralPath $DataRoot)) { New-Item -ItemType Directory -Path $DataRoot -Force | Out-Null }
    $null = & icacls.exe $DataRoot /setowner '*S-1-5-32-544' /T /C /Q
    $null = & icacls.exe $DataRoot /reset /T /C /Q
    $null = & icacls.exe $DataRoot /inheritance:r /grant:r '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' /C /Q
    if ($LASTEXITCODE -ne 0) { throw "Nao foi possivel proteger a pasta '$DataRoot'." }

    foreach ($Dir in @($AppDir, $DownloadDir)) {
        if (Test-Path -LiteralPath $Dir) { Remove-Item -LiteralPath $Dir -Recurse -Force -ErrorAction Stop }
        New-Item -ItemType Directory -Path $Dir -Force | Out-Null
    }
    $ZipPath = Join-Path $DownloadDir $AssetName

    # 3. Download da ultima versao publicada, com verificacao SHA256
    $Headers = @{ 'User-Agent' = 'Otimizador-Windows-Bootstrap' }
    $Release = $null
    try { $Release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers $Headers -ErrorAction Stop } catch {}

    $ZipAsset = $null
    $HashAsset = $null
    if ($null -ne $Release) {
        $ZipAsset = $Release.assets | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
        $HashAsset = $Release.assets | Where-Object { $_.name -eq "$AssetName.sha256" } | Select-Object -First 1
    }

    if ($null -ne $ZipAsset -and $null -ne $HashAsset) {
        Write-Host "`n [..] Baixando o Otimizador de Windows $($Release.tag_name)..." -ForegroundColor Cyan
        $HashPath = Join-Path $DownloadDir "$AssetName.sha256"
        Invoke-WebRequest -Uri $ZipAsset.browser_download_url -OutFile $ZipPath -UseBasicParsing -Headers $Headers -ErrorAction Stop
        Invoke-WebRequest -Uri $HashAsset.browser_download_url -OutFile $HashPath -UseBasicParsing -Headers $Headers -ErrorAction Stop

        $Expected = ((Get-Content -LiteralPath $HashPath -Raw).Trim() -split '\s+')[0]
        $Actual = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash
        if ($Expected -ne $Actual) {
            throw "Falha na verificacao de integridade (SHA256 esperado $Expected, obtido $Actual). O pacote foi descartado."
        }
        Write-Host ' [ OK ] Integridade do pacote verificada (SHA256).' -ForegroundColor Green
    } else {
        Write-Host ' [AVISO] Nenhuma versao publicada encontrada: usando o branch main SEM verificacao de integridade.' -ForegroundColor Yellow
        Invoke-WebRequest -Uri "https://github.com/$Repo/archive/refs/heads/main.zip" -OutFile $ZipPath -UseBasicParsing -Headers $Headers -ErrorAction Stop
    }

    Expand-Archive -LiteralPath $ZipPath -DestinationPath $AppDir -Force
    Remove-Item -LiteralPath $DownloadDir -Recurse -Force -ErrorAction SilentlyContinue

    $RunScript = Get-ChildItem -LiteralPath $AppDir -Filter 'Run.ps1' -Recurse -File | Sort-Object { $_.FullName.Length } | Select-Object -First 1
    if ($null -eq $RunScript) { throw 'Run.ps1 nao encontrado no pacote baixado.' }

    $ProgressPreference = $PreviousProgress
    Write-Host ' [ OK ] Pacote carregado. Iniciando o Otimizador...' -ForegroundColor Green
    & $RunScript.FullName
} catch {
    $ProgressPreference = $PreviousProgress
    Write-Host " [ERRO] Falha ao baixar e iniciar o Otimizador: $($_.Exception.Message)" -ForegroundColor Red
}
