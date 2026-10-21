# HISTÓRICO DE DECISÕES ARQUITETURAIS & ENGENHARIA

> **DIRETRIZ CRÍTICA DE PRESERVAÇÃO**:
> Em qualquer atividade de manutenção ou evolução deste projeto, é terminantemente proibido apagar a pasta de logs (`%ProgramData%\OtimizadorWindows\logs`, ou `/logs/` em versões antigas), os diários de alteração (`%ProgramData%\OtimizadorWindows\estado`) ou remover arquivos históricos persistentes. Os diários são a única forma de desfazer exatamente uma otimização. Todo o desenvolvimento deve preservar a integridade das auditorias anteriores e a segurança dos dados do usuário.

---

### [2026-10-21] - Auditoria e Correções Estruturais (v14.0)

- **Contexto e Motivação:**
  Uma auditoria da v13 encontrou problemas que podiam causar dano ou passar despercebidos: o OneDrive era removido sempre, sem opção; falhas apareciam como sucesso (inclusive o ponto de restauração); a linha de comando e a janela aplicavam coisas diferentes para o mesmo perfil; várias caixas de seleção não tinham efeito próprio; e a janela travava durante a execução.

- **Decisões Arquiteturais Adotadas:**
  1. **Motor único de execução (`OptimizationEngine.ps1`):**
     - A GUI e o terminal apenas montam a lista de aplicativos e ajustes; quem executa é sempre o mesmo motor. Cada ajuste do `Tweaks.json` informa no campo `Function` qual função o executa, então cada caixa de seleção corresponde a exatamente uma função.
     - Cada item vira uma etapa isolada com status `OK`, `AVISO`, `ERRO` ou `SIMULADO`. Uma falha não interrompe as outras e entra no resumo final e no código de saída.
  2. **Falhas reais em vez de silêncio:**
     - Removidos os `catch {}` vazios e o `-ErrorAction SilentlyContinue` nas alterações. As funções lançam erro quando algo falha, e avisos (ex.: serviço que não parou) são contados.
     - O ponto de restauração agora é confirmado com `Get-ComputerRestorePoint`. Se falhar, o usuário decide se continua; no modo `-Silent` a execução é abortada (código 3), a menos que se use `-ContinueWithoutRestorePoint`.
  3. **Diário de alterações e Undo exato (`ChangeJournal.ps1`):**
     - Antes de cada alteração de registro, serviço, tarefa agendada ou plano de energia, o valor original é gravado em `estado_*.json`. O Undo reverte as execuções da mais nova para a mais antiga, devolvendo o estado exato. Sistemas otimizados pela v13 (sem diário) usam a restauração de padrões do Windows.
  4. **Execução em segundo plano na GUI (`ThreadingManager.ps1`):**
     - Substituído o "bombeamento" de mensagens (`DoEvents`) por um runspace separado. A janela lê logs e progresso por uma fila a cada 120 ms. Durante a execução, os botões que poderiam iniciar outra operação ficam bloqueados e a janela não pode ser fechada.
  5. **Segurança operacional:**
     - Logs e diários ficam em `%ProgramData%\OtimizadorWindows`, com escrita restrita a Administradores e SYSTEM. A limpeza de temporários nunca apaga a pasta do programa nem a de logs (comparação de caminhos por componentes, não por texto).
     - O `Run.ps1` verifica privilégio de Administrador e se reabre elevado (ou falha com código 1 no modo `-Silent`).
     - O `get.ps1` baixa a última release publicada e confere o SHA256 antes de executar, extraindo em pasta protegida.
  6. **Respeito ao equipamento e ao usuário:**
     - Em notebooks, a hibernação é mantida e o plano Ultimate Performance não é ativado. O plano é reaproveitado entre execuções (antes uma cópia nova era criada a cada vez).
     - Se o programa for elevado com outra conta de administrador, os ajustes de usuário vão para o perfil do usuário conectado (`HKEY_USERS\<SID>`).
     - O OneDrive virou um ajuste próprio (pode ser desmarcado): os perfis Padrão, Gamer e Extremo o removem; o Corporativo o preserva.
     - A versão do programa não aparece mais na janela nem no terminal (é controle interno). Ela continua registrada no log de cada execução e no CHANGELOG.
     - Corrigido o catálogo do Teams: `MSTeams` é o Teams de trabalho e `MicrosoftTeams` o pessoal. Nomes sem `*` passam a exigir correspondência exata.
  7. **Qualidade contínua:**
     - Modo `-DryRun` (simulação sem alterações e sem precisar de Administrador), testes Pester em `/tests` e CI no GitHub Actions (sintaxe, PSScriptAnalyzer e testes). A pasta `Documentação/` foi renomeada para `docs/` (acentos em nomes de pasta atrapalham links e pacotes zip).

---

### [2026-08-21] - Reestruturação Modular, Interface Gráfica Moderna (WPF Dark Mode) e Threading Não-Bloqueante (v13.0)

- **Contexto e Motivação:**
  O projeto precisava evoluir de um script monolítico para uma solução de engenharia aberta, profissional e comparável às principais referências mundiais de debloat (ex: *Win11Debloat*), oferecendo interface gráfica com seleção granular de aplicativos, preservação do ecossistema de jogos (Xbox Game Pass) e execução fluida sem travamentos.

- **Decisões Arquiteturais Adotadas:**
  1. **Separação em Módulos Lógicos (`/src/Core`, `/src/UI`, `/Config`, `/Presets`):**
     - O código foi desacoplado de um único arquivo `.cmd` para módulos especializados (`BackupManager`, `AppManager`, `GamingManager`, `PrivacyManager`, `PerformanceManager`, `UndoManager`, `ThreadingManager` e `MainWindowGUI`).
  2. **Interface Gráfica Moderna em Dark Mode com Menu Lateral (Sidebar):**
     - Desenvolvida janela WPF nativa com Fluent Design Dark Mode e navegação por abas laterais para seleção de presets, catálogo de 35 aplicativos com busca instantânea, tweaks de IA/telemetria e terminal de logs com barra de progresso em tempo real.
  3. **Threading Não-Bloqueante & Dispatcher Pumping (`ThreadingManager.ps1`):**
     - Implementação da rotina `Invoke-DoEvents` (pumping de mensagens da UI a 60 FPS) e isolamento da criação de ponto de restauração (`Checkpoint-Computer` / VSS) e comandos lentos em segundo plano para erradicar o estado de "Não está respondendo".
  4. **Pré-Cache em Memória de Pacotes DISM (`AppManager.ps1`):**
     - Leitura única dos pacotes provisionados do Windows em RAM, acelerando a remoção de 27 aplicativos de 2 minutos para menos de 15 segundos.
  5. **Neutralização de Protocolos Xbox via Null-Routing (`GamingManager.ps1`):**
     - Redirecionamento das URIs `ms-gamingoverlay` e `ms-gamebar` para `systray.exe` para permitir a remoção do Game Bar sem gerar popups de erro ao abrir jogos.
  6. **Padronização de Encoding UTF-8 com BOM e Launcher Limpo:**
     - Conversão de todos os arquivos `.ps1` e `.json` para UTF-8 com BOM e gravação do `Otimizador.cmd` em ANSI limpo com elevação silenciosa da GUI.

---

### [2026-03-31] - Unificação Canônica e Alta Densidade Visual (v12.0)

- **Contexto:** Existência de múltiplos scripts dispersos no repositório.
- **Decisão:** Unificação no launcher `Otimizador.cmd` com detecção de SO e sistema de escape cromático ANSI.
