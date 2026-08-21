# CHANGELOG | Otimizador de Windows

Log oficial de todas as mudanças notáveis, correções de bugs e evoluções arquiteturais deste projeto.

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
