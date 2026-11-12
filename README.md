<div align="center">
  <h1>⚡ Otimizador de Windows (10 & 11)</h1>
  <p><i>Debloat, privacidade, cibersegurança e otimização em executável nativo com simulação e reversão exata.</i></p>

  [![OS - Windows 10 | 11](https://img.shields.io/badge/OS-Windows_10_%7C_11-0078D7?style=for-the-badge&logo=windows&logoColor=white)](https://microsoft.com/windows)
  [![.NET Framework 4.8](https://img.shields.io/badge/.NET_Framework-4.8_x64-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
  [![CI](https://img.shields.io/github/actions/workflow/status/raphael-c-martins/Otimizador-Windows-10-11/ci.yml?branch=main&style=for-the-badge&label=CI)](https://github.com/raphael-c-martins/Otimizador-Windows-10-11/actions)
  [![Licença MIT](https://img.shields.io/badge/Licen%C3%A7a-MIT-green?style=for-the-badge)](./LICENSE)
</div>

<br>

O **Otimizador de Windows** é um executável nativo (`.exe`, compilado em C# / .NET Framework 4.8 WPF) que remove bloatware, bloqueia telemetria e recursos de IA (Windows Recall, Copilot e Click-to-Do), endurece a segurança do sistema e da rede (compartilhamentos ocultos `C$`/`D$`/`ADMIN$`, SMBv1, WDigest, LLMNR, AutoRun/AutoPlay, acesso anônimo, logon de convidado SMB, Assistência Remota e proteção PUA no Defender) e aplica ajustes de interface e desempenho sem depender de arquivos `.bat`, `.cmd` ou PowerShell em tempo de execução. Toda alteração fica registrada em diário para reversão exata.

---

## Execução Rápida

### Opção 1: Executável Direto na Raiz (Recomendado — Funciona com PowerShell/Batch bloqueados)
1. Baixe diretamente o [**`Otimizador-Windows.exe`**](./Otimizador-Windows.exe) na raiz do repositório, na [última release](https://github.com/raphael-c-martins/Otimizador-Windows-10-11/releases/latest) (ou em `Code` → `Download ZIP`).
2. Dê duplo clique em **`Otimizador-Windows.exe`** (já na pasta raiz, sem precisar entrar em subpastas). O programa pede elevação de Administrador via janela nativa do Windows (UAC) e abre a interface gráfica.

> **Ambientes corporativos:** como todo o código, catálogos (`src/Config/*.json`) e telas ficam embutidos dentro do `.exe`, não é necessário liberar políticas do PowerShell (`ExecutionPolicy` / restrições de script) ou arquivos `.bat`/`.cmd` para executar.

### Opção 2: Atalho via PowerShell (PCs sem restrição de script)
Se preferir baixar e verificar o executável automaticamente pelo PowerShell, cole um dos comandos abaixo:

```powershell
# Atalho curto
irm https://tinyurl.com/2bsvrtpk | iex
```

```powershell
# Script direto do GitHub
irm "https://raw.githubusercontent.com/raphael-c-martins/Otimizador-Windows-10-11/main/get.ps1" | iex
```

> O `get.ps1` pede permissão de Administrador, baixa `Otimizador-Windows.exe` e `Otimizador-Windows.exe.sha256` da última release, valida a assinatura SHA256 do executável em `%ProgramData%\OtimizadorWindows\app` e inicia o programa.

---

## Funcionalidades

- **Executável único sem dependências externas:** compilado para Windows 10 e 11 (`x64`, `.NET Framework 4.8` nativo do sistema) utilizando APIs nativas Win32, Registro 64-bit, WMI, WinRT e COM em vez de scripts externos.
- **Interface gráfica (WPF Dark Mode):** perfis com 1 clique, aba dedicada **Privacidade & Segurança**, seleção granular de aplicativos e ajustes, busca e log ao vivo sem travar a janela.
- **Cibersegurança e Endurecimento do Sistema (9 ajustes nativos reversíveis):**
  - **Compartilhamentos administrativos ocultos (`DisableAdminShares`):** desativa a criação automática de `C$`, `D$` e `ADMIN$` (`AutoShareWks = 0` ou `AutoShareServer = 0`) sem apagar pastas compartilhadas manualmente, com reinício controlado do `LanmanServer` e conferência pós-aplicação.
  - **Proteção contra ameaças de rede, credenciais e mídias (Lote 1):** desativa o protocolo legado SMBv1 (`SMB1 = 0` + driver `mrxsmb10`), bloqueia senhas em texto claro na memória via WDigest (`UseLogonCredential = 0`), desliga resolução multicast LLMNR (`EnableMulticast = 0`), desativa AutoRun e Reprodução Automática em pendrives/discos, restringe acesso e enumeração anônima no LSA/SAM (`RestrictAnonymous` e `RestrictAnonymousSAM`), bloqueia logon de convidado SMB sem senha (`AllowInsecureGuestAuth = 0`), desativa a Assistência Remota nativa e habilita o bloqueio PUA (aplicativos indesejados) por diretiva do Microsoft Defender.
  > **Atenção:** reiniciar o `LanmanServer` pode interromper brevemente conexões SMB ativas; bloquear logon de convidado SMB sem senha pode impactar NAS/impressoras antigas sem autenticação; e o bloqueio de SMBv1 requer reinicialização do Windows para efeito completo. Tudo pode ser revertido via `-Undo`.
- **Modo simulação (`-DryRun`):** mostra tudo o que seria feito sem alterar o sistema e sem exigir privilégio de Administrador.
- **Ponto de restauração confirmado:** verifica via WMI se o Windows realmente criou o ponto. Se falhar, você decide se continua.
- **Undo exato:** grava os valores originais de registro, serviços (incluindo reinício coordenado pós-reversão no `LanmanServer`), tarefas agendadas e energia em diário para restaurar o estado exato anterior.
- **Respeita notebooks:** mantém a hibernação e não ativa o plano Ultimate Performance em equipamentos com bateria.
- **Correção de protocolos do Xbox:** neutraliza `ms-gamebar` e `ms-gamingoverlay` ao remover o Game Bar para evitar popups de erro.

---

## Perfis

Todos os 4 perfis vêm com os ajustes de **Privacidade, IA e os 9 itens de Cibersegurança** selecionados por padrão (podendo ser desmarcados livremente na interface antes de executar).

| Perfil | Foco | Aplicativos | Xbox | OneDrive / Edge / Serviços |
| :--- | :--- | :--- | :--- | :--- |
| **Padrão** | Uso diário | Bloatware comum e patrocinados | Preservado | Remove OneDrive; mantém Edge e serviços |
| **Gamer** | Jogos | Bloatware e utilitários irrelevantes | Preservado (Game Pass, Clipchamp) | Remove OneDrive; mantém Edge e serviços |
| **Corporativo** | Trabalho | Jogos, redes sociais e patrocinados | Removido + correção de protocolos | Teams, Outlook e OneDrive preservados |
| **Extremo** | Hardware antigo / HDD | Quase todos | Removido + correção de protocolos | Remove OneDrive e Edge; desativa SysMain e WSearch |

---

## Uso em Linha de Comando (SysAdmins)

```powershell
# Modo terminal interativo
.\Otimizador-Windows.exe -CLI

# Simular um perfil sem alterar nada (não exige Administrador)
Start-Process -FilePath .\Otimizador-Windows.exe -ArgumentList '-Preset','Gamer','-DryRun','-Silent' -NoNewWindow -Wait -PassThru

# Execução automatizada (em terminal elevado como Administrador)
Start-Process -FilePath .\Otimizador-Windows.exe -ArgumentList '-Preset','Corporativo','-Silent' -NoNewWindow -Wait -PassThru

# Perfil personalizado em arquivo .json
.\Otimizador-Windows.exe -Preset C:\Perfis\MeuPerfil.json -Silent

# Desfazer as otimizações
.\Otimizador-Windows.exe -Undo
```

> **Dica de automação:** por ser um aplicativo gráfico Windows (`WinExe`), ao chamar por scripts `.bat` ou PowerShell e precisar aguardar o código de saída final, utilize `Start-Process ... -Wait -PassThru` no PowerShell ou `start /wait "" Otimizador-Windows.exe ...` no Prompt.

| Parâmetro | Função |
| :--- | :--- |
| `-CLI` | Abre o modo terminal interativo em vez da janela gráfica. |
| `-Preset <nome ou .json>` | Perfil a aplicar (`Padrao`, `Gamer`, `Corporativo`, `Extremo` ou caminho de um arquivo `.json`). |
| `-Silent` | Execução sem perguntas. Exige `-Preset` ou `-Undo`. |
| `-DryRun` | Simulação: mostra as etapas sem alterar nada. |
| `-CustomApps "Id1,Id2"` | Substitui a lista de aplicativos do perfil (IDs de `src/Config/Apps.json`). |
| `-SkipRestorePoint` | Não cria ponto de restauração. |
| `-ContinueWithoutRestorePoint` | No modo `-Silent`, continua mesmo se o ponto de restauração falhar. |
| `-Undo` | Reverte as otimizações usando os diários de alteração. |
| `-Help` | Exibe a lista de parâmetros. |

**Códigos de saída:** `0` sucesso · `1` erro de uso ou falta de permissão · `2` concluído com etapas com erro · `3` abortado porque o ponto de restauração falhou.

---

## Onde ficam os dados

| Pasta | Conteúdo |
| :--- | :--- |
| `%ProgramData%\OtimizadorWindows\logs` | Log UTF-8 de cada execução. |
| `%ProgramData%\OtimizadorWindows\estado` | Diários de alteração usados pelo Undo (`estado_*.json`). **Não apague.** |
| `%ProgramData%\OtimizadorWindows\app` | Cópia do executável baixada pelo `get.ps1`. |
| `%TEMP%\OtimizadorWindows-simulacao` | Logs do modo simulação (`-DryRun`). |

A pasta `%ProgramData%\OtimizadorWindows` aplica controle de acesso (ACL) nativo restrito a escrita apenas para Administradores e SYSTEM.

---

## Estrutura do Repositório

```
Otimizador-Windows-10-11/
├── Otimizador-Windows.exe             # Executável principal na raiz (pronto para duplo clique)
├── get.ps1                            # Bootstrap opcional via PowerShell com verificação SHA256
├── README.md                          # Documentação principal
├── LICENSE                            # Licença MIT
├── src/                               # Única pasta visível com código-fonte, catálogos, testes e docs
│   ├── Otimizador.csproj              # Projeto MSBuild (.NET Framework 4.8 WPF x64)
│   ├── app.manifest                   # Manifesto UAC (asInvoker) e DPI por monitor
│   ├── Program.cs                     # Ponto de entrada, parser de CLI e auto-elevação UAC
│   ├── AssemblyInfo.cs                # Metadados e versão do executável
│   ├── Assets/
│   │   └── Otimizador.ico             # Ícone multi-resolução (16px a 256px) embutido no .exe e nas janelas WPF
│   ├── Config/
│   │   ├── Apps.json                  # Catálogo de aplicativos (embutido no .exe)
│   │   ├── Tweaks.json                # Catálogo de ajustes e segurança (embutido no .exe)
│   │   └── Presets/                   # Perfis Padrao, Gamer, Corporativo e Extremo (embutidos)
│   ├── Core/
│   │   ├── OptimizationEngine.cs      # Motor único de execução (GUI e CLI)
│   │   ├── ChangeJournal.cs           # Registro de snapshot e reversão (Undo)
│   │   ├── SecurityManager.cs         # Hardening SMB (C$, D$, ADMIN$), Lote 1 e reinício seguro
│   │   ├── AppManager.cs              # Remoção Appx/Provisioned (WinRT/DISM), WinGet e OneDrive
│   │   ├── PrivacyManager.cs          # Telemetria, anúncios, Recall e Copilot
│   │   ├── PerformanceManager.cs      # Interface, energia, serviços, Edge e limpeza segura
│   │   ├── GamingManager.cs           # GameDVR e protocolos do Xbox
│   │   ├── BackupManager.cs           # Ponto de restauração via WMI
│   │   ├── UndoManager.cs             # Reversão por diário (compatível com v14.0)
│   │   ├── SystemInfo.cs              # OS/Server, notebook, SID do usuário e subprocessos
│   │   ├── Logger.cs                  # Log UTF-8 thread-safe e ACL do ProgramData
│   │   ├── Models.cs                  # Modelos de dados e serialização JSON
│   │   └── Constants.cs               # Versão e caminhos padrão
│   ├── UI/
│   │   ├── MainWindow.xaml            # Layout WPF Dark Mode (embutido no .exe)
│   │   ├── MainWindowGUI.cs           # Lógica da janela e fila assíncrona de logs
│   │   ├── ConsoleTUI.cs              # Modo terminal
│   │   └── AppSelectionModal.cs       # Seleção de aplicativos no modo terminal
│   ├── tests/                         # Suíte de testes automatizados em C# (Otimizador.Tests.csproj)
│   └── docs/                          # CHANGELOG.md, HISTORICO.md e ideias-pendentes.md
└── .github/                           # Workflows de CI/Release e regras do PSScriptAnalyzer
```

---

## Desenvolvimento e Compilação Local

Compila nativamente em qualquer Windows 10 ou 11 usando o **MSBuild do .NET Framework 4.8** (não exige instalação de Visual Studio ou SDK extra):

```powershell
# Compilar e atualizar automaticamente o Otimizador-Windows.exe na raiz (artefatos locais em .build/)
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" src\Otimizador.csproj /nologo /p:Configuration=Release /t:Rebuild

# Compilar e executar os testes automatizados (não alteram o sistema real)
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" src\tests\Otimizador.Tests.csproj /nologo /p:Configuration=Release /t:Rebuild
.\.build\Tests\Otimizador.Tests.exe

# Simular o perfil Corporativo sem aplicar mudanças
.\Otimizador-Windows.exe -Preset Corporativo -DryRun -Silent
```

**Adicionar um ajuste novo:** implemente o método em `src/Core` usando `ChangeJournal.SetRegistryValue`, `ChangeJournal.SetServiceStartupTracked` ou `ChangeJournal.DisableScheduledTaskTracked` (garantindo o Undo), registre a função em `TweakHandlers` de [`OptimizationEngine.cs`](./src/Core/OptimizationEngine.cs) e declare o ajuste em [`src/Config/Tweaks.json`](./src/Config/Tweaks.json). Os testes automatizados validam automaticamente todas as funções e perfis.

**Publicar uma versão:** crie e envie uma tag (`git tag v15.0.0` e depois `git push origin v15.0.0`). O workflow de Release roda toda a suíte de testes, compila o `Otimizador-Windows.exe`, gera `Otimizador-Windows.zip` e os hashes `.sha256` em um ambiente limpo do GitHub Actions.

---

## Contribuições

1. Faça um Fork do projeto.
2. Crie uma Branch (`git checkout -b feature/NovaOtimizacao`).
3. Compile, rode os testes (`Otimizador.Tests.exe`) e valide com `-DryRun`.
4. Abra um Pull Request.

---

## Licença

Distribuído sob a licença **MIT**. Veja o arquivo [LICENSE](./LICENSE).
