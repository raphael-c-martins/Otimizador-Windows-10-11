# Ideias e Sugestões Pendentes | Otimizador de Windows

Documento de rastreamento local de ideias, melhorias e sugestões em backlog para o projeto (espelhado na tabela do Notion).

## Ideias do Usuário (Enviadas)
- [x] **[ID-001] [2026-08-21]**: **Seleção Granular de Apps**: Permitir que o usuário marque/desmarque quais aplicativos deseja remover (ex: Xbox Game Bar, Spotify, Sticky Notes, Teams) em uma interface interativa. *(Concluído na v13.0)*
- [x] **[ID-002] [2026-08-21]**: **Tratamento Especial do Xbox e Popups**: Implementar o redirecionamento de protocolo `ms-gamingoverlay` e `ms-gamebar` para `systray.exe`, eliminando popups de erro para gamers. *(Concluído na v13.0)*
- [x] **[ID-003] [2026-08-21]**: **Segurança, Rollback e Ponto de Restauração**: Criar ponto de restauração do sistema automático antes de otimizações críticas e fornecer mecanismo de restauração/desfazer (Undo). *(Concluído na v13.0)*
- [x] **[ID-004] [2026-08-21]**: **Modularização do Repositório para Open-Source**: Estruturar o projeto com separação clara de módulos no GitHub (`/src`, `/Config`, etc.) para receber contribuições da comunidade. *(Concluído na v13.0)*
- [x] **[ID-005] [2026-08-21]**: **Distribuição Bimodal**: Suporte a execução tanto via launcher único portátil (`Otimizador.cmd`) quanto via comando web direto (`irm | iex`). *(Concluído na v13.0)*

## Sugestões da IA e Achados de Auditoria
- [x] **[ID-006] [2026-08-21]**: **Interface Gráfica WPF Dark Mode Nativa**: Modal XAML embutido em PowerShell (sem dependências externas) com caixas de seleção, busca e categorização visual de apps e tweaks. *(Concluído na v13.0)*
- [x] **[ID-007] [2026-08-21]**: **Catálogo Declarativo JSON**: Separar a lista de apps e diretivas em arquivos JSON estruturados (`Apps.json`, `Tweaks.json`) com níveis de recomendação (`seguro`, `avançado`). *(Concluído na v13.0)*
- [x] **[ID-008] [2026-08-21]**: **Bloqueio de IA Local Avançada (Win 11 24H2+)**: Desativar Windows Recall, Click-to-Do, serviço `WSAIFabricSvc`, AI no Notepad e Paint. *(Concluído na v13.0)*
- [x] **[ID-009] [2026-08-21]**: **Modo CLI com Presets para SysAdmins**: Adicionar suporte a parâmetros de linha de comando (`-Preset Gamer`, `-Preset Corporativo`, `-Silent`). *(Concluído na v13.0)*
- [x] **[ID-010] [2026-08-21]**: **Exportação e Importação de Perfis Customizados**: Permitir que técnicos salvem presets próprios em arquivos `.json` e os carreguem em outras máquinas via linha de comando (`-Preset C:\Custom.json`). *(Importação concluída na v14.0; falta o botão de exportar na GUI.)*
- [ ] **[ID-011] [2026-08-21]**: **Detecção Inteligente de Hardware (SSD vs HDD)**: Detectar o tipo de disco do sistema com `Get-PhysicalDisk` (ou WMI equivalente em C#) para sugerir desligar o SysMain apenas em unidades mecânicas lentas (HDDs).
- [x] **[ID-012] [2026-10-21]**: **Undo Exato por Diário de Alterações**: Gravar os valores originais antes de cada mudança e restaurá-los exatamente. *(Concluído na v14.0)*
- [x] **[ID-013] [2026-10-21]**: **Modo Simulação (`-DryRun`)**: Mostrar tudo o que seria feito sem alterar o sistema. *(Concluído na v14.0)*
- [ ] **[ID-014] [2026-10-21]**: **Assinatura Digital do Executável (Authenticode)**: Assinar digitalmente o `Otimizador-Windows.exe` (e opcionalmente o `get.ps1`) com certificado confiável, além da verificação SHA256 já publicada na release (`AUD-01`).
- [ ] **[ID-015] [2026-10-21]**: **Exportar Perfil pela GUI**: Botão para salvar as caixas marcadas como um perfil `.json` reutilizável em outras máquinas.
- [x] **[ID-016] [2026-11-10]**: **Compilação Nativa em `.exe` (Zero PowerShell/Batch em Runtime)**: Migrar o motor e a UI para executável nativo C#/.NET 4.8 WPF compilado via GitHub Actions Releases, permitindo execução mesmo em estações com PowerShell bloqueado. *(Concluído na v15.0)*
- [x] **[ID-017] [2026-11-10]**: **Hardening SMB (`C$`, `D$`, `ADMIN$`)**: Desativar criação automática de compartilhamentos administrativos ocultos (`AutoShareWks`/`AutoShareServer = 0`) com reinício controlado de `LanmanServer`, verificação ativa e suporte completo ao Undo. *(Concluído na v15.0)*
- [ ] **[AUD-01] [2026-10-22]**: **Verificação de integridade não prova a origem (Baixa)**: Além do `.sha256` hospedado na mesma release para `Otimizador-Windows.exe` e validado em `get.ps1`, assinar digitalmente com Authenticode ou publicar hash externo independente.
- [x] **[AUD-02] [2026-10-22]**: **Remoções Recursivas Seguras (`ReparsePoint`)**: Validar caminhos permitidos por componentes e desvincular junções/symlinks sem percorrer destinos em `ClearSystemTempFiles`. *(Concluído na v15.0 com teste automatizado de junção NTFS)*
- [ ] **[AUD-03] [2026-11-10]**: **Hardening adicional nos workflows CI e Release (Baixa)**: Fixar versão e verificar falhas do `PSScriptAnalyzer` no `.github/workflows/ci.yml` e adicionar `concurrency` e varredura `gitleaks` no `.github/workflows/release.yml`.
- [x] **[AUD-04] [2026-11-11]**: **Limpeza de emojis na GUI WPF e documentação (Baixa)**: Remover excesso de emojis com variation selector (`FE0F`) e surrogate pairs nas abas, cards e categorias da interface (`src/UI/MainWindow.xaml`, `src/UI/MainWindowGUI.cs`) e subtítulos do repositório para manter sobriedade visual e compatibilidade tipográfica. *(Concluído em 2026-11-11)*
- [ ] **[AUD-05] [2026-11-10]**: **Liberação determinística de objetos WMI e COM (Baixa)**: Envolver coleções `ManagementObjectSearcher.Get()` (`SystemInfo.cs`, `SecurityManager.cs`, `BackupManager.cs`) em blocos `using` e liberar explícitamente objetos COM `Schedule.Service` (`ChangeJournal.cs`) com `Marshal.FinalReleaseComObject`.

## Ideias Rejeitadas / Descartadas
- *Nenhuma ideia descartada até o momento.*
