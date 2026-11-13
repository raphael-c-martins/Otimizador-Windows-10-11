# CHANGELOG | Otimizador de Windows

Log oficial de todas as mudanças notáveis, correções de bugs e evoluções arquiteturais deste projeto.

---

## [v15.1.3 - Polling na Confirmação WMI e Tratamento de Ponto Recente Existente] - 2026-11-13
### Corrigido & Melhorado
- **Retentativa (`WaitForRestorePoint`) na confirmação via WMI (`BackupManager.cs`):** o provedor WMI `SystemRestore` pode levar alguns segundos após `CreateRestorePoint` retornar `0` para expor o snapshot recém-criado; a verificação agora realiza até 5 leituras progressivas (até 9 s quando necessário) antes de concluir que o ponto não foi gravado.
- **Distinção e reaproveitamento honesto de Ponto de Restauração recente (`BackupManager.cs`, `MainWindowGUI.cs`, `Program.cs`):**
  - Coleta de eventos recentes (`SystemRestore`/`VSS`/`SPP`) também quando há ponto prévio, além de `IsRecentRestorePoint` e `RestorePointRecentAvailableException`.
  - Quando o Windows mantém um ponto recente já existente (`<= 24 h`, como em reexecuções na mesma máquina/VM mesmo com outro usuário), o log registra `AVISO` em vez de `ERRO` e o diálogo pergunta se deseja continuar usando esse ponto recente como salvaguarda em vez de afirmar falsamente que o sistema está sem ponto de restauração.
- **Testes:** cobertura ampliada em `SegurancaTests.cs` para diagnóstico em ponto recente e janela de 24 h (`IsRecentRestorePoint`), versão atualizada para `15.1.3` e `Otimizador-Windows.exe` recompilado na raiz.

---

## [v15.1.2 - Correções em VM: Log em Documentos, Shadow Storage VSS e Falsos Erros Appx/OneDrive] - 2026-11-13
### Corrigido & Melhorado
- **Log detalhado acessível em `Documentos\OtimizadorWindows` (`SystemInfo.cs`, `Logger.cs`, `Program.cs`):** além da cópia protegida em `%ProgramData%\OtimizadorWindows\logs`, o log UTF-8 passou a ser espelhado para a pasta `Documentos\OtimizadorWindows` do usuário conectado (com validação contra `ReparsePoint`/junções NTFS), direcionando o botão **Abrir Pasta de Logs** e os avisos finais para esse caminho visível no Explorador.
- **Alocação automática de Shadow Storage VSS para Ponto de Restauração (`BackupManager.cs`):** após ativar a Proteção do Sistema via WMI, o motor verifica `vssadmin list shadowstorage` e configura `5%` automaticamente (`vssadmin add/resize shadowstorage`) quando a cota está ausente ou em `0 bytes (0%)` (comum em VMs e instalações limpas do Windows 11), adicionando coleta dos eventos recentes de `SystemRestore`/`VSS`/`SPP` caso ainda haja falha.
- **Remoção de falsos erros em `People`, `DevHome` e aviso de `OneDrive` (`Apps.json`, `AppManager.cs`, `Otimizador.csproj`):**
  - Narrowing de `People` para `Microsoft.People` (evitando `Microsoft.Windows.PeopleExperienceHost` / erro `0x80070032`) com proteção `IsProtectedSystemShellPackage` ignorando pacotes em `Windows\SystemApps` e frameworks.
  - Pacotes provisionados órfãos sem arquivos locais (`DISM` código `3` quando não há pacote instalado correspondente, como `Microsoft.Windows.DevHome` no Windows 11 24H2) passam a registrar `AVISO` informativo em vez de `ERRO`.
  - `RemoveOneDriveDeep` prioriza instaladores somente leitura de sistema (`System32`/`SysWOW64`/`Program Files`) antes da chave do perfil e inclui a pasta do usuário conectado nas raízes confiáveis.
- **Testes automatizados ampliados de 50 para 53 verificações:** novas validações de regressão em `SegurancaTests.cs` e `CatalogoTests.cs`, atualização de versão interna para `15.1.2` e recompilação do executável raiz `Otimizador-Windows.exe`.

