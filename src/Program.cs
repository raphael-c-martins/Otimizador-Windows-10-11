using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Otimizador.Core;
using Otimizador.UI;

namespace Otimizador
{
    public sealed class CliOptions
    {
        public bool Cli { get; set; }
        public string Preset { get; set; }
        public bool Silent { get; set; }
        public bool SkipRestorePoint { get; set; }
        public bool ContinueWithoutRestorePoint { get; set; }
        public bool Undo { get; set; }
        public string CustomApps { get; set; }
        public bool DryRun { get; set; }
        public bool Help { get; set; }
        public string ParseError { get; set; }
    }

    public static class Program
    {
        private const int AttachParentProcess = -1;
        private const int StdOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        public static CliOptions ParseArguments(string[] args)
        {
            var opts = new CliOptions();
            if (args == null) return opts;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;
                string lower = arg.ToLowerInvariant();

                if (lower == "-cli" || lower == "--cli") opts.Cli = true;
                else if (lower == "-silent" || lower == "--silent") opts.Silent = true;
                else if (lower == "-skiprestorepoint" || lower == "--skip-restore-point") opts.SkipRestorePoint = true;
                else if (lower == "-continuewithoutrestorepoint" || lower == "--continue-without-restore-point") opts.ContinueWithoutRestorePoint = true;
                else if (lower == "-undo" || lower == "--undo") opts.Undo = true;
                else if (lower == "-dryrun" || lower == "--dry-run") opts.DryRun = true;
                else if (lower == "-help" || lower == "--help" || lower == "-h" || lower == "/?") opts.Help = true;
                else if (lower == "-preset" || lower == "--preset")
                {
                    if (i + 1 >= args.Length) { opts.ParseError = "Parâmetro -Preset exige um nome ou caminho de arquivo .json."; return opts; }
                    opts.Preset = args[++i];
                }
                else if (lower == "-customapps" || lower == "--custom-apps")
                {
                    if (i + 1 >= args.Length) { opts.ParseError = "Parâmetro -CustomApps exige uma lista de IDs separada por vírgula."; return opts; }
                    opts.CustomApps = args[++i];
                }
                else
                {
                    opts.ParseError = string.Format("Parâmetro desconhecido: '{0}'. Use -Help para ver as opções.", arg);
                    return opts;
                }
            }

            return opts;
        }

