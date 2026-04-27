# CHANGELOG | Otimizador de Windows

Log oficial de todas as mudanças notáveis, correções de bugs e evoluções arquiteturais deste projeto.

## [v12.0 - Versão Final de Performance] - 2026-03-31
### Adicionado
- **Unificação Engine:** Unificados os antigos scripts em um arquivo canônico: `Otimizador_Ultimate.cmd`.
- **Menu Modular Bimodal (OS & Agressividade):** Implementada a seleção em tempo de execução para _Windows 10/11_ e nível de tunagem _Padrão/Extremo_.
- **Alta Densidade Visual:** Reformulação térmica e cromática do output no console, utilizando escape sequences Truecolor/ANSI para prover feedback em blocos Premium.
- **Integração Win11Debloat:** Selecionadas apenas as diretivas estritas de alívio de CPU/RAM extraídas do projeto Open Source, como a Remoção de GameBar/DVR, Desativação de Search Highlights e remoção ágil de botões inúteis. Foram removidas injeções puramente subjetivas e de cunho pessoal (DarkMode e preferências de Taskbar) para forçar um foco estrito em Hardware.
- **Remoção de Componentes Redundantes:** Processo silencioso para remoção de Outlook, Copilot e desativação preventiva do OneDrive integrada na rotina principal.
- **Auditoria Precisa com Timestamps:** Embutido o rastreamento temporal dinâmico (`[HH:mm:ss]`) na interface do Console e Logs para cada rotina de baixo nível, provendo precisão de *debugging*. Tratamento estrito no fluxo de erro de remoções (Silenciando Red-Texts do PowerShell).
- **Tratamento Global de Logs:** Logs gerados de cada operação agora são persistidos estritamente na pasta `\logs\`.
- **Validação de Entrada de Dados:** Implementada lógica de loop com validação estrita para menus de seleção, impedindo avanços com opções inválidas e melhorando a resiliência da TUI.
- **Feedback de Seleção Dinâmico:** Adicionado indicador visual do Sistema Operacional selecionado durante o fluxo de configuração para reduzir erros de operação.

- Repositório local completamente refatorado; *scripts legados* foram depreciados permanentemente e arquivados em favor do motor de execução Canônica.
- Corrigido Bug de Pathing: Corrigido o fechamento prematuro do terminal quando o caminho completo (`%~dp0`) contém caracteres sensíveis como E comercial (`&`), assegurando execução segura utilizando concatenação nominal e aspas rígidas.

### Segurança e Estabilidade
- O design de _Polyglot Script_ (Batch + PS1 invisível) garante a execução com política de Bypasse local e requisição automática do UAC em qualquer ambiente.
- Invocação assíncrona tolerante a falhas `-ErrorAction SilentlyContinue` onde pacotes corrompidos não estouram a UI no terminal.