---

## [v15.1 - Auditoria de Robustez, Sanitização e Descarte WMI/COM] - 2026-11-13
### Corrigido & Melhorado
- **Ponto de Restauração mais confiável (`BackupManager.cs`):** rejeição explícita para Windows Server e política `DisableSR = 1`, liberação temporária de `VSS` e `swprv` com restauração no `finally`, sanitização do rótulo (`<= 32` caracteres para descrição total `<= 63`), proteção ao restaurar `SystemRestorePointCreationFrequency`, tolerância a `CreationTime` ausente na confirmação WMI e diagnóstico claro sobre o evento `8216`.
- **Segurança na resolução de executáveis e desinstaladores (`SystemInfo.cs`, `AppManager.cs`, `PerformanceManager.cs`, `MainWindowGUI.cs`):** resolução explícita para `System32`/`Windows` e `WindowsApps\winget.exe`, validação de `WinGetId` (`IsValidWinGetPackageId`) e validação de caminho/nome confiáveis para `OneDriveSetup.exe` e `setup.exe` do Edge (`IsTrustedUninstallExecutable`).
- **Limpeza e permissões mais resilientes (`PerformanceManager.cs`, `Logger.cs`, `UndoManager.cs`):** isolamento por arquivo em `DeleteDirectoryContentsSafely` para continuar limpando itens livres mesmo com arquivo em uso; travessia ACL em `Logger` ignorando `ReparsePoint`; nomes únicos ao arquivar `.restaurado.json` no Undo.
- **Liberação determinística WMI/COM (`AUD-05` em `ChangeJournal.cs`, `SecurityManager.cs`, `SystemInfo.cs`, `BackupManager.cs`):** blocos `using` para objetos/coleções WMI e `Marshal.FinalReleaseComObject` para objetos COM de `Schedule.Service`, diferenciando tarefa inexistente de erros reais e registrando aviso se um serviço não reiniciar no Undo.
- **Testes automatizados ampliados de 44 para 50 verificações:** novos testes de regressão em `SegurancaTests.cs` e `LimpezaTests.cs` e `Otimizador-Windows.exe` na raiz recompilado.

---

## [v15.1 - Lote 1 de Cibersegurança e Hardening] - 2026-11-12
### Adicionado
- **8 novos ajustes de Cibersegurança (Lote 1) em `SecurityManager.cs`, `Tweaks.json` e nos 4 perfis (`Padrao`, `Gamer`, `Corporativo`, `Extremo`):**
  - `DisableSMBv1`: desativa o protocolo legado SMBv1 (`LanmanServer\Parameters\SMB1 = 0`), remove dependência `MRxSmb10` de `LanmanWorkstation` quando presente e desabilita o driver `mrxsmb10` (`Start = 4`), com suporte completo ao Undo.
  - `DisableWDigest`: bloqueia o armazenamento de senhas em texto claro na memória do LSASS (`WDigest\UseLogonCredential = 0`).
  - `DisableLLMNR`: desliga a resolução de nomes multicast na rede local por diretiva (`DNSClient\EnableMulticast = 0`).
  - `DisableAutoRunAndAutoPlay`: desativa AutoRun e Reprodução Automática para unidades/USB no sistema e no usuário atual (`NoDriveTypeAutoRun = 255`, `NoAutorun = 1`, `DisableAutoplay = 1`).
  - `DisableAnonymousAccess`: restringe acesso e enumeração anônima de contas SAM (`Lsa\RestrictAnonymous = 1` e `RestrictAnonymousSAM = 1`).
  - `DisableSMBInsecureGuestAuth`: bloqueia logon de convidado sem senha no cliente SMB (`LanmanWorkstation\Parameters` e diretiva `AllowInsecureGuestAuth = 0`, com aviso sobre NAS/dispositivos antigos sem autenticação).
  - `DisableRemoteAssistance`: desativa a Assistência Remota nativa do Windows (`fAllowToGetHelp = 0`, `fAllowFullControl = 0`) sem afetar RDP ou Quick Assist.
  - `EnableDefenderPUAProtection`: ativa o bloqueio contra aplicativos potencialmente indesejados (PUA/adware) nas diretivas do Microsoft Defender (`PUAProtection = 1` e `MpEngine\MpEnablePus = 1`), tratando eventuais bloqueios de permissão como aviso.
