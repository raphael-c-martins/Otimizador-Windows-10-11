# ==============================================================================
# OTIMIZADOR DE WINDOWS (10 & 11) - BOOTSTRAP ONE-LINER
# ==============================================================================

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    [Console]::InputEncoding  = [System.Text.Encoding]::UTF8
    $OutputEncoding           = [System.Text.Encoding]::UTF8
} catch {}

# 1. Verificar privilégios de Administrador e auto-elevar se necessário
$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $IsAdmin) {
    Write-Host " [!] Solicitando permissão de Administrador..." -ForegroundColor Cyan
    $BootstrapCommand = "irm 'https://raw.githubusercontent.com/raphael-c-martins/Otimizador-Windows-10-11/main/get.ps1' | iex"
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"$BootstrapCommand`"" -Verb RunAs
    exit
}

# 2. Configurar TLS 1.2
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# 3. Baixar e extrair o repositório oficial do GitHub em pasta temporária
Write-Host "`n [..] Baixando suíte Otimizador de Windows do GitHub..." -ForegroundColor Cyan
$ZipUrl = "https://github.com/raphael-c-martins/Otimizador-Windows-10-11/archive/refs/heads/main.zip"
$TempDir = Join-Path $env:TEMP "Otimizador-Windows-Package"
$TempZip = Join-Path $env:TEMP "Otimizador-Windows.zip"

try {
    if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $TempZip) { Remove-Item $TempZip -Force -ErrorAction SilentlyContinue }

    Invoke-RestMethod -Uri $ZipUrl -OutFile $TempZip
    Expand-Archive -Path $TempZip -DestinationPath $TempDir -Force
    Remove-Item $TempZip -Force -ErrorAction SilentlyContinue

    $ExtractedRoot = (Get-ChildItem -Path $TempDir -Directory | Select-Object -First 1).FullName
    $RunScript = Join-Path $ExtractedRoot "Run.ps1"

    if (Test-Path $RunScript) {
        Write-Host " [ OK ] Pacote carregado com sucesso. Iniciando interface gráfica..." -ForegroundColor Green
        & $RunScript
    } else {
        Write-Error "Não foi possível localizar o arquivo Run.ps1 dentro do pacote extraído."
    }
} catch {
    Write-Error "Falha ao baixar e inicializar o Otimizador: $($_.Exception.Message)"
}
