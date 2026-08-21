# 💡 Ideias e Sugestões Pendentes | Otimizador de Windows

Documento de rastreamento local de ideias, melhorias e sugestões em backlog para o projeto.

## 👤 Ideias do Usuário (Enviadas)
- [x] **[ID-001] [2026-08-21]**: **Seleção Granular de Apps**: Permitir que o usuário marque/desmarque quais aplicativos deseja remover (ex: Xbox Game Bar, Spotify, Sticky Notes, Teams) em uma interface interativa. *(Concluído na v13.0)*
- [x] **[ID-002] [2026-08-21]**: **Tratamento Especial do Xbox e Popups**: Implementar o redirecionamento de protocolo `ms-gamingoverlay` e `ms-gamebar` para `systray.exe`, eliminando popups de erro para gamers. *(Concluído na v13.0)*
- [x] **[ID-003] [2026-08-21]**: **Segurança, Rollback e Ponto de Restauração**: Criar ponto de restauração do sistema automático antes de otimizações críticas e fornecer mecanismo de restauração/desfazer (Undo). *(Concluído na v13.0)*
- [x] **[ID-004] [2026-08-21]**: **Modularização do Repositório para Open-Source**: Estruturar o projeto com separação clara de módulos no GitHub (`/src`, `/Config`, etc.) para receber contribuições da comunidade. *(Concluído na v13.0)*
- [x] **[ID-005] [2026-08-21]**: **Distribuição Bimodal**: Suporte a execução tanto via launcher único portátil (`Otimizador.cmd`) quanto via comando web direto (`irm | iex`). *(Concluído na v13.0)*

## 🤖 Sugestões da IA (Avaliadas)
- [x] **[ID-006] [2026-08-21]**: **Interface Gráfica WPF Dark Mode Nativa**: Modal XAML embutido em PowerShell (sem dependências externas) com caixas de seleção, busca e categorização visual de apps e tweaks. *(Concluído na v13.0)*
- [x] **[ID-007] [2026-08-21]**: **Catálogo Declarativo JSON**: Separar a lista de apps e diretivas em arquivos JSON estruturados (`Apps.json`, `Tweaks.json`) com níveis de recomendação (`seguro`, `avançado`). *(Concluído na v13.0)*
- [x] **[ID-008] [2026-08-21]**: **Bloqueio de IA Local Avançada (Win 11 24H2+)**: Desativar Windows Recall, Click-to-Do, serviço `WSAIFabricSvc`, AI no Notepad e Paint. *(Concluído na v13.0)*
- [x] **[ID-009] [2026-08-21]**: **Modo CLI com Presets para SysAdmins**: Adicionar suporte a parâmetros de linha de comando (`-Preset Gamer`, `-Preset Corporativo`, `-Silent`). *(Concluído na v13.0)*
- [ ] **[ID-010] [2026-08-21]**: **Exportação e Importação de Perfis Customizados**: Permitir que técnicos salvem presets próprios em arquivos `.json` e os carreguem em outras máquinas via linha de comando (`-Preset C:\Custom.json`).
- [ ] **[ID-011] [2026-08-21]**: **Detecção Inteligente de Hardware (SSD vs HDD)**: Detectar o tipo de disco do sistema com `Get-PhysicalDisk` para sugerir desligar o SysMain apenas em unidades mecânicas lentas (HDDs).

## ❌ Ideias Rejeitadas / Descartadas
- *Nenhuma ideia descartada até o momento.*
