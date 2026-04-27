<div align="center">
  <h1>⚡ Otimizador de Windows</h1>
  <p><i>Script de automação (Batch/PowerShell) focado em <b>Alta Disponibilidade (HA)</b> e <b>Performance Infraestrutural</b>.</i></p>
  
  ![Windows 10/11](https://img.shields.io/badge/OS-Windows_10%20%7C%2011-blue?style=for-the-badge&logo=windows)
  ![PowerShell](https://img.shields.io/badge/Terminal-PowerShell%205.1+-5391FE?style=for-the-badge&logo=powershell)
  ![Batch](https://img.shields.io/badge/Engine-Batch_Script-4d4d4d?style=for-the-badge&logo=gnubash)
  ![Version](https://img.shields.io/badge/Versão-v12.0-success?style=for-the-badge)
</div>

<br>

O **Otimizador de Windows** foi rigorosamente projetado para limpar, otimizar e assegurar a máxima performance e privacidade em ambientes de alta produtividade. Desenvolvido sob princípios de engenharia e performance, ele foca estritamente na redução de uso de hardware e privacidade do usuário.

---

## 🚀 Funcionalidades Principais

- 💻 **Seleção Dinâmica de Sistema Operacional:** O script se adapta caso esteja executando no Windows 10 ou Windows 11.
- ⚙️ **Dois Níveis de Refinamento (Bimodal):**
  - **1. PADRÃO (Otimizado/Leve):** Foco puro em performance diária. Mantém serviços de pesquisa e o navegador Microsoft Edge, mas remove todo o bloatware nativo e processos de fundo inúteis, otimizando CPU/RAM e desativando limitadores como Fast Startup e Xbox DVR.
  - **2. EXTREMO (Desempenho p/ Hardware Antigo):** Otimiza hardware legado (HDD, baixa densidade de RAM) desativando serviços como o _SysMain_ (Superfetch) e _Windows Search_ (indexação). Além disso, realiza a **remoção profunda do Microsoft Edge**, impedindo sua execução em segundo plano.
- 🛡️ **Privacidade e Redução de Telemetria:** Fortalece o sistema bloqueando a coleta de dados de serviços de IA locais da Microsoft, como o Recall (Win11), ClickToDo, Windows Search AI e Microsoft Copilot. Aplicações redundantes como o "Novo Outlook" também são removidas.
- 🎨 **Interface TUI (Terminal User Interface):** Interface otimizada no terminal com feedback em tempo real, utilizando cores ANSI para visualização clara do status de cada operação.
- 📝 **Logs de Auditoria:** Cada execução gera um relatório detalhado com informações do host, timestamps dinâmicos e resultados de cada rotina, salvos automaticamente na pasta `/logs/`.

---

## 📖 Manual de Operação

1. **Requisitos:** Windows 10 ou Windows 11.
2. **Execução:** Dê dois cliques no arquivo `Otimizador.cmd`. O script tentará escalar privilégios administrativos automaticamente (Zero-Touch UAC Bypass).
3. **Menu 1:** Digite `1` para Windows 10 ou `2` para Windows 11.
4. **Menu 2:** Digite `1` para Otimização Padrão ou `2` para Nível Extremo.
5. Aguarde as barras de carregamento finalizarem. No fim, a barra de tarefas (Explorer) reiniciará sozinha.
6. Confira o log gerado em `/logs/` para ter certeza de tudo que foi modificado.

---

## 📜 Changelog e Histórico

Para acompanhar todas as novidades, correções de bugs e evoluções de cada versão, consulte o documento oficial de **[Histórico de Atualizações (CHANGELOG)](./Documentação/CHANGELOG.md)**.

---

<div align="center">
  <p><i>"Maximize a eficiência do seu hardware, remova componentes redundantes e proteja sua privacidade."</i></p>
  <b>Desenvolvido por Raphael C. Martins</b>
</div>
