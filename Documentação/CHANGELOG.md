# CHANGELOG | Otimizador Ultimate

Log oficial de todas as mudanças notáveis, correções de bugs e evoluções arquiteturais deste projeto.

## [v12.0 - Otimizador Ultimate] - 2026-03-31
### Adicionado
- **Unificação Engine:** Unificados os antigos scripts em um arquivo canônico: `Otimizador_Ultimate.cmd`.
- **Menu Modular Bimodal (OS & Agressividade):** Implementada a seleção em tempo de execução para _Windows 10/11_ e nível de tunagem _Padrão/Extremo_.
- **Alta Densidade Visual:** Reformulação térmica e cromática do output no console, utilizando escape sequences Truecolor/ANSI para prover feedback em blocos Premium.
- **Integração Win11Debloat:** Selecionadas apenas as diretivas estritas de alívio de CPU/RAM extraídas do projeto Open Source, como a Remoção de GameBar/DVR, Desativação de Search Highlights e remoção ágil de botões inúteis. Foram removidas injeções puramente subjetivas e de cunho pessoal (DarkMode e preferências de Taskbar) para forçar um foco estrito em Hardware.
- **Hard-Kill Global de Ciber-IAs e Lixos:** Processo silencioso para remoção massiva de Outlook, Copilot, OneDrive (Process-kill preventivo) integrados na rotina raiz sem quebras.
- **Auditoria Precisa com Timestamps:** Embutido o rastreamento temporal dinâmico (`[HH:mm:ss]`) na interface do Console e Logs para cada rotina de baixo nível, provendo precisão de *debugging*. Tratamento estrito no fluxo de erro de remoções (Silenciando Red-Texts do PowerShell).
- **Tratamento Global de Logs:** Logs gerados de cada operação agora são persistidos estritamente na pasta `\logs\`.

- Repositório local completamente refatorado; *scripts legados* foram depreciados permanentemente e arquivados em favor do motor de execução Canônica.
- Corrigido Bug de Pathing: Corrigido o fechamento prematuro do terminal quando o caminho completo (`%~dp0`) contém caracteres sensíveis como E comercial (`&`), assegurando execução segura utilizando concatenação nominal e aspas rígidas.

### Segurança e Estabilidade
- O design de _Polyglot Script_ (Batch + PS1 invisível) garante a execução com política de Bypasse local e requisição automática do UAC em qualquer ambiente.
- Invocação assíncrona tolerante a falhas `-ErrorAction SilentlyContinue` onde pacotes corrompidos não estouram a UI no terminal.
