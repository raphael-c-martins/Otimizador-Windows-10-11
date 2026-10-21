<div align="center">
  <h1>⚡ Otimizador de Windows (10 & 11)</h1>
  <p><i>Debloat, privacidade e otimização com interface gráfica, simulação e reversão exata.</i></p>

  [![OS - Windows 10 | 11](https://img.shields.io/badge/OS-Windows_10_%7C_11-0078D7?style=for-the-badge&logo=windows&logoColor=white)](https://microsoft.com/windows)
  [![Windows PowerShell 5.1](https://img.shields.io/badge/Windows_PowerShell-5.1-5391FE?style=for-the-badge&logo=powershell&logoColor=white)](https://learn.microsoft.com/powershell/)
  [![CI](https://img.shields.io/github/actions/workflow/status/raphael-c-martins/Otimizador-Windows-10-11/ci.yml?branch=main&style=for-the-badge&label=CI)](https://github.com/raphael-c-martins/Otimizador-Windows-10-11/actions)
  [![Licença MIT](https://img.shields.io/badge/Licen%C3%A7a-MIT-green?style=for-the-badge)](./LICENSE)
</div>

<br>

O **Otimizador de Windows** remove bloatware, desativa telemetria e recursos de IA (Windows Recall, Copilot e Click-to-Do) e aplica ajustes de interface e desempenho em computadores pessoais e corporativos. Toda alteração fica registrada e pode ser desfeita.

---

## ⚡ Execução Rápida

Abra o **PowerShell** e cole **um dos comandos abaixo**:

### Opção 1: Comando Curto (Recomendado)
```powershell
irm https://tinyurl.com/2bsvrtpk | iex
```

### Opção 2: Comando Direto do GitHub
```powershell
irm "https://raw.githubusercontent.com/raphael-c-martins/Otimizador-Windows-10-11/main/get.ps1" | iex
```

> 💡 **Como funciona:** o script pede permissão de Administrador, baixa a **última versão publicada**, confere a integridade do pacote (SHA256), extrai em uma pasta protegida (`%ProgramData%\OtimizadorWindows\app`) e abre a interface gráfica.

---

## ✨ Funcionalidades

- 🎨 **Interface gráfica (WPF Dark Mode):** perfis com 1 clique, seleção granular de aplicativos e ajustes, busca e log ao vivo. A execução roda em segundo plano, então a janela não trava.
- 🎛️ **Seleção granular:** cada caixa de seleção corresponde a exatamente um ajuste. Nada é aplicado sem estar marcado.
- 🧪 **Modo simulação (`-DryRun`):** mostra tudo o que seria feito sem alterar o sistema.
- 💾 **Ponto de restauração confirmado:** o programa verifica se o Windows realmente criou o ponto. Se falhar, você decide se continua.
- 🔄 **Undo exato:** antes de cada mudança, o valor original de registro, serviço, tarefa agendada e plano de energia é salvo em um diário. O Undo devolve o estado exato anterior.
- 💻 **Respeita notebooks:** mantém a hibernação e não ativa o plano Ultimate Performance em equipamentos com bateria.
- 🎮 **Correção de protocolos do Xbox:** ao remover o Game Bar, os protocolos `ms-gamebar`/`ms-gamingoverlay` são neutralizados para evitar popups de erro.
- 📋 **Relatório honesto:** cada etapa termina como `OK`, `AVISO` ou `ERRO`, com resumo final e log completo.

---

## 📊 Perfis

| Perfil | Foco | Aplicativos | Xbox | OneDrive / Edge / Serviços |
| :--- | :--- | :--- | :--- | :--- |
| **Padrão** | Uso diário | Bloatware comum e patrocinados | Preservado | Remove OneDrive; mantém Edge e serviços |
| **Gamer** | Jogos | Bloatware e utilitários irrelevantes | Preservado (Game Pass, Clipchamp) | Remove OneDrive; mantém Edge e serviços |
| **Corporativo** | Trabalho | Jogos, redes sociais e patrocinados | Removido + correção de protocolos | Teams, Outlook e OneDrive preservados |
| **Extremo** | Hardware antigo / HDD | Quase todos | Removido + correção de protocolos | Remove OneDrive e Edge; desativa SysMain e WSearch |

---

## 🚀 Outras Formas de Executar

### Duplo clique (offline)
1. Baixe o `.zip` da [última release](https://github.com/raphael-c-martins/Otimizador-Windows-10-11/releases).
2. Dê duplo clique em **`Otimizador.cmd`**.

### Linha de comando (SysAdmins)
```powershell
# Modo terminal interativo
.\Run.ps1 -CLI

# Simular um perfil sem alterar nada (não exige Administrador)
.\Run.ps1 -Preset Gamer -DryRun

# Execução automatizada (exige PowerShell como Administrador)
.\Run.ps1 -Preset Corporativo -Silent

# Perfil personalizado em arquivo .json
.\Run.ps1 -Preset C:\Perfis\MeuPerfil.json -Silent

# Desfazer as otimizações
.\Run.ps1 -Undo
```

| Parâmetro | Função |
| :--- | :--- |
| `-CLI` | Abre o modo terminal em vez da janela gráfica. |
| `-Preset <nome ou .json>` | Perfil a aplicar (`Padrao`, `Gamer`, `Corporativo`, `Extremo` ou caminho de um arquivo). |
| `-Silent` | Sem perguntas. Exige `-Preset` ou `-Undo`. |
| `-DryRun` | Simulação: mostra as etapas sem alterar nada. |
| `-CustomApps "Id1,Id2"` | Substitui a lista de aplicativos do perfil (IDs do `Config/Apps.json`). |
| `-SkipRestorePoint` | Não cria ponto de restauração. |
| `-ContinueWithoutRestorePoint` | No modo `-Silent`, continua mesmo se o ponto de restauração falhar. |
| `-Undo` | Reverte as otimizações usando os diários de alteração. |

**Códigos de saída:** `0` sucesso · `1` erro de uso ou falta de permissão · `2` concluído com etapas com erro · `3` abortado porque o ponto de restauração falhou.

---

## 📁 Onde ficam os dados

| Pasta | Conteúdo |
| :--- | :--- |
| `%ProgramData%\OtimizadorWindows\logs` | Log de cada execução. |
| `%ProgramData%\OtimizadorWindows\estado` | Diários de alteração usados pelo Undo. **Não apague.** |
| `%ProgramData%\OtimizadorWindows\app` | Cópia do programa baixada pelo `get.ps1`. |
| `%TEMP%\OtimizadorWindows-simulacao` | Logs do modo simulação. |

A pasta `%ProgramData%\OtimizadorWindows` só aceita escrita de Administradores e do SYSTEM.

---

## 📂 Estrutura do Repositório

```
Otimizador-Windows-10-11/
├── get.ps1                        # Bootstrap web (irm | iex) com verificação SHA256
├── Otimizador.cmd                 # Launcher com elevação UAC
├── Run.ps1                        # Orquestrador: GUI, terminal, automação, simulação e Undo
├── Config/
│   ├── Apps.json                  # Catálogo de aplicativos
│   ├── Tweaks.json                # Catálogo de ajustes (cada um aponta para sua função)
│   └── Presets/                   # Perfis (Padrao, Gamer, Corporativo, Extremo)
├── src/
│   ├── Core/
│   │   ├── OptimizationEngine.ps1 # Motor único de execução (usado pela GUI e pelo terminal)
│   │   ├── ChangeJournal.ps1      # Alterações rastreáveis e diário para o Undo
│   │   ├── Logger.ps1             # Log em arquivo e saída em tela
│   │   ├── SystemInfo.ps1         # Administrador, versão do Windows, notebook e usuário conectado
│   │   ├── ThreadingManager.ps1   # Execução em segundo plano para a GUI
│   │   ├── AppManager.ps1         # Remoção de aplicativos (Appx + WinGet) e OneDrive
│   │   ├── PrivacyManager.ps1     # Telemetria, anúncios, Recall e Copilot
│   │   ├── PerformanceManager.ps1 # Interface, energia, serviços, Edge e limpeza
│   │   ├── GamingManager.ps1      # GameDVR e protocolos do Xbox
│   │   ├── BackupManager.ps1      # Ponto de restauração confirmado
│   │   ├── UndoManager.ps1        # Reversão por diário (ou padrões do Windows)
│   │   └── Constants.ps1          # Versão e pastas de dados
│   └── UI/
│       ├── MainWindowGUI.ps1      # Janela principal
│       ├── ConsoleTUI.ps1         # Modo terminal
│       └── AppSelectionModal.ps1  # Seleção de aplicativos no modo terminal
├── tests/                         # Testes automatizados (Pester)
├── docs/
│   ├── CHANGELOG.md               # Histórico de versões
│   ├── HISTORICO.md               # Decisões de arquitetura e diretrizes
│   └── ideias-pendentes.md        # Backlog de ideias
└── .github/workflows/             # CI (testes) e Release (pacote + SHA256)
```

---

## 🧪 Desenvolvimento

```powershell
# Testes (Windows PowerShell 5.1, que já traz o Pester 3.4). Não alteram o sistema.
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Invoke-Testes.ps1

# Ver o que um perfil faria, sem alterar nada
.\Run.ps1 -Preset Extremo -DryRun
```

**Adicionar um ajuste novo:** crie a função em `src/Core` usando `Set-RegistryValue`, `Set-ServiceStartupTracked` ou `Disable-ScheduledTaskTracked` (assim ela entra no diário do Undo) e registre o ajuste no `Config/Tweaks.json` com o campo `Function`. Os testes verificam se a função existe.

**Publicar uma versão:** crie e envie uma tag (`git tag v14.0.0` e depois `git push origin v14.0.0`). O workflow de release roda os testes, gera o `.zip` com o `.sha256` e cria a release usada pelo `get.ps1`.

---

## 🤝 Contribuições

1. Faça um Fork do projeto.
2. Crie uma Branch (`git checkout -b feature/NovaOtimizacao`).
3. Rode os testes e confira o resultado com `-DryRun`.
4. Abra um Pull Request.

---

## 📄 Licença

Distribuído sob a licença **MIT**. Veja o arquivo [LICENSE](./LICENSE).