        private static void EnsureConsole(bool allowAlloc)
        {
            ConsoleTUI.UseAnsiColors = false;
            try
            {
                if (!Console.IsOutputRedirected && GetStdHandle(StdOutputHandle) == IntPtr.Zero)
                {
                    if (!AttachConsole(AttachParentProcess) && allowAlloc)
                    {
                        AllocConsole();
                    }
                }

                if (!Console.IsOutputRedirected)
                {
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.InputEncoding = Encoding.UTF8;
                    IntPtr hOut = GetStdHandle(StdOutputHandle);
                    uint mode;
                    if (hOut != IntPtr.Zero && GetConsoleMode(hOut, out mode))
                    {
                        if ((mode & EnableVirtualTerminalProcessing) != 0 || SetConsoleMode(hOut, mode | EnableVirtualTerminalProcessing))
                        {
                            ConsoleTUI.UseAnsiColors = true;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public static string BuildQuotedArguments(string[] args)
        {
            if (args == null || args.Length == 0) return string.Empty;
            var parts = new List<string>();
            foreach (string arg in args)
            {
                if (string.IsNullOrEmpty(arg)) { parts.Add("\"\""); continue; }
                if (arg.IndexOfAny(new char[] { ' ', '\t', '"' }) < 0)
                {
                    parts.Add(arg);
                    continue;
                }

                var sb = new StringBuilder();
                sb.Append('"');
                int backslashes = 0;
                foreach (char ch in arg)
                {
                    if (ch == '\\')
                    {
                        backslashes++;
                    }
                    else if (ch == '"')
                    {
                        sb.Append('\\', backslashes * 2 + 1);
                        sb.Append('"');
                        backslashes = 0;
                    }
                    else
                    {
                        if (backslashes > 0)
                        {
                            sb.Append('\\', backslashes);
                            backslashes = 0;
                        }
                        sb.Append(ch);
                    }
                }
                if (backslashes > 0)
                {
                    sb.Append('\\', backslashes * 2);
                }
                sb.Append('"');
                parts.Add(sb.ToString());
            }
            return string.Join(" ", parts.ToArray());
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Otimizador de Windows v" + Constants.OptimizerVersion);
            Console.WriteLine();
            Console.WriteLine("Uso: Otimizador-Windows.exe [opções]");
            Console.WriteLine("  (sem parâmetros)             Abre a interface gráfica (GUI).");
            Console.WriteLine("  -CLI                         Abre o modo terminal interativo.");
            Console.WriteLine("  -Preset <nome ou .json>      Padrao, Gamer, Corporativo, Extremo ou caminho .json.");
            Console.WriteLine("  -Silent                      Execução automatizada sem perguntas (exige -Preset ou -Undo).");
            Console.WriteLine("  -DryRun                      Simulação: mostra o que seria feito sem alterar o sistema.");
            Console.WriteLine("  -CustomApps \"Id1,Id2\"        Substitui a lista de aplicativos do perfil.");
            Console.WriteLine("  -SkipRestorePoint            Não cria ponto de restauração.");
            Console.WriteLine("  -ContinueWithoutRestorePoint Continua no modo -Silent se o ponto de restauração falhar.");
            Console.WriteLine("  -Undo                        Reverte otimizações usando o diário de alterações.");
            Console.WriteLine("  -Help                        Exibe esta ajuda.");
        }

        [STAThread]
        public static int Main(string[] args)
        {
            CliOptions opts = ParseArguments(args);
            bool useGui = !opts.Cli && !opts.Silent && !opts.Undo && string.IsNullOrWhiteSpace(opts.Preset) && !opts.Help && string.IsNullOrWhiteSpace(opts.ParseError);

            if (!useGui)
            {
                EnsureConsole(!opts.Silent);
            }

            if (!string.IsNullOrWhiteSpace(opts.ParseError))
            {
                Console.Error.WriteLine("[ERRO] " + opts.ParseError);
                return ExitCodes.ErroDeUso;
            }

            if (opts.Help)
            {
                PrintHelp();
                if (!Console.IsInputRedirected && opts.Cli)
                {
                    Console.WriteLine("\nPressione ENTER para sair...");
                    Console.ReadLine();
                }
                return ExitCodes.Sucesso;
            }

            if (opts.Silent && !opts.Undo && string.IsNullOrWhiteSpace(opts.Preset))
            {
                Console.Error.WriteLine("[ERRO] -Silent exige -Preset <nome ou arquivo .json> (ou -Undo). Nada foi alterado.");
                return ExitCodes.ErroDeUso;
            }

            if (!opts.DryRun && !SystemInfo.TestIsAdministrator())
            {
                if (opts.Silent)
                {
                    Console.Error.WriteLine("[ERRO] Execute como Administrador. Nada foi alterado.");
                    return ExitCodes.ErroDeUso;
                }

                try
                {
                    string exePath = Assembly.GetExecutingAssembly().Location;
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = BuildQuotedArguments(args),
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return ExitCodes.Sucesso;
                }
                catch (Win32Exception)
                {
                    string deniedMsg = "A permissão de Administrador foi negada. Nada foi alterado.";
                    if (useGui)
                    {
                        MessageBox.Show(deniedMsg, "Permissão necessária", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        Console.Error.WriteLine("[ERRO] " + deniedMsg);
                    }
                    return ExitCodes.ErroDeUso;
                }
            }

            string dataRoot = opts.DryRun ? Constants.OptimizerDryRunRoot : Constants.OptimizerDataRoot;
            try
            {
                if (opts.DryRun)
                {
                    if (!Directory.Exists(dataRoot)) Directory.CreateDirectory(dataRoot);
                }
                else
                {
                    Logger.InitializeSecureDataDirectory(dataRoot);
                }
            }
            catch (Exception ex)
            {
                string err = string.Format("Não foi possível preparar a pasta de dados '{0}': {1}", dataRoot, ex.Message);
                if (useGui) MessageBox.Show(err, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                else Console.Error.WriteLine("[ERRO] " + err);
                return ExitCodes.ErroDeUso;
            }

            string logDir = Path.Combine(dataRoot, "logs");
            string journalDir = Path.Combine(dataRoot, "estado");
            string logPath = Logger.NewOptimizerLogFile(logDir);

            if (useGui)
            {
                Logger.SetTarget(logPath, null, null);
            }
            else
            {
                Logger.SetTarget(logPath, ConsoleTUI.WriteLogEntry, ConsoleTUI.WriteProgress);
            }

            OSInfo os;
            bool isPortable;
            UserTargetInfo userTarget;
            ExecutionContext context;
            OptimizerCatalog catalog;
            string accessibleLogPath = null;
            string displayLogPath = logPath;

            try
            {
                os = SystemInfo.GetOSInfo();
                isPortable = SystemInfo.TestIsPortableDevice();
                userTarget = opts.DryRun
                    ? new UserTargetInfo { Sid = null, Message = "Simulação: nenhum perfil de usuário será alterado.", IsWarning = false }
                    : SystemInfo.ResolveUserRegistrySid();

                string docsLogsDir = SystemInfo.ResolveUserDocumentsLogsDirectory(userTarget.Sid);
                if (!string.IsNullOrWhiteSpace(docsLogsDir))
                {
                    accessibleLogPath = Logger.SetAccessibleLogTarget(Path.Combine(docsLogsDir, Path.GetFileName(logPath)));
                    if (!string.IsNullOrWhiteSpace(accessibleLogPath))
                    {
                        displayLogPath = accessibleLogPath;
                    }
                }

                Logger.Write("INFO", string.Format("Otimizador de Windows v{0} | {1} (build {2}) | {3}{4}",
                    Constants.OptimizerVersion,
                    os.Caption,
                    os.Build,
                    isPortable ? "Notebook" : "Desktop",
                    opts.DryRun ? " | MODO SIMULAÇÃO" : string.Empty));
                Logger.Write(userTarget.IsWarning ? "AVISO" : "INFO", userTarget.Message);

                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                context = new ExecutionContext
                {
                    IsWin11 = os.IsWin11,
                    IsServer = os.IsServer,
                    IsPortable = isPortable,
                    ProtectedPaths = new List<string> { exeDir, dataRoot },
                    StateDirectory = dataRoot,
                    JournalDirectory = journalDir,
                    UserSid = userTarget.Sid,
                    RestartExplorer = true
                };

                catalog = OptimizationEngine.GetCatalog(null);
            }
            catch (Exception ex)
            {
                Logger.Write("ERRO", string.Format("Falha ao inicializar: {0}", ex.Message));
                if (useGui) MessageBox.Show("Falha ao inicializar: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return ExitCodes.ErroDeUso;
            }

            bool interactive = !opts.Silent;

            Func<int> runUndoFlow = delegate
            {
                ConsoleTUI.ShowMainHeader("RESTAURAÇÃO DO SISTEMA (UNDO)", os.Caption);
                ChangeJournal.SetUserRegistryTarget(context.UserSid);
                UndoResult undoResult;
                try
                {
                    undoResult = UndoManager.InvokeSystemUndo(journalDir, os.IsWin11, opts.DryRun);
                }
                finally
                {
                    ChangeJournal.SetUserRegistryTarget(null);
                }

                Console.ForegroundColor = undoResult.Failed > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                Console.WriteLine(string.Format("\n Restauração: {0} itens revertidos, {1} com erro. Log: {2}",
                    undoResult.Restored, undoResult.Failed, displayLogPath));
                Console.ResetColor();
                if (interactive) ConsoleTUI.PromptInput("\n Pressione ENTER para sair");
                return undoResult.Failed > 0 ? ExitCodes.FalhasParciais : ExitCodes.Sucesso;
            };

            if (opts.Undo)
            {
                return runUndoFlow();
            }

            if (useGui)
            {
                try
                {
                    MainWindowGUI.ShowMainWindow(catalog, context, displayLogPath, os.Caption, opts.DryRun);
                    return ExitCodes.Sucesso;
                }
                catch (Exception ex)
                {
                    Logger.Write("ERRO", string.Format("A interface gráfica não pôde ser iniciada: {0}", ex.Message));
                    EnsureConsole(true);
                    Console.WriteLine("\n [AVISO] Iniciando no modo terminal...\n");
                    Logger.SetTarget(logPath, ConsoleTUI.WriteLogEntry, ConsoleTUI.WriteProgress);
                    if (!string.IsNullOrWhiteSpace(accessibleLogPath))
                    {
                        Logger.SetAccessibleLogTarget(accessibleLogPath);
                    }
                }
            }

            OptimizerPreset selectedPreset = null;
            string[] appIds;
            string[] tweakIds;

            try
            {
                if (string.IsNullOrWhiteSpace(opts.Preset))
                {
                    var presetMenu = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { "1", "Padrao" },
                        { "2", "Gamer" },
                        { "3", "Corporativo" },
                        { "4", "Extremo" }
                    };

                    string selectedName = null;
                    while (selectedName == null)
                    {
                        ConsoleTUI.ShowMainHeader("PROTOCOLO DE OTIMIZAÇÃO & DEBLOAT" + (opts.DryRun ? " [SIMULAÇÃO]" : string.Empty), os.Caption);
                        Console.WriteLine(ConsoleTUI.C_GN + ">> PASSO 1: SELECIONE O PERFIL DE OTIMIZAÇÃO" + ConsoleTUI.C_RST + "\n");
                        Console.WriteLine(ConsoleTUI.C_W + " [1] PADRÃO (Recomendado / Equilibrado)" + ConsoleTUI.C_RST);
                        Console.WriteLine("     - Limpeza de bloatware, bloqueio de compartilhamentos ocultos e ajustes de interface/energia.\n");
                        Console.WriteLine(ConsoleTUI.C_C + " [2] GAMER (Otimizado p/ Jogos & FPS)" + ConsoleTUI.C_RST);
                        Console.WriteLine("     - Preserva Game Pass, Xbox App e players de mídia, desativa GameDVR e ativa Ultimate Performance.\n");
                        Console.WriteLine(ConsoleTUI.C_P + " [3] CORPORATIVO (Escritório & Produtividade)" + ConsoleTUI.C_RST);
                        Console.WriteLine("     - Preserva Teams, Outlook e OneDrive, remove jogos e aplica privacidade e cibersegurança.\n");
                        Console.WriteLine(ConsoleTUI.C_R + " [4] EXTREMO (Desempenho Máximo p/ Hardware Antigo)" + ConsoleTUI.C_RST);
                        Console.WriteLine("     - Desativa SysMain/WSearch e remove o Microsoft Edge.\n");
                        Console.WriteLine(ConsoleTUI.C_Y + " [5] RESTAURAR / DESFAZER OTIMIZAÇÕES (Undo)" + ConsoleTUI.C_RST);
                        Console.WriteLine("     - Devolve registro, serviços, tarefas e energia ao estado anterior às otimizações.\n");

                        string choice = ConsoleTUI.PromptInput(ConsoleTUI.C_C + " [?] Digite sua opção (1-5): " + ConsoleTUI.C_RST);
                        if (choice == "5") return runUndoFlow();

                        string matchedPreset;
                        if (presetMenu.TryGetValue(choice, out matchedPreset))
                        {
                            selectedName = matchedPreset;
                        }
                        else
                        {
                            Console.WriteLine("\n" + ConsoleTUI.C_R + " [!] Opção inválida, por favor selecione um número de 1 a 5." + ConsoleTUI.C_RST);
                            System.Threading.Thread.Sleep(2000);
                        }
                    }

                    selectedPreset = OptimizationEngine.ImportPreset(catalog, selectedName);
                    appIds = selectedPreset.RemoveApps != null ? selectedPreset.RemoveApps.ToArray() : new string[0];

                    ConsoleTUI.ShowMainHeader("PROTOCOLO DE OTIMIZAÇÃO & DEBLOAT", os.Caption);
                    Console.WriteLine(string.Format("{0} [ INFO ] Perfil selecionado: {1}{2}\n", ConsoleTUI.C_C, selectedPreset.DisplayName, ConsoleTUI.C_RST));
                    Console.WriteLine(ConsoleTUI.C_GN + ">> PASSO 2: PERSONALIZAÇÃO DE APLICATIVOS (DEBLOAT)" + ConsoleTUI.C_RST + "\n");
                    if (ConsoleTUI.ReadYesNo("Deseja abrir a janela para marcar/desmarcar aplicativos individualmente?"))
                    {
                        string[] customSelection = AppSelectionModal.ShowDialog(catalog.Apps, appIds);
                        if (customSelection != null) appIds = customSelection;
                    }
                }
                else
                {
                    selectedPreset = OptimizationEngine.ImportPreset(catalog, opts.Preset);
                    appIds = selectedPreset.RemoveApps != null ? selectedPreset.RemoveApps.ToArray() : new string[0];
                }

                if (!string.IsNullOrWhiteSpace(opts.CustomApps))
                {
                    var customList = new List<string>();
                    foreach (string part in opts.CustomApps.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string trimmed = part.Trim();
                        if (!string.IsNullOrEmpty(trimmed)) customList.Add(trimmed);
                    }
                    appIds = customList.ToArray();
                }

                tweakIds = selectedPreset.ApplyTweaks != null ? selectedPreset.ApplyTweaks.ToArray() : new string[0];
                OptimizationEngine.AssertSelection(catalog, appIds, tweakIds);
            }
            catch (Exception ex)
            {
                Logger.Write("ERRO", ex.Message);
                return ExitCodes.ErroDeUso;
            }

            string presetLabel = string.IsNullOrWhiteSpace(selectedPreset.PresetName) ? "Personalizado" : selectedPreset.PresetName;

            if (interactive)
            {
                ConsoleTUI.ShowMainHeader("CONFIRMAÇÃO: " + selectedPreset.DisplayName, os.Caption);
                var appIdSet = new HashSet<string>(appIds, StringComparer.OrdinalIgnoreCase);
                var tweakIdSet = new HashSet<string>(tweakIds, StringComparer.OrdinalIgnoreCase);

                Console.WriteLine(string.Format("{0} Aplicativos a remover ({1}):{2}", ConsoleTUI.C_W, appIds.Length, ConsoleTUI.C_RST));
                foreach (AppItem app in catalog.Apps)
                {
                    if (appIdSet.Contains(app.Id)) Console.WriteLine(string.Format("{0}   - {1}{2}", ConsoleTUI.C_G, app.FriendlyName, ConsoleTUI.C_RST));
                }

                Console.WriteLine(string.Format("\n{0} Ajustes a aplicar ({1}):{2}", ConsoleTUI.C_W, tweakIds.Length, ConsoleTUI.C_RST));
                foreach (TweakItem tweak in catalog.Tweaks)
                {
                    if (tweakIdSet.Contains(tweak.Id)) Console.WriteLine(string.Format("{0}   - {1}{2}", ConsoleTUI.C_G, tweak.Name, ConsoleTUI.C_RST));
                }

                Console.WriteLine();
                if (!ConsoleTUI.ReadYesNo("Confirmar a execução" + (opts.DryRun ? " da SIMULAÇÃO" : string.Empty) + "?"))
                {
                    Logger.Write("INFO", "Execução cancelada pelo usuário. Nada foi alterado.");
                    return ExitCodes.Sucesso;
                }

                ConsoleTUI.ShowMainHeader("EXECUTANDO: " + selectedPreset.DisplayName + (opts.DryRun ? " [SIMULAÇÃO]" : string.Empty), os.Caption);
            }

            if (opts.DryRun)
            {
                Logger.Write("INFO", "[SIMULAÇÃO] Ponto de Restauração não criado.");
            }
            else if (opts.SkipRestorePoint)
            {
                Logger.Write("AVISO", "Ponto de Restauração ignorado a pedido (-SkipRestorePoint).");
            }
            else
            {
                try
                {
                    BackupManager.InvokeSystemRestorePoint(BackupManager.NewRestorePointDescription(presetLabel));
                }
                catch (RestorePointRecentAvailableException recentEx)
                {
                    Logger.Write("AVISO", string.Format("Ponto de Restauração recente disponível: {0}", recentEx.Message));
                    bool continueAnyway = opts.ContinueWithoutRestorePoint;
                    if (!continueAnyway && interactive)
                    {
                        continueAnyway = ConsoleTUI.ReadYesNo(string.Format("Continuar usando o ponto recente '{0}' ({1:dd/MM/yyyy HH:mm})?",
                            recentEx.RecentPoint.Description, recentEx.RecentPoint.CreationTime));
                    }
                    if (!continueAnyway)
                    {
                        Logger.Write("AVISO", "Otimização cancelada pelo usuário. Nada foi alterado.");
                        return ExitCodes.AbortadoSemBackup;
                    }
                    Logger.Write("INFO", string.Format("Continuando com o Ponto de Restauração recente '{0}' como salvaguarda.", recentEx.RecentPoint.Description));
                }
                catch (Exception ex)
                {
                    Logger.Write("ERRO", string.Format("Ponto de Restauração não criado: {0}", ex.Message));
                    bool continueAnyway = opts.ContinueWithoutRestorePoint;
                    if (!continueAnyway && interactive)
                    {
                        continueAnyway = ConsoleTUI.ReadYesNo("Continuar SEM ponto de restauração?");
                    }
                    if (!continueAnyway)
                    {
                        Logger.Write("AVISO", "Otimização abortada para manter a segurança do sistema. Nada foi alterado.");
                        return ExitCodes.AbortadoSemBackup;
                    }
                    Logger.Write("AVISO", "Continuando sem Ponto de Restauração por decisão do usuário.");
                }
            }

            ExecutionResult execResult;
            try
            {
                execResult = OptimizationEngine.InvokeOptimizationPlan(catalog, appIds, tweakIds, context, opts.DryRun);
            }
            catch (Exception ex)
            {
                Logger.Write("ERRO", string.Format("Falha inesperada na execução: {0}", ex.Message));
                return ExitCodes.ErroDeUso;
            }

            ConsoleTUI.ShowExecutionSummary(execResult, displayLogPath);
            if (interactive) ConsoleTUI.PromptInput("\n Pressione ENTER para finalizar");

            return execResult.Failed > 0 ? ExitCodes.FalhasParciais : ExitCodes.Sucesso;
        }
    }
}
