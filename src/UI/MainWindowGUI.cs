using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using Otimizador.Core;

namespace Otimizador.UI
{
    public sealed class MainWindowHandles
    {
        public Window Window { get; set; }
        public Dictionary<string, FrameworkElement> Controls { get; set; }
    }

    internal sealed class BackgroundEvent
    {
        public string Type { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public DateTime Time { get; set; }
        public int Current { get; set; }
        public int Total { get; set; }
        public string Activity { get; set; }
    }

    internal sealed class RestorePointTaskResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public static class MainWindowGUI
    {
        private static string LoadMainWindowXaml()
        {
            Assembly asm = typeof(MainWindowGUI).Assembly;
            using (Stream stream = asm.GetManifestResourceStream("Otimizador.UI.MainWindow.xaml"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Recurso embutido 'Otimizador.UI.MainWindow.xaml' não encontrado no executável.");
                }
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static readonly string[] ControlNames = new string[]
        {
            "DryRunBadge", "HostInfoText", "UserInfoText",
            "NavBtnPresets", "NavBtnApps", "NavBtnPrivacy", "NavBtnPerformance", "NavBtnLogs", "NavBtnUndo",
            "PanelPresets", "PanelApps", "PanelPrivacy", "PanelPerformance", "PanelLogs", "PanelUndo",
            "AppsContainerList", "PrivacyTweaksContainer", "SystemTweaksContainer", "AppSearchBox",
            "BtnAppsSelectDefault", "BtnAppsSelectAll", "BtnAppsDeselectAll",
            "BtnPresetPadrao", "BtnPresetGamer", "BtnPresetCorp", "BtnPresetExtremo",
            "BtnUndoAction", "BtnApplyOptimization", "BtnCloseWindow", "BtnOpenLogsFolder",
            "ChkRestorePoint", "StatusBarText", "LogOutputBox", "GlobalProgressBar"
        };

        private static readonly HashSet<string> PrivacyPanelCategories = new HashSet<string>(
            new string[] { "Privacy", "AI", "Security" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> AppCategoryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Gaming", "Jogos & Xbox" },
            { "AI", "Inteligência Artificial & Copilot" },
            { "Productivity", "Produtividade & Office" },
            { "Communication", "Comunicação & Mensagens" },
            { "Media", "Mídia, Vídeo & Áudio" },
            { "Utilities", "Utilitários & Notícias" },
            { "ThirdPartyBloat", "Bloatware Pré-instalado (OEM / Patrocinados)" }
        };

        internal static ImageSource LoadApplicationIcon()
        {
            Assembly asm = typeof(MainWindowGUI).Assembly;
            using (Stream stream = asm.GetManifestResourceStream("Otimizador.Assets.Otimizador.ico"))
            {
                if (stream == null)
                {
                    return null;
                }

                var decoder = new IconBitmapDecoder(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                if (decoder.Frames == null || decoder.Frames.Count == 0)
                {
                    return null;
                }

                BitmapFrame chosen = null;
                foreach (BitmapFrame frame in decoder.Frames)
                {
                    if (frame.PixelWidth == 48 || frame.PixelWidth == 32)
                    {
                        chosen = frame;
                        if (frame.PixelWidth == 48)
                        {
                            break;
                        }
                    }
                }

                if (chosen == null)
                {
                    chosen = decoder.Frames[0];
                }

                if (chosen.CanFreeze && !chosen.IsFrozen)
                {
                    chosen.Freeze();
                }

                return chosen;
            }
        }

        private static Brush ConvertToBrush(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        public static MainWindowHandles NewMainWindow()
        {
            Window window;
            using (var sr = new StringReader(LoadMainWindowXaml()))
            using (var xr = XmlReader.Create(sr))
            {
                window = (Window)XamlReader.Load(xr);
            }

            ImageSource appIcon = LoadApplicationIcon();
            if (appIcon != null)
            {
                window.Icon = appIcon;
            }

            var controls = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
            foreach (string name in ControlNames)
            {
                var ctl = window.FindName(name) as FrameworkElement;
                if (ctl == null)
                {
                    throw new InvalidOperationException(string.Format("Controle '{0}' não encontrado no XAML da janela principal.", name));
                }
                controls[name] = ctl;
            }

            return new MainWindowHandles { Window = window, Controls = controls };
        }

        public static void ShowMainWindow(
            OptimizerCatalog catalog,
            ExecutionContext context,
            string logFilePath,
            string osCaption,
            bool dryRun)
        {
            MainWindowHandles ui = NewMainWindow();
            Window window = ui.Window;
            var c = ui.Controls;

            var dryRunBadge = (Border)c["DryRunBadge"];
            var hostInfoText = (TextBlock)c["HostInfoText"];
            var userInfoText = (TextBlock)c["UserInfoText"];
            var navBtnPresets = (Button)c["NavBtnPresets"];
            var navBtnApps = (Button)c["NavBtnApps"];
            var navBtnPrivacy = (Button)c["NavBtnPrivacy"];
            var navBtnPerformance = (Button)c["NavBtnPerformance"];
            var navBtnLogs = (Button)c["NavBtnLogs"];
            var navBtnUndo = (Button)c["NavBtnUndo"];
            var panelPresets = (Grid)c["PanelPresets"];
            var panelApps = (Grid)c["PanelApps"];
            var panelPrivacy = (Grid)c["PanelPrivacy"];
            var panelPerformance = (Grid)c["PanelPerformance"];
            var panelLogs = (Grid)c["PanelLogs"];
            var panelUndo = (Grid)c["PanelUndo"];
            var appsContainerList = (StackPanel)c["AppsContainerList"];
            var privacyTweaksContainer = (StackPanel)c["PrivacyTweaksContainer"];
            var systemTweaksContainer = (StackPanel)c["SystemTweaksContainer"];
            var appSearchBox = (TextBox)c["AppSearchBox"];
            var btnAppsSelectDefault = (Button)c["BtnAppsSelectDefault"];
            var btnAppsSelectAll = (Button)c["BtnAppsSelectAll"];
            var btnAppsDeselectAll = (Button)c["BtnAppsDeselectAll"];
            var btnPresetPadrao = (Button)c["BtnPresetPadrao"];
            var btnPresetGamer = (Button)c["BtnPresetGamer"];
            var btnPresetCorp = (Button)c["BtnPresetCorp"];
            var btnPresetExtremo = (Button)c["BtnPresetExtremo"];
            var btnUndoAction = (Button)c["BtnUndoAction"];
            var btnApplyOptimization = (Button)c["BtnApplyOptimization"];
            var btnCloseWindow = (Button)c["BtnCloseWindow"];
            var btnOpenLogsFolder = (Button)c["BtnOpenLogsFolder"];
            var chkRestorePoint = (CheckBox)c["ChkRestorePoint"];
            var statusBarText = (TextBlock)c["StatusBarText"];
            var logOutputBox = (TextBox)c["LogOutputBox"];
            var globalProgressBar = (ProgressBar)c["GlobalProgressBar"];

            window.Title = "Otimizador de Windows" + (dryRun ? " [SIMULAÇÃO]" : string.Empty);
            if (dryRun) dryRunBadge.Visibility = Visibility.Visible;
            hostInfoText.Text = "Sistema: " + osCaption;
            userInfoText.Text = string.Format("Usuário: {0} | PC: {1} | {2}",
                Environment.UserName, Environment.MachineName, context.IsPortable ? "Notebook" : "Desktop");

            Action<string, string> appendLogLine = delegate(string level, string message)
            {
                logOutputBox.AppendText(string.Format("[{0}] [{1}] {2}\r\n", DateTime.Now.ToString("HH:mm:ss"), level, message));
                logOutputBox.ScrollToEnd();
            };
            Logger.SetTarget(logFilePath, appendLogLine, null);

            Action<string> setStatus = delegate(string text)
            {
                statusBarText.Text = text;
            };

            var allNavButtons = new Button[] { navBtnPresets, navBtnApps, navBtnPrivacy, navBtnPerformance, navBtnLogs, navBtnUndo };
            var allPanels = new Grid[] { panelPresets, panelApps, panelPrivacy, panelPerformance, panelLogs, panelUndo };

            Action<Button, Grid> switchTab = delegate(Button targetBtn, Grid targetPanel)
            {
                foreach (Button btn in allNavButtons)
                {
                    btn.Background = Brushes.Transparent;
                    btn.Foreground = ConvertToBrush("#C0C0C0");
                }
                foreach (Grid panel in allPanels)
                {
                    panel.Visibility = Visibility.Collapsed;
                }
                targetBtn.Background = ConvertToBrush("#222222");
                targetBtn.Foreground = ConvertToBrush("#00B4D8");
                targetPanel.Visibility = Visibility.Visible;
            };

            navBtnPresets.Click += delegate { switchTab(navBtnPresets, panelPresets); };
            navBtnApps.Click += delegate { switchTab(navBtnApps, panelApps); };
            navBtnPrivacy.Click += delegate { switchTab(navBtnPrivacy, panelPrivacy); };
            navBtnPerformance.Click += delegate { switchTab(navBtnPerformance, panelPerformance); };
            navBtnLogs.Click += delegate { switchTab(navBtnLogs, panelLogs); };
            navBtnUndo.Click += delegate { switchTab(navBtnUndo, panelUndo); };

            var appCheckBoxes = new List<CheckBox>();
            var tweakCheckBoxes = new List<CheckBox>();

            Func<string, TextBlock> newHeader = delegate(string text)
            {
                return new TextBlock
                {
                    Text = text,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = ConvertToBrush("#00B4D8"),
                    Margin = new Thickness(4, 10, 4, 4)
                };
            };

            var appGroups = new Dictionary<string, List<AppItem>>(StringComparer.OrdinalIgnoreCase);
            var appGroupOrder = new List<string>();
            foreach (AppItem app in catalog.Apps)
            {
                string cat = app.Category ?? "Outros";
                if (!appGroups.ContainsKey(cat))
                {
                    appGroups[cat] = new List<AppItem>();
                    appGroupOrder.Add(cat);
                }
                appGroups[cat].Add(app);
            }

            foreach (string cat in appGroupOrder)
            {
                string title = AppCategoryNames.ContainsKey(cat) ? AppCategoryNames[cat] : cat;
                appsContainerList.Children.Add(newHeader(title));

                foreach (AppItem app in appGroups[cat])
                {
                    var cb = new CheckBox
                    {
                        Content = app.FriendlyName,
                        Tag = app,
                        ToolTip = string.Format("{0}\nImpacto: {1}", app.Description, app.Recommendation == "caution" ? "Requer cautela" : "Seguro"),
                        IsChecked = app.SelectedByDefault
                    };
                    if (string.Equals(app.Recommendation, "caution", StringComparison.OrdinalIgnoreCase))
                    {
                        cb.Foreground = ConvertToBrush("#F39C12");
                    }
                    appCheckBoxes.Add(cb);
                    appsContainerList.Children.Add(cb);
                }
            }

            var portableNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "DisableFastStartup", "Notebook detectado: apenas a Inicialização Rápida será desligada; a hibernação é mantida." },
                { "InjectUltimatePerformancePlan", "Notebook detectado: este ajuste será ignorado para preservar a bateria." }
            };

            var declaredCategories = new List<TweakCategory>(catalog.Categories);
            var declaredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TweakCategory cat in declaredCategories) declaredIds.Add(cat.Id);
            foreach (TweakItem t in catalog.Tweaks)
            {
                if (!string.IsNullOrWhiteSpace(t.Category) && declaredIds.Add(t.Category))
                {
                    declaredCategories.Add(new TweakCategory { Id = t.Category, Name = t.Category });
                }
            }

            foreach (TweakCategory cat in declaredCategories)
            {
                var catTweaks = new List<TweakItem>();
                foreach (TweakItem t in catalog.Tweaks)
                {
                    if (string.Equals(t.Category, cat.Id, StringComparison.OrdinalIgnoreCase)) catTweaks.Add(t);
                }
                if (catTweaks.Count == 0) continue;

                StackPanel container = PrivacyPanelCategories.Contains(cat.Id) ? privacyTweaksContainer : systemTweaksContainer;
                container.Children.Add(newHeader(cat.Name));

                foreach (TweakItem tweak in catTweaks)
                {
                    string tip = tweak.Description ?? string.Empty;
                    if (context.IsPortable && portableNotes.ContainsKey(tweak.Id))
                    {
                        tip += "\n" + portableNotes[tweak.Id];
                    }
                    var cb = new CheckBox
                    {
                        Content = tweak.Name,
                        Tag = tweak,
                        ToolTip = tip,
                        IsChecked = tweak.SelectedByDefault
                    };
                    tweakCheckBoxes.Add(cb);
                    container.Children.Add(cb);
                }
            }

            appSearchBox.TextChanged += delegate
            {
                string filter = (appSearchBox.Text ?? string.Empty).ToLowerInvariant().Trim();
                foreach (CheckBox cb in appCheckBoxes)
                {
                    var app = (AppItem)cb.Tag;
                    bool match = (app.FriendlyName ?? string.Empty).ToLowerInvariant().Contains(filter)
                              || (app.Description ?? string.Empty).ToLowerInvariant().Contains(filter);
                    cb.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                }
            };

            btnAppsSelectDefault.Click += delegate
            {
                foreach (CheckBox cb in appCheckBoxes) cb.IsChecked = ((AppItem)cb.Tag).SelectedByDefault;
                setStatus("Seleção padrão de aplicativos carregada.");
            };
            btnAppsSelectAll.Click += delegate
            {
                foreach (CheckBox cb in appCheckBoxes) cb.IsChecked = true;
                setStatus("Todos os aplicativos marcados para remoção.");
            };
            btnAppsDeselectAll.Click += delegate
            {
                foreach (CheckBox cb in appCheckBoxes) cb.IsChecked = false;
                setStatus("Todos os aplicativos desmarcados.");
            };

            Action<string> loadPreset = delegate(string presetName)
            {
                OptimizerPreset preset;
                try
                {
                    preset = OptimizationEngine.ImportPreset(catalog, presetName);
                }
                catch (Exception ex)
                {
                    Logger.Write("ERRO", ex.Message);
                    MessageBox.Show(ex.Message, "Perfil inválido", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var removeSet = new HashSet<string>(preset.RemoveApps ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                var tweakSet = new HashSet<string>(preset.ApplyTweaks ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

                foreach (CheckBox cb in appCheckBoxes) cb.IsChecked = removeSet.Contains(((AppItem)cb.Tag).Id);
                foreach (CheckBox cb in tweakCheckBoxes) cb.IsChecked = tweakSet.Contains(((TweakItem)cb.Tag).Id);

                setStatus(string.Format("Perfil '{0}' carregado: {1} aplicativos e {2} ajustes marcados. Revise as abas e clique em Executar.",
                    preset.DisplayName, removeSet.Count, tweakSet.Count));
                Logger.Write("INFO", string.Format("Perfil '{0}' carregado na interface.", preset.DisplayName));
            };

            btnPresetPadrao.Click += delegate { loadPreset("Padrao"); };
            btnPresetGamer.Click += delegate { loadPreset("Gamer"); };
            btnPresetCorp.Click += delegate { loadPreset("Corporativo"); };
            btnPresetExtremo.Click += delegate { loadPreset("Extremo"); };

            var lockableControls = new UIElement[]
            {
                btnPresetPadrao, btnPresetGamer, btnPresetCorp, btnPresetExtremo,
                btnAppsSelectDefault, btnAppsSelectAll, btnAppsDeselectAll, appSearchBox,
                appsContainerList, privacyTweaksContainer, systemTweaksContainer,
                chkRestorePoint, btnUndoAction, btnApplyOptimization, btnCloseWindow
            };

            bool busy = false;
            Task activeTask = null;
            Action onCompleteCallback = null;
            var queue = new ConcurrentQueue<BackgroundEvent>();
            string[] pendingAppIds = null;
            string[] pendingTweakIds = null;

            Action<bool> setBusy = delegate(bool isBusy)
            {
                busy = isBusy;
                foreach (UIElement ctl in lockableControls) ctl.IsEnabled = !isBusy;
                globalProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
                if (isBusy) globalProgressBar.IsIndeterminate = true;
            };

            Action drainQueue = delegate
            {
                BackgroundEvent item;
                while (queue.TryDequeue(out item))
                {
                    if (item.Type == "Log")
                    {
                        logOutputBox.AppendText(string.Format("[{0}] [{1}] {2}\r\n", item.Time.ToString("HH:mm:ss"), item.Level, item.Message));
                        logOutputBox.ScrollToEnd();
                    }
                    else if (item.Type == "Progress" && item.Total > 0)
                    {
                        globalProgressBar.IsIndeterminate = false;
                        globalProgressBar.Maximum = item.Total;
                        globalProgressBar.Value = item.Current;
                        setStatus(string.Format("{0} ({1}/{2})", item.Activity, item.Current, item.Total));
                    }
                }
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            timer.Tick += delegate
            {
                if (activeTask == null) { timer.Stop(); return; }
                drainQueue();
                if (!activeTask.IsCompleted) return;

                timer.Stop();
                drainQueue();
                Logger.SetTarget(logFilePath, appendLogLine, null);
                setBusy(false);

                Action callback = onCompleteCallback;
                activeTask = null;
                onCompleteCallback = null;
                if (callback != null) callback();
            };

            Action<Func<object>, Action<object>> startBackgroundTask = delegate(Func<object> work, Action<object> onComplete)
            {
                setBusy(true);
                switchTab(navBtnLogs, panelLogs);

                Action<string, string> bgLogSink = delegate(string lvl, string msg)
                {
                    queue.Enqueue(new BackgroundEvent { Type = "Log", Level = lvl, Message = msg, Time = DateTime.Now });
                };
                Action<int, int, string> bgProgSink = delegate(int cur, int tot, string act)
                {
                    queue.Enqueue(new BackgroundEvent { Type = "Progress", Current = cur, Total = tot, Activity = act });
                };

                Logger.SetTarget(logFilePath, bgLogSink, bgProgSink);

                Task<object> task = Task<object>.Factory.StartNew(delegate
                {
                    try
                    {
                        return work();
                    }
                    catch (Exception ex)
                    {
                        Logger.Write("ERRO", string.Format("Falha inesperada: {0}", ex.Message));
                        return null;
                    }
                }, TaskCreationOptions.LongRunning);

                activeTask = task;
                onCompleteCallback = delegate { onComplete(task.Result); };
                timer.Start();
            };

            Action<object> onOptimizeComplete = delegate(object rawResult)
            {
                var result = rawResult as ExecutionResult;
                if (result == null)
                {
                    setStatus("A otimização foi interrompida por um erro inesperado.");
                    MessageBox.Show(string.Format("A otimização foi interrompida por um erro inesperado.\n\nConsulte o log: {0}", logFilePath),
                        "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string text;
                MessageBoxImage icon;
                if (result.DryRun)
                {
                    text = string.Format("Simulação concluída: {0} etapas seriam executadas. Nada foi alterado.", result.Simulated);
                    icon = MessageBoxImage.Information;
                }
                else
                {
                    text = string.Format("Otimização concluída: {0} OK, {1} com aviso, {2} com erro.", result.Ok, result.Warnings, result.Failed);
                    icon = result.Failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
                }

                setStatus(text);
                MessageBox.Show(string.Format("{0}\n\nLog detalhado: {1}", text, logFilePath), "Otimização", MessageBoxButton.OK, icon);
            };

            Action startOptimize = delegate
            {
                string[] appsCopy = pendingAppIds;
                string[] tweaksCopy = pendingTweakIds;
                startBackgroundTask(
                    delegate { return OptimizationEngine.InvokeOptimizationPlan(catalog, appsCopy, tweaksCopy, context, dryRun); },
                    onOptimizeComplete);
            };

            Action<object> onRestorePointComplete = delegate(object rawResult)
            {
                var rp = rawResult as RestorePointTaskResult;
                if (rp != null && rp.Success)
                {
                    startOptimize();
                    return;
                }

                string reason = rp != null ? rp.Message : "erro desconhecido";
                MessageBoxResult answer = MessageBox.Show(
                    string.Format("Não foi possível criar o Ponto de Restauração:\n{0}\n\nDeseja continuar SEM ponto de restauração?", reason),
                    "Ponto de Restauração",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (answer == MessageBoxResult.Yes)
                {
                    Logger.Write("AVISO", "Continuando sem Ponto de Restauração por decisão do usuário.");
                    startOptimize();
                }
                else
                {
                    Logger.Write("INFO", "Otimização cancelada pelo usuário. Nada foi alterado.");
                    setStatus("Otimização cancelada (sem ponto de restauração).");
                }
            };

            Action<object> onUndoComplete = delegate(object rawResult)
            {
                var result = rawResult as UndoResult;
                if (result == null)
                {
                    MessageBox.Show(string.Format("A restauração foi interrompida por um erro inesperado.\n\nConsulte o log: {0}", logFilePath),
                        "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string text = string.Format("Restauração concluída: {0} itens revertidos, {1} com erro.", result.Restored, result.Failed);
                if (result.Mode == "Padroes")
                {
                    text += "\nNenhum diário encontrado: foram restaurados os padrões do Windows.";
                }
                setStatus(text.Split('\n')[0]);
                MessageBoxImage icon = result.Failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
                MessageBox.Show(string.Format("{0}\n\nLog detalhado: {1}", text, logFilePath), "Restauração", MessageBoxButton.OK, icon);
            };

            btnApplyOptimization.Click += delegate
            {
                var selectedApps = new List<string>();
                foreach (CheckBox cb in appCheckBoxes)
                {
                    if (cb.IsChecked == true) selectedApps.Add(((AppItem)cb.Tag).Id);
                }
                var selectedTweaks = new List<string>();
                foreach (CheckBox cb in tweakCheckBoxes)
                {
                    if (cb.IsChecked == true) selectedTweaks.Add(((TweakItem)cb.Tag).Id);
                }

                if (selectedApps.Count == 0 && selectedTweaks.Count == 0)
                {
                    MessageBox.Show("Nenhum aplicativo ou ajuste selecionado.", "Nada a fazer", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string question = string.Format("Serão removidos {0} aplicativo(s) e aplicados {1} ajuste(s).", selectedApps.Count, selectedTweaks.Count);
                if (dryRun) question += "\n\nMODO SIMULAÇÃO: nada será alterado.";
                MessageBoxResult answer = MessageBox.Show(question + "\n\nDeseja continuar?", "Confirmar Otimização", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;

                pendingAppIds = selectedApps.ToArray();
                pendingTweakIds = selectedTweaks.ToArray();

                if (chkRestorePoint.IsChecked == true && !dryRun)
                {
                    string desc = BackupManager.NewRestorePointDescription("GUI");
                    startBackgroundTask(delegate
                    {
                        try
                        {
                            BackupManager.InvokeSystemRestorePoint(desc);
                            return new RestorePointTaskResult { Success = true, Message = string.Empty };
                        }
                        catch (Exception ex)
                        {
                            Logger.Write("ERRO", string.Format("Ponto de Restauração não criado: {0}", ex.Message));
                            return new RestorePointTaskResult { Success = false, Message = ex.Message };
                        }
                    }, onRestorePointComplete);
                }
                else
                {
                    if (!dryRun) Logger.Write("AVISO", "Ponto de Restauração desmarcado pelo usuário.");
                    startOptimize();
                }
            };

            btnUndoAction.Click += delegate
            {
                MessageBoxResult answer = MessageBox.Show("Deseja restaurar as configurações anteriores às otimizações?",
                    "Confirmar Restauração", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;

                startBackgroundTask(delegate
                {
                    ChangeJournal.SetUserRegistryTarget(context.UserSid);
                    try
                    {
                        return UndoManager.InvokeSystemUndo(context.JournalDirectory, context.IsWin11, dryRun);
                    }
                    finally
                    {
                        ChangeJournal.SetUserRegistryTarget(null);
                    }
                }, onUndoComplete);
            };

            btnOpenLogsFolder.Click += delegate
            {
                string folder = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    string explorerExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                    Process.Start(File.Exists(explorerExe) ? explorerExe : "explorer.exe", string.Format("\"{0}\"", folder));
                }
            };

            btnCloseWindow.Click += delegate { window.Close(); };

            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (busy)
                {
                    e.Cancel = true;
                    MessageBox.Show("Aguarde o término da operação em andamento antes de fechar.",
                        "Operação em andamento", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            Logger.Write("INFO", string.Format("Janela aberta. Log desta sessão: {0}", logFilePath));
            window.ShowDialog();
        }
    }
}
