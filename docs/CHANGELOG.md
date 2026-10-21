# CHANGELOG | Otimizador de Windows

Log oficial de todas as mudanças notáveis, correções de bugs e evoluções arquiteturais deste projeto.

---

## [v14.0 - Auditoria, Motor Único e Undo Exato] - 2026-10-21
### ✨ Adicionado
- **Motor único de execução:** GUI e terminal usam o mesmo motor; cada ajuste do `Tweaks.json` aponta para sua própria função (campo `Function`).
- **Diário de alterações e Undo exato:** valores originais de registro, serviços, tarefas e energia são gravados antes de cada mudança e restaurados exatamente pelo Undo.
- **Modo simulação (`-DryRun`):** mostra tudo o que seria feito sem alterar nada e sem exigir Administrador.
- **Perfis personalizados:** `-Preset C:\caminho\perfil.json` (validado contra os catálogos).
- **Novos ajustes separados:** Ocultar botões da barra de tarefas, Pesquisa Web no menu Iniciar, Desinstalar OneDrive e Limpar temporários.
- **Códigos de saída para automação:** 0 sucesso, 1 erro de uso/permissão, 2 etapas com erro, 3 abortado sem ponto de restauração.
- **Testes e CI:** suíte Pester em `/tests` e GitHub Actions com verificação de sintaxe, PSScriptAnalyzer e testes. Workflow de release com SHA256.

### 🔧 Corrigido
- O OneDrive não é mais removido em todas as execuções (agora é opcional; só o perfil Extremo o inclui).
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
### ✨ Adicionado
- **Interface Gráfica Completa (Fluent Dark Mode GUI):** Janela moderna estilo Windows 11 com menu lateral (Sidebar), cards de perfis com 1 clique, painéis de personalização de aplicativos, privacidade, performance e terminal de logs ao vivo.
- **Módulo de Threading Não-Bloqueante (`ThreadingManager.ps1`):** Implementado bombeamento de eventos da interface gráfica (`Invoke-DoEvents`) e execução assíncrona (`Invoke-NonBlocking`), garantindo 60 FPS e eliminando qualquer risco de travamento ou estado de "Não está respondendo".
- **Pré-Cache em Memória de Pacotes DISM (`AppManager.ps1`):** Leitura otimizada de pacotes provisionados com busca em RAM, reduzindo o tempo de remoção de 2 minutos para menos de 10 segundos.
- **Engenharia Anti-Popup para Xbox / Jogos:** Redirecionamento de protocolos (*Null-Routing*) `ms-gamingoverlay` e `ms-gamebar` para `systray.exe`, eliminando erros após a remoção do Game Bar.
- **Perfis Especializados (Presets Declarativos em JSON):** Perfis prontos *Padrão* (Equilibrado), *Gamer* (Preserva Xbox/FPS boost), *Corporativo* (Produtividade/Trabalho) e *Extremo* (Hardware legado/HDD).
- **Módulo de Ponto de Restauração (`BackupManager.ps1`):** Criação automática de ponto de restauração do sistema com desbloqueio do limite de 24h (`SystemRestorePointCreationFrequency = 0`).
- **Engine de Reversão / Desfazer (`UndoManager.ps1`):** Opção com 1 clique na interface gráfica e via CLI para restaurar serviços (SysMain, WSearch), menu nativo do Windows 11 e protocolos do Xbox.
- **Suporte Bimodal Completo (GUI & CLI para SysAdmins):** Execução gráfica nativa ao dar duplo clique no `Otimizador.cmd`, com suporte a parâmetros de linha de comando (`-CLI`, `-Preset Gamer`, `-Silent`, `-SkipRestorePoint`, `-Undo`).

### 🔧 Modificado & Corrigido
- **Correção Definitiva de Encoding UTF-8 com BOM:** Todos os arquivos de script e catálogos foram regravados com assinatura UTF-8 com BOM (`0xEF 0xBB 0xBF`) e o launcher `.cmd` em ANSI limpo sem BOM, eliminando qualquer problema de caracteres especiais (*Mojibake*) ou eco de comandos no terminal.
- **Launcher Mestre (`Otimizador.cmd`):** Auto-elevação UAC silenciosa com lançamento direto da janela visual sem terminal concorrente visível.

---

## [v12.0 - Versão de Performance em Lote] - 2026-03-31
### Adicionado
- **Unificação Engine:** Unificados os antigos scripts em um arquivo canônico: `Otimizador.cmd`.
- **Menu Modular Bimodal:** Seleção em tempo de execução para _Windows 10/11_ e nível de tunagem _Padrão/Extremo_.
- **Alta Densidade Visual:** Reformulação cromática do output no console com escape sequences ANSI.
- **Auditoria com Timestamps:** Embutido o rastreamento temporal dinâmico (`[HH:mm:ss]`) na interface e logs em `/logs/`.