- **Testes automatizados ampliados para 44 verificações:** novas asserções em `CatalogoTests.cs` (consistência dos 9 ajustes de segurança nos catálogos e nos 4 perfis) e `SegurancaTests.cs` (validação das especificações do Lote 1, filtro `DependOnService` e teste isolado de aplicação + reversão por `ChangeJournal` em chave temporária `HKCU`).

### Modificado
- Atualizada a versão interna para `15.1` (`src/Core/Constants.cs`, `src/AssemblyInfo.cs`), recompilado o executável `Otimizador-Windows.exe` na raiz e atualizados o texto do card Padrão (`src/UI/MainWindow.xaml`) e a documentação.

---

## [v15.0 - Executável Nativo C# e Hardening de Cibersegurança] - 2026-11-10
### Adicionado
- **Executável nativo em C# / .NET Framework 4.8 (`Otimizador-Windows.exe` na raiz):** o programa agora é compilado para binário único `x64` disponibilizado diretamente na raiz do repositório (substituindo o antigo `Otimizador.cmd`) com catálogos (`src/Config/*.json`), ícone próprio e layout WPF (`src/UI/MainWindow.xaml`) embutidos, eliminando a necessidade de scripts `.bat`, `.cmd` ou `.ps1` em tempo de execução — ideal para estações corporativas com PowerShell bloqueado.
- **Ícone próprio multi-resolução (`src/Assets/Otimizador.ico`):** ícone minimalista gerado em 6 resoluções (`16x16` a `256x256`) embutido no recurso Win32 do `Otimizador-Windows.exe` (Windows Explorer, atalhos e UAC) e na barra de título/tarefas das janelas WPF.
- **Cibersegurança — Desativar compartilhamentos administrativos ocultos (`C$`, `D$`, `ADMIN$`):** novo ajuste `DisableAdminShares` selecionado por padrão nos 4 perfis (Padrão, Gamer, Corporativo e Extremo) e listado na aba **Privacidade & Segurança**. Grava `AutoShareWks = 0` (Windows Cliente) ou `AutoShareServer = 0` (Windows Server), reinicia o serviço `LanmanServer` junto aos dependentes ativos quando necessário e valida os compartilhamentos resultantes via WMI (`Win32_Share`) / `net share`, preservando compartilhamentos manuais, `IPC$` e `print$`.
- **Reversão completa de serviço (`ServiceRestart`):** o Undo reverte primeiro a chave do Registro e reinicia o `LanmanServer` automaticamente se estiver ativo.
- **Workflow de Release com binário compilado:** `.github/workflows/release.yml` compila e valida os testes no ambiente limpo do GitHub antes de anexar `Otimizador-Windows.exe`, `Otimizador-Windows.zip` e seus arquivos `.sha256`.
- **Suíte de testes C# (`src/tests/Otimizador.Tests.csproj`):** 41 testes automatizados validando catálogos embutidos e em disco, perfis, hardening SMB, compatibilidade retroativa com diários JSON v14.0, reversão do Registro, junções NTFS em temporários, unicidade de logs e diários, carregamento WPF e presença do executável na raiz.

