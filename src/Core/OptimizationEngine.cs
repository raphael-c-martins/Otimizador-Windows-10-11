using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Otimizador.Core
{
    public static class OptimizationEngine
    {
        private static readonly string[] BuiltInPresetNames = new string[]
        {
            "Corporativo",
            "Extremo",
            "Gamer",
            "Padrao"
        };

        private static readonly Dictionary<string, Action<ExecutionContext>> TweakHandlers =
            new Dictionary<string, Action<ExecutionContext>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Disable-SystemTelemetry", delegate(ExecutionContext ctx) { PrivacyManager.DisableSystemTelemetry(); } },
                { "Disable-ActivityHistory", delegate(ExecutionContext ctx) { PrivacyManager.DisableActivityHistory(); } },
                { "Disable-WindowsSuggestionsAndAds", delegate(ExecutionContext ctx) { PrivacyManager.DisableWindowsSuggestionsAndAds(); } },
                { "Disable-StartMenuWebSearch", delegate(ExecutionContext ctx) { PrivacyManager.DisableStartMenuWebSearch(); } },
                { "Disable-EdgeBackgroundAndTelemetry", delegate(ExecutionContext ctx) { PrivacyManager.DisableEdgeBackgroundAndTelemetry(); } },
                { "Disable-WindowsRecallAndAI", delegate(ExecutionContext ctx) { PrivacyManager.DisableWindowsRecallAndAI(ctx.IsWin11); } },
                { "Disable-WindowsCopilot", delegate(ExecutionContext ctx) { PrivacyManager.DisableWindowsCopilot(); } },
                { "Disable-AdminShares", delegate(ExecutionContext ctx) { SecurityManager.DisableAdminShares(ctx.IsServer); } },
                { "Disable-GameDVRBackgroundCapture", delegate(ExecutionContext ctx) { GamingManager.DisableGameDVRBackgroundCapture(); } },
                { "Set-GameBarProtocolNullRoute", delegate(ExecutionContext ctx) { GamingManager.SetGameBarProtocolNullRoute(); } },
                { "Enable-ClassicContextMenu", delegate(ExecutionContext ctx) { PerformanceManager.EnableClassicContextMenu(ctx.IsWin11); } },
                { "Show-FileExtensions", delegate(ExecutionContext ctx) { PerformanceManager.ShowFileExtensions(); } },
                { "Hide-TaskbarExtraButtons", delegate(ExecutionContext ctx) { PerformanceManager.HideTaskbarExtraButtons(ctx.IsWin11); } },
                { "Set-OptimizedVisualEffects", delegate(ExecutionContext ctx) { PerformanceManager.SetOptimizedVisualEffects(); } },
                { "Disable-FastStartup", delegate(ExecutionContext ctx) { PerformanceManager.DisableFastStartup(ctx.IsPortable); } },
                { "Enable-UltimatePerformancePlan", delegate(ExecutionContext ctx) { PerformanceManager.EnableUltimatePerformancePlan(ctx.IsPortable, ctx.StateDirectory); } },
                { "Disable-LegacyHeavyServices", delegate(ExecutionContext ctx) { PerformanceManager.DisableLegacyHeavyServices(); } },
                { "Remove-OneDriveDeep", delegate(ExecutionContext ctx) { AppManager.RemoveOneDriveDeep(); } },
                { "Remove-MicrosoftEdge", delegate(ExecutionContext ctx) { PerformanceManager.RemoveMicrosoftEdge(); } },
                { "Clear-SystemTempFiles", delegate(ExecutionContext ctx) { PerformanceManager.ClearSystemTempFiles(ctx.ProtectedPaths.ToArray()); } }
            };

        private static string ReadEmbeddedResource(string resourceName)
        {
            Assembly asm = typeof(OptimizationEngine).Assembly;
            using (Stream stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException(string.Format("Recurso embutido não encontrado: '{0}'.", resourceName));
                }
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static OptimizerCatalog GetCatalog(string baseDirectory = null)
        {
            string appsJson;
            string tweaksJson;

            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                string appsPath = Path.Combine(baseDirectory, @"src\Config\Apps.json");
                string tweaksPath = Path.Combine(baseDirectory, @"src\Config\Tweaks.json");
                if (!File.Exists(appsPath)) throw new FileNotFoundException(string.Format("Catálogo não encontrado: '{0}'.", appsPath));
                if (!File.Exists(tweaksPath)) throw new FileNotFoundException(string.Format("Catálogo não encontrado: '{0}'.", tweaksPath));
                appsJson = File.ReadAllText(appsPath, Encoding.UTF8);
                tweaksJson = File.ReadAllText(tweaksPath, Encoding.UTF8);
            }
            else
            {
                appsJson = ReadEmbeddedResource("Otimizador.Config.Apps.json");
                tweaksJson = ReadEmbeddedResource("Otimizador.Config.Tweaks.json");
            }

            var appsDoc = JsonHelper.Deserialize<AppsDocument>(appsJson);
            var tweaksDoc = JsonHelper.Deserialize<TweaksDocument>(tweaksJson);

            return new OptimizerCatalog
            {
                BaseDirectory = baseDirectory,
                Apps = appsDoc != null && appsDoc.Apps != null ? appsDoc.Apps : new List<AppItem>(),
                Tweaks = tweaksDoc != null && tweaksDoc.Tweaks != null ? tweaksDoc.Tweaks : new List<TweakItem>(),
                Categories = tweaksDoc != null && tweaksDoc.Categories != null ? tweaksDoc.Categories : new List<TweakCategory>()
            };
        }

        public static string[] GetPresetNames(string baseDirectory = null)
        {
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                string presetDir = Path.Combine(baseDirectory, @"src\Config\Presets");
                if (Directory.Exists(presetDir))
                {
                    var names = new List<string>();
                    foreach (string file in Directory.GetFiles(presetDir, "*.json"))
                    {
                        names.Add(Path.GetFileNameWithoutExtension(file));
                    }
                    names.Sort(StringComparer.OrdinalIgnoreCase);
                    return names.ToArray();
                }
            }
            return (string[])BuiltInPresetNames.Clone();
        }

        public static string[] TestPreset(OptimizerCatalog catalog, OptimizerPreset preset)
        {
            var problems = new List<string>();
            var appIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AppItem app in catalog.Apps) appIds.Add(app.Id);

            var tweakIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TweakItem tweak in catalog.Tweaks) tweakIds.Add(tweak.Id);

            if (preset == null || string.IsNullOrWhiteSpace(preset.DisplayName))
            {
                problems.Add("Campo \"DisplayName\" ausente.");
                if (preset == null) return problems.ToArray();
            }

            var removeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (preset.RemoveApps != null)
            {
                foreach (string id in preset.RemoveApps)
                {
                    if (id == null) continue;
                    removeSet.Add(id);
                    if (!appIds.Contains(id)) problems.Add(string.Format("Aplicativo desconhecido: '{0}'.", id));
                }
            }

            if (preset.PreserveApps != null)
            {
                foreach (string id in preset.PreserveApps)
                {
                    if (id == null) continue;
                    if (!appIds.Contains(id)) problems.Add(string.Format("Aplicativo desconhecido: '{0}'.", id));
                    if (removeSet.Contains(id)) problems.Add(string.Format("Aplicativo '{0}' está ao mesmo tempo em RemoveApps e PreserveApps.", id));
                }
            }

            if (preset.ApplyTweaks != null)
            {
                foreach (string id in preset.ApplyTweaks)
                {
                    if (id != null && !tweakIds.Contains(id)) problems.Add(string.Format("Ajuste desconhecido: '{0}'.", id));
                }
            }

            return problems.ToArray();
        }

        public static OptimizerPreset ImportPreset(OptimizerCatalog catalog, string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                throw new ArgumentException("Nome ou caminho do perfil não informado.");
            }

            string sourceLabel;
            string json;

            if (nameOrPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(nameOrPath))
                {
                    throw new FileNotFoundException(string.Format("Arquivo de perfil não encontrado: '{0}'.", nameOrPath));
                }
                sourceLabel = Path.GetFullPath(nameOrPath);
                json = File.ReadAllText(sourceLabel, Encoding.UTF8);
            }
            else
            {
                string[] available = GetPresetNames(catalog.BaseDirectory);
                string match = Array.Find(available, delegate(string n) { return string.Equals(n, nameOrPath, StringComparison.OrdinalIgnoreCase); });
                if (match == null)
                {
                    throw new InvalidOperationException(string.Format("Perfil '{0}' inexistente. Perfis disponíveis: {1}.", nameOrPath, string.Join(", ", available)));
                }

                if (!string.IsNullOrWhiteSpace(catalog.BaseDirectory))
                {
                    sourceLabel = Path.Combine(catalog.BaseDirectory, string.Format(@"src\Config\Presets\{0}.json", match));
                    json = File.ReadAllText(sourceLabel, Encoding.UTF8);
                }
                else
                {
                    sourceLabel = match;
                    json = ReadEmbeddedResource(string.Format("Otimizador.Config.Presets.{0}.json", match));
                }
            }

            OptimizerPreset preset;
            try
            {
                preset = JsonHelper.Deserialize<OptimizerPreset>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Perfil '{0}' não é um JSON válido: {1}", sourceLabel, ex.Message));
            }

            string[] problems = TestPreset(catalog, preset);
            if (problems.Length > 0)
            {
                throw new InvalidOperationException(string.Format("Perfil '{0}' inválido: {1}", sourceLabel, string.Join(" ", problems)));
            }

            return preset;
        }

        public static bool HasTweakFunction(string functionName)
        {
            return !string.IsNullOrWhiteSpace(functionName) && TweakHandlers.ContainsKey(functionName);
        }

        public static void AssertSelection(OptimizerCatalog catalog, IEnumerable<string> appIds, IEnumerable<string> tweakIds)
        {
            var knownApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AppItem a in catalog.Apps) knownApps.Add(a.Id);

            var tweaksById = new Dictionary<string, TweakItem>(StringComparer.OrdinalIgnoreCase);
            foreach (TweakItem t in catalog.Tweaks) tweaksById[t.Id] = t;

            var unknownApps = new List<string>();
            if (appIds != null)
            {
                foreach (string id in appIds)
                {
                    if (!knownApps.Contains(id)) unknownApps.Add(id);
                }
            }
            if (unknownApps.Count > 0)
            {
                throw new InvalidOperationException(string.Format("Aplicativos desconhecidos: {0}.", string.Join(", ", unknownApps.ToArray())));
            }

            var unknownTweaks = new List<string>();
            var selectedTweaks = new List<TweakItem>();
            if (tweakIds != null)
            {
                foreach (string id in tweakIds)
                {
                    TweakItem found;
                    if (!tweaksById.TryGetValue(id, out found)) unknownTweaks.Add(id);
                    else selectedTweaks.Add(found);
                }
            }
            if (unknownTweaks.Count > 0)
            {
                throw new InvalidOperationException(string.Format("Ajustes desconhecidos: {0}.", string.Join(", ", unknownTweaks.ToArray())));
            }

            foreach (TweakItem tweak in selectedTweaks)
            {
                if (!HasTweakFunction(tweak.Function))
                {
                    throw new InvalidOperationException(string.Format("O ajuste '{0}' aponta para a função '{1}', que não existe.", tweak.Id, tweak.Function));
                }
            }
        }

        public static StepResult InvokeStep(string stepName, Action stepAction, bool dryRun)
        {
            var sw = Stopwatch.StartNew();
            string detail = string.Empty;
            string status;

            Logger.Write("ETAPA", stepName);

            if (dryRun)
            {
                status = "SIMULADO";
                Logger.Write("INFO", "[SIMULAÇÃO] Nenhuma alteração aplicada.");
            }
            else
            {
                Logger.ResetStepWarningCount();
                try
                {
                    stepAction();
                    status = Logger.GetStepWarningCount() > 0 ? "AVISO" : "OK";
                }
                catch (Exception ex)
                {
                    status = "ERRO";
                    detail = ex.Message;
                    Logger.Write("ERRO", string.Format("{0}: {1}", stepName, detail));
                }
            }

            sw.Stop();
            if (status == "OK")
            {
                Logger.Write("OK", string.Format("{0} (concluído)", stepName));
            }

            return new StepResult
            {
                Name = stepName,
                Status = status,
                Detail = detail,
                Duration = Math.Round(sw.Elapsed.TotalSeconds, 1)
            };
        }

        private static void AddStepResult(ExecutionResult result, StepResult step)
        {
            result.Steps.Add(step);
            switch (step.Status)
            {
                case "OK": result.Ok++; break;
                case "AVISO": result.Warnings++; break;
                case "ERRO": result.Failed++; break;
                case "SIMULADO": result.Simulated++; break;
            }
        }

        public static ExecutionResult InvokeOptimizationPlan(
            OptimizerCatalog catalog,
            IEnumerable<string> rawAppIds,
            IEnumerable<string> rawTweakIds,
            ExecutionContext context,
            bool dryRun = false,
            Dictionary<string, Action<ExecutionContext>> customHandlers = null)
        {
            if (context == null) context = new ExecutionContext();

            var appIdList = new List<string>();
            if (rawAppIds != null)
            {
                foreach (string id in rawAppIds)
                {
                    if (!string.IsNullOrWhiteSpace(id)) appIdList.Add(id.Trim());
                }
            }

            var tweakIdList = new List<string>();
            if (rawTweakIds != null)
            {
                foreach (string id in rawTweakIds)
                {
                    if (!string.IsNullOrWhiteSpace(id)) tweakIdList.Add(id.Trim());
                }
            }

            if (customHandlers == null)
            {
                AssertSelection(catalog, appIdList, tweakIdList);
            }

            var appIdSet = new HashSet<string>(appIdList, StringComparer.OrdinalIgnoreCase);
            var tweakIdSet = new HashSet<string>(tweakIdList, StringComparer.OrdinalIgnoreCase);

            foreach (AppItem app in catalog.Apps)
            {
                if (!appIdSet.Contains(app.Id) || app.RequiresTweaks == null) continue;
                foreach (string required in app.RequiresTweaks)
                {
                    if (!string.IsNullOrWhiteSpace(required) && tweakIdSet.Add(required))
                    {
                        tweakIdList.Add(required);
                        Logger.Write("INFO", string.Format("Ajuste '{0}' incluído automaticamente porque '{1}' será removido.", required, app.FriendlyName));
                    }
                }
            }

            if (customHandlers == null)
            {
                AssertSelection(catalog, appIdList, tweakIdList);
            }

            // A ordem segue o catálogo para garantir execução previsível
            var apps = new List<AppItem>();
            foreach (AppItem app in catalog.Apps)
            {
                if (appIdSet.Contains(app.Id)) apps.Add(app);
            }

            var tweaks = new List<TweakItem>();
            foreach (TweakItem tweak in catalog.Tweaks)
            {
                if (tweakIdSet.Contains(tweak.Id)) tweaks.Add(tweak);
            }

            var result = new ExecutionResult { DryRun = dryRun };

            if (apps.Count == 0 && tweaks.Count == 0)
            {
                Logger.Write("AVISO", "Nenhum aplicativo ou ajuste selecionado: nada a executar.");
                return result;
            }

            int total = apps.Count + tweaks.Count + (context.RestartExplorer ? 1 : 0);
            int index = 0;

            Logger.Write("INFO", string.Format("Plano: {0} aplicativo(s) e {1} ajuste(s){2}.",
                apps.Count, tweaks.Count, dryRun ? " [SIMULAÇÃO]" : string.Empty));

            if (!dryRun)
            {
                result.JournalPath = ChangeJournal.Start(context.JournalDirectory);
                ChangeJournal.SetUserRegistryTarget(context.UserSid);
            }

            try
            {
                PackageCache cache = null;
                if (apps.Count > 0 && !dryRun)
                {
                    cache = AppManager.GetPackageCache();
                }

                foreach (AppItem app in apps)
                {
                    index++;
                    Logger.WriteProgress(index, total, "Removendo: " + app.FriendlyName);
                    AppItem currentApp = app;
                    StepResult step = InvokeStep(
                        "Remover aplicativo: " + currentApp.FriendlyName,
                        delegate { AppManager.RemoveAppByDefinition(currentApp, cache); },
                        dryRun);
                    AddStepResult(result, step);
                }

                foreach (TweakItem tweak in tweaks)
                {
                    index++;
                    Logger.WriteProgress(index, total, tweak.Name);

                    Action<ExecutionContext> handler = null;
                    if (customHandlers != null && customHandlers.TryGetValue(tweak.Function, out handler))
                    {
                    }
                    else if (!TweakHandlers.TryGetValue(tweak.Function, out handler))
                    {
                        throw new InvalidOperationException(string.Format("O ajuste '{0}' aponta para a função '{1}', que não existe.", tweak.Id, tweak.Function));
                    }

                    Action<ExecutionContext> currentHandler = handler;
                    StepResult step = InvokeStep(
                        tweak.Name,
                        delegate { currentHandler(context); },
                        dryRun);
                    AddStepResult(result, step);
                    if (!dryRun) ChangeJournal.Save();
                }

                if (context.RestartExplorer)
                {
                    index++;
                    Logger.WriteProgress(index, total, "Reiniciando o Windows Explorer");
                    StepResult step = InvokeStep(
                        "Reiniciar o Windows Explorer",
                        delegate { PerformanceManager.RestartWindowsExplorer(); },
                        dryRun);
                    AddStepResult(result, step);
                }
            }
            finally
            {
                if (!dryRun)
                {
                    ChangeJournal.Stop();
                    ChangeJournal.SetUserRegistryTarget(null);
                }
            }

            string summary = dryRun
                ? string.Format("Resumo da simulação: {0} etapas seriam executadas", result.Simulated)
                : string.Format("Resumo: {0} OK, {1} com aviso, {2} com erro", result.Ok, result.Warnings, result.Failed);

            Logger.Write(result.Failed > 0 ? "AVISO" : "OK", summary + ".");
            if (!string.IsNullOrWhiteSpace(result.JournalPath))
            {
                Logger.Write("INFO", string.Format("Diário para reversão (Undo): {0}", result.JournalPath));
            }

            return result;
        }
    }
}
