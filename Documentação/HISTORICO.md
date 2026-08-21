# HISTÓRICO DE DECISÕES ARQUITETURAIS & ENGENHARIA

> **DIRETRIZ CRÍTICA DE PRESERVAÇÃO**:
> Em qualquer atividade de manutenção ou evolução deste projeto, é terminantemente proibido apagar a pasta física de logs (`/logs/`) ou remover arquivos históricos persistentes. Todo o desenvolvimento deve preservar a integridade das auditorias anteriores e a segurança dos dados do usuário.

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
