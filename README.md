# Otimizador Ultimate (Engine v12)

Este repositório contém o **Otimizador Ultimate**, um script poliglota (Batch/PowerShell) focado em **Alta Disponibilidade (HA)** e **Cibersegurança Defensiva**. Ele foi projetado para limpar, otimizar e assegurar a máxima performance e privacidade em ambientes corporativos e de alta produtividade (One-Hand Workflow).

## Funcionalidades Principais

- **Seleção Dinâmica de Sistema Operacional:** O script se adapta caso esteja executando no Windows 10 ou Windows 11.
- **Dois Níveis de Refinamento:**
  - **1. PADRÃO (Otimizado/Leve):** Foco puro em performance diária. Mantém serviços de pesquisa e o navegador Microsoft Edge, mas remove todo o bloatware nativo e processos de fundo inúteis, otimizando CPU/RAM e desativando limitadores como Fast Startup e Xbox DVR.
  - **2. EXTREMO (Desempenho p/ Hardware Antigo):** Otimiza hardware legado (HDD, pouca RAM) sacrificando funções convenientes como o _SysMain_ (Superfetch) e _Windows Search_ (indexação). Além disso, **efetua a remoção forçada (Brute Force) do Microsoft Edge**, impedindo-o de rodar em segundo plano.
- **Cibersegurança e Privacidade (Anti-IA):** Vacina o sistema bloqueando a coleta de dados de IAs locais da Microsoft, como o Recall (Win11), ClickToDo, Windows Search AI e Windows Copilot (Excluído massivamente). O app "Novo Outlook" também é removido ativamente.
- **Interface Gráfica Viva (TUI Premium):** O script roda nativamente no Terminal/CMD provendo uma interface lindíssima com progress bars e highlights utilizando códigos de cor ANSI truecolor, mantendo o usuário informado em tempo real sobre a integridade de cada operação.
- **Log Nativo de Auditoria:** Cada execução gera um log documentado com o "Target" (nome da máquina), data, técnico de operação e **Timestamps em tempo real (HH:mm:ss)** para todas as verificações do console. Fica salvo automaticamente de forma cega na pasta local `/logs/`.

## Manual de Operação

1. **Requisitos:** Windows 10 ou Windows 11.
2. **Execução:** Dê dois cliques no arquivo `Otimizador_Ultimate.cmd`. O script tentará escalar privilégios administrativos automaticamente se necessário (basta aceitar o UAC).
3. **Menu 1:** Digite `1` para Windows 10 ou `2` para Windows 11. Aperte ENTER.
4. **Menu 2:** Digite `1` para Otimização Padrão ou `2` para Nível Extremo. Aperte ENTER.
5. Aguarde as barras de carregamento finalizarem. No fim, a barra de tarefas (Explorer) reiniciará sozinha.
6. Confira o log gerado em `/logs/` para ter certeza de tudo que foi modificado na infraestrutura.

---

> _"Projete sistemas com mecanismos de auto-recuperação e Brutalidade Operacional."_ - Diretiva Global