### Modificado & Corrigido
- **Reorganização da raiz do projeto:** consolidação do projeto MSBuild (`src/Otimizador.csproj`), manifesto (`src/app.manifest`), ativos (`src/Assets/`), catálogos (`src/Config/`), testes (`src/tests/`) e documentação (`src/docs/`) dentro de `src/`, saídas locais isoladas em `.build/` (ignoradas pelo Git) e regras do PSScriptAnalyzer em `.github/PSScriptAnalyzerSettings.psd1`, deixando apenas `Otimizador-Windows.exe`, `get.ps1`, `README.md` e `LICENSE` na raiz.
- **Proteção contra travessia de links em temporários (`AUD-02`):** `PerformanceManager.ClearSystemTempFiles` desvincula junções e links simbólicos (`ReparsePoint`) sem percorrer nem excluir arquivos do diretório de destino.
- **Prevenção de sobrescrita em logs e diários:** `Logger.ResolveUniqueFilePath` acrescenta sufixos `(2)`, `(3)` para logs no mesmo segundo, e `ChangeJournal.ResolveUniqueJournalPath` usa `_002`, `_003` em execuções no mesmo milissegundo mantendo a ordenação reversa do Undo.
- **Contabilização correta no WinGet:** `AppManager.InvokeWinGetUninstall` sinaliza sucesso quando o `winget` conclui a remoção sem pacote Appx detectado previamente.
- **Bootstrap opcional atualizado (`get.ps1`):** passa a baixar diretamente `Otimizador-Windows.exe` e exigir a validação SHA256 da release antes de iniciar.
- **Segurança no CI e `.gitignore` (`.github/workflows/ci.yml`):** adicionada varredura automática de segredos com `gitleaks`, controle de concorrência, atualização das actions para Node 24 e regras no `.gitignore` para `.build/`, certificados `.pfx`, bancos `.db`/`.sqlite`/`.sqlite3` e pasta local `/.claude/`.
- **Padronização visual da interface e documentação (`AUD-04`):** removida a maioria dos emojis excessivos nos botões, abas, cards e categorias da janela WPF (`src/UI/MainWindow.xaml` e `src/UI/MainWindowGUI.cs`) e nos subtítulos de documentação (`README.md`, `CHANGELOG.md`, `ideias-pendentes.md`), preservando apenas o marcador principal no cabeçalho e evitando problemas de glifos com variation selector em fontes mais antigas.

---

## [v14.0 - Auditoria, Motor Único e Undo Exato] - 2026-10-21
### Adicionado
- **Motor único de execução:** GUI e terminal usam o mesmo motor; cada ajuste do `Tweaks.json` aponta para sua própria função (campo `Function`).
- **Diário de alterações e Undo exato:** valores originais de registro, serviços, tarefas e energia são gravados antes de cada mudança e restaurados exatamente pelo Undo.
- **Modo simulação (`-DryRun`):** mostra tudo o que seria feito sem alterar nada e sem exigir Administrador.
- **Perfis personalizados:** `-Preset C:\caminho\perfil.json` (validado contra os catálogos).
- **Novos ajustes separados:** Ocultar botões da barra de tarefas, Pesquisa Web no menu Iniciar, Desinstalar OneDrive e Limpar temporários.
- **Códigos de saída para automação:** 0 sucesso, 1 erro de uso/permissão, 2 etapas com erro, 3 abortado sem ponto de restauração.
- **Testes e CI:** suíte Pester em `/tests` e GitHub Actions com verificação de sintaxe, PSScriptAnalyzer e testes. Workflow de release com SHA256.

### Corrigido
- O OneDrive virou um ajuste que pode ser desmarcado; o perfil Corporativo não o remove mais (Padrão, Gamer e Extremo continuam removendo).
- A versão do programa saiu do título da janela e do cabeçalho do terminal (continua registrada no log).
- O ponto de restauração é confirmado; falhas não aparecem mais como sucesso.
- Etapas com falha são reportadas como `ERRO` no log, no resumo e no código de saída.
- `Run.ps1` verifica privilégio de Administrador.
- A linha de comando passa a respeitar os ajustes (`ApplyTweaks`) do perfil.
- Caixas de seleção da GUI passam a ter efeito individual (ex.: "Exibir extensões" não altera mais o menu de contexto).
- A GUI não trava durante a execução e bloqueia ações concorrentes (Undo, perfis, fechar).
- Logs movidos para `%ProgramData%\OtimizadorWindows\logs` e nunca apagados pela limpeza de temporários.
- Falha da GUI aberta pelo `Otimizador.cmd` agora reabre o modo terminal em janela visível (antes ficava oculta e parada).
- Teams: o pacote `MSTeams` (trabalho/escola) não é mais removido pelos perfis Padrão, Gamer e Extremo.
- Notebooks: hibernação mantida e plano Ultimate Performance não ativado. O plano não é mais duplicado a cada execução.
- Ajustes de usuário vão para o perfil conectado quando o programa é elevado com outra conta.
- Limite de 24 h dos pontos de restauração volta ao valor original após a criação.
- Undo restaura `VisualFXSetting` para o padrão do Windows (0).
- `get.ps1`: verifica SHA256 da release, extrai em pasta protegida, não fecha o terminal do usuário e preserva o TLS 1.3.
- `Otimizador.cmd -CLI` não repete mais o parâmetro `-CLI`.

