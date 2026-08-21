<div align="center">
  <h1>⚡ Otimizador de Windows (10 & 11)</h1>
  <p><i>Suíte modular de automação, debloat de alta performance, privacidade e interface gráfica moderna.</i></p>
  
  [![OS - Windows 10 | 11](https://img.shields.io/badge/OS-Windows_10_%7C_11-0078D7?style=for-the-badge&logo=windows&logoColor=white)](https://microsoft.com/windows)
  [![PowerShell 5.1+](https://img.shields.io/badge/PowerShell-5.1%2B-5391FE?style=for-the-badge&logo=powershell&logoColor=white)](https://github.com/PowerShell/PowerShell)
  [![Versão](https://img.shields.io/badge/Versão-v13.0_Modular-success?style=for-the-badge)](./Documentação/CHANGELOG.md)
  [![Licença MIT](https://img.shields.io/badge/Licen%C3%A7a-MIT-green?style=for-the-badge)](./LICENSE)
</div>

<br>

O **Otimizador de Windows** é uma solução profissional e de código aberto projetada para remover bloatware nativo, desativar telemetrias invasivas e recursos redundantes de IA (como Windows Recall, Copilot e Click-to-Do), restaurar a fluidez do sistema e maximizar o desempenho em computadores pessoais e corporativos.

---

## ✨ Funcionalidades Principais

- 🎨 **Interface Gráfica Completa (WPF Dark Mode):** Janela moderna no estilo Fluent Design com menu lateral (Sidebar), seleção de perfis rápidos com 1 clique, busca em tempo real e visualização de logs ao vivo.
- ⚡ **Threading Não-Bloqueante (60 FPS):** Execução assíncrona com bombeamento de UI que garante total fluidez e impede travamentos (*"Não está respondendo"*).
- 🎛️ **Seleção Granular de Aplicativos (35 Apps):** Painel categorizado para você escolher exatamente o que manter ou desinstalar (Xbox Game Bar, Teams, Copilot, Spotify, Clipchamp, etc.).
- 🎮 **Engenharia para Jogos (Anti-Popup Protocol Fix):** Redirecionamento seguro de protocolos (`ms-gamingoverlay` e `ms-gamebar`) para evitar erros no Windows após a remoção do Game Bar.
- 🛡️ **Privacidade & Bloqueio de IA Local:** Desativação de telemetria diagnóstica (DiagTrack), anúncios do sistema, histórico de atividades, Windows Recall, Click-to-Do e Microsoft Copilot.
- 💾 **Ponto de Restauração Automático:** Criação de ponto de restauração (`Checkpoint-Computer`) antes de qualquer alteração de baixo nível.
- 🔄 **Módulo de Reversão (Undo Engine):** Capacidade de restaurar serviços padrão, menu nativo do Windows 11 e configurações a qualquer momento.
- ⚡ **Quatro Perfis de Otimização Especializados:** Presets declarativos em JSON prontos para diferentes cenários de uso.

---

## 📊 Tabela Comparativa de Perfis (Presets)

| Perfil | Foco Principal | Aplicativos Removidos | Xbox & Jogos | Edge & Serviços Legados |
| :--- | :--- | :--- | :--- | :--- |
| **Padrão** | Uso Diário / Equilibrado | Bloatware comum e patrocinados | Preserva Xbox / Desativa GameDVR | Mantém Edge e WSearch |
| **Gamer** | Jogos & Máximo FPS | Bloatware e utilitários irrelevantes | Preserva Game Pass / Desativa GameDVR | Mantém Edge e WSearch |
| **Corporativo** | Trabalho & Produtividade | Jogos, redes sociais e telemetria | Remove ecossistema de jogos | Preserva Teams e Office |
| **Extremo** | Hardware Antigo / HDDs | Remoção completa de quase todos apps | Remove Xbox + Protocol Fix | Desativa SysMain e WSearch + Remove Edge |

---

## 🚀 Como Executar

### Método 1: Duplo Clique (Interface Gráfica Moderna)
1. Dê duplo clique no arquivo **`Otimizador.cmd`**.
2. Aceite o prompt de confirmação de Administrador (UAC).
3. A janela gráfica moderna em Dark Mode abrirá na sua área de trabalho.
4. Escolha o perfil desejado na primeira aba e clique em **`⚡ EXECUTAR OTIMIZAÇÃO`**.

### Método 2: Modo Terminal (CLI Interativo ou Automatizado)
Abra o **PowerShell como Administrador** na pasta do projeto e execute:

```powershell
# Execução no modo Terminal TUI
.\Run.ps1 -CLI

# Execução automatizada com perfil Gamer
.\Run.ps1 -Preset Gamer -Silent

# Executar modo de restauração (Undo)
.\Run.ps1 -Undo
```

---

## 📂 Estrutura do Repositório

```
Otimizador-Windows-10-11/
├── Otimizador.cmd                 # Launcher principal com auto-elevação UAC
├── Run.ps1                        # Orquestrador nativo em PowerShell e CLI
├── Config/
│   ├── Apps.json                  # Catálogo declarativo de 35 aplicativos
│   ├── Tweaks.json                # Catálogo de diretivas de sistema e registro
│   └── Presets/                   # Perfis prontos (Padrao, Gamer, Corporativo, Extremo)
├── src/
│   ├── Core/                      # Módulos de regras de negócio e execução
│   │   ├── ThreadingManager.ps1   # Bombeamento de UI (DoEvents) e execução assíncrona
│   │   ├── AppManager.ps1         # Desinstalação híbrida Appx + WinGet com cache DISM
│   │   ├── GamingManager.ps1      # Otimizações de jogos e fix de protocolos
│   │   ├── PrivacyManager.ps1     # Telemetria, Recall e bloqueio de IA
│   │   ├── PerformanceManager.ps1 # Tweaks de Explorer, energia e SysMain
│   │   ├── BackupManager.ps1      # Criação de ponto de restauração não-bloqueante
│   │   └── UndoManager.ps1        # Módulo de reversão e restauração
│   └── UI/                        # Interfaces de usuário
│       ├── MainWindowGUI.ps1      # Janela gráfica principal moderna (WPF Dark Mode)
│       ├── ConsoleTUI.ps1         # Menu ANSI de alta densidade
│       └── AppSelectionModal.ps1  # Modal gráfico auxiliar
├── Documentação/
│   ├── CHANGELOG.md               # Histórico de versões
│   └── HISTORICO.md               # Diário arquitetural e diretrizes de segurança
└── logs/                          # Logs de auditoria gerados por execução
```

---

## 🤝 Contribuições

Contribuições da comunidade são muito bem-vindas!
1. Faça um Fork do projeto.
2. Crie uma Branch para sua feature (`git checkout -b feature/NovaOtimizacao`).
3. Faça o Commit de suas alterações (`git commit -m 'Adiciona novo tweak de privacidade'`).
4. Envie para o GitHub (`git push origin feature/NovaOtimizacao`).
5. Abra um Pull Request.

---

## 📄 Licença

Distribuído sob a licença **MIT**. Veja o arquivo [LICENSE](./LICENSE) para mais detalhes.