---

## [v13.0 - Arquitetura Modular & Interface Gráfica Moderna] - 2026-08-21
### Adicionado
- **Interface Gráfica Completa (Fluent Dark Mode GUI):** Janela moderna estilo Windows 11 com menu lateral (Sidebar), cards de perfis com 1 clique, painéis de personalização de aplicativos, privacidade, performance e terminal de logs ao vivo.
- **Módulo de Threading Não-Bloqueante (`ThreadingManager.ps1`):** Implementado bombeamento de eventos da interface gráfica (`Invoke-DoEvents`) e execução assíncrona (`Invoke-NonBlocking`), garantindo 60 FPS e eliminando qualquer risco de travamento ou estado de "Não está respondendo".
- **Pré-Cache em Memória de Pacotes DISM (`AppManager.ps1`):** Leitura otimizada de pacotes provisionados com busca em RAM, reduzindo o tempo de remoção de 2 minutos para menos de 10 segundos.
- **Engenharia Anti-Popup para Xbox / Jogos:** Redirecionamento de protocolos (*Null-Routing*) `ms-gamingoverlay` e `ms-gamebar` para `systray.exe`, eliminando erros após a remoção do Game Bar.
- **Perfis Especializados (Presets Declarativos em JSON):** Perfis prontos *Padrão* (Equilibrado), *Gamer* (Preserva Xbox/FPS boost), *Corporativo* (Produtividade/Trabalho) e *Extremo* (Hardware legado/HDD).
- **Módulo de Ponto de Restauração (`BackupManager.ps1`):** Criação automática de ponto de restauração do sistema com desbloqueio do limite de 24h (`SystemRestorePointCreationFrequency = 0`).
- **Engine de Reversão / Desfazer (`UndoManager.ps1`):** Opção com 1 clique na interface gráfica e via CLI para restaurar serviços (SysMain, WSearch), menu nativo do Windows 11 e protocolos do Xbox.
- **Suporte Bimodal Completo (GUI & CLI para SysAdmins):** Execução gráfica nativa ao dar duplo clique no `Otimizador.cmd`, com suporte a parâmetros de linha de comando (`-CLI`, `-Preset Gamer`, `-Silent`, `-SkipRestorePoint`, `-Undo`).

### Modificado & Corrigido
- **Correção Definitiva de Encoding UTF-8 com BOM:** Todos os arquivos de script e catálogos foram regravados com assinatura UTF-8 com BOM (`0xEF 0xBB 0xBF`) e o launcher `.cmd` em ANSI limpo sem BOM, eliminando qualquer problema de caracteres especiais (*Mojibake*) ou eco de comandos no terminal.
- **Launcher Mestre (`Otimizador.cmd`):** Auto-elevação UAC silenciosa com lançamento direto da janela visual sem terminal concorrente visível.

---

## [v12.0 - Versão de Performance em Lote] - 2026-03-31
### Adicionado
- **Unificação Engine:** Unificados os antigos scripts em um arquivo canônico: `Otimizador.cmd`.
- **Menu Modular Bimodal:** Seleção em tempo de execução para _Windows 10/11_ e nível de tunagem _Padrão/Extremo_.
- **Alta Densidade Visual:** Reformulação cromática do output no console com escape sequences ANSI.
- **Auditoria com Timestamps:** Embutido o rastreamento temporal dinâmico (`[HH:mm:ss]`) na interface e logs em `/logs/`.
