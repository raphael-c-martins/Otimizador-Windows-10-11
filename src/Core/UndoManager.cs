using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public static class UndoManager
    {
        public static UndoResult InvokeSystemUndo(
            string journalDirectory,
            bool isWin11 = true,
            bool dryRun = false,
            bool skipExplorerRestart = false)
        {
            FileInfo[] journals = ChangeJournal.GetPendingJournals(journalDirectory);
            var result = new UndoResult { Files = journals.Length };

            if (journals.Length > 0)
            {
                result.Mode = "Diario";
                Logger.Write("ETAPA", string.Format("Revertendo {0} execução(ões) registradas no diário de alterações", journals.Length));

                foreach (FileInfo file in journals)
                {
                    if (dryRun)
                    {
                        var doc = JsonHelper.Deserialize<JournalDocument>(File.ReadAllText(file.FullName, Encoding.UTF8));
                        int count = (doc != null && doc.Entradas != null) ? doc.Entradas.Count : 0;
                        Logger.Write("INFO", string.Format("[SIMULAÇÃO] {0}: {1} alterações seriam revertidas.", file.Name, count));
                        continue;
                    }

                    UndoResult partial = ChangeJournal.RestoreJournalFile(file.FullName);
                    result.Restored += partial.Restored;
                    result.Failed += partial.Failed;

                    if (partial.Failed == 0)
                    {
                        string restoredPath = Logger.ResolveUniqueFilePath(Path.Combine(
                            file.DirectoryName ?? journalDirectory,
                            file.Name.Substring(0, file.Name.Length - ".json".Length) + ".restaurado.json"));
                        File.Move(file.FullName, restoredPath);
                        Logger.Write("OK", string.Format("{0}: {1} alterações revertidas.", file.Name, partial.Restored));
                    }
                    else
                    {
                        Logger.Write("AVISO", string.Format("{0}: {1} revertidas, {2} falharam (o diário foi mantido para nova tentativa).",
                            file.Name, partial.Restored, partial.Failed));
                    }
                }
            }
            else
            {
                result.Mode = "Padroes";
                if (dryRun)
                {
                    Logger.Write("INFO", "[SIMULAÇÃO] Nenhum diário de alterações encontrado: os padrões do Windows seriam restaurados para os itens conhecidos.");
                }
                else
                {
                    Logger.Write("AVISO", "Nenhum diário de alterações encontrado: restaurando os padrões do Windows para os itens conhecidos.");
                    UndoResult partial = InvokeWindowsDefaultsRestore(isWin11);
                    result.Restored = partial.Restored;
                    result.Failed = partial.Failed;
                }
            }

            if (!dryRun && !skipExplorerRestart)
            {
                try
                {
                    PerformanceManager.RestartWindowsExplorer();
                }
                catch (Exception ex)
                {
                    Logger.Write("AVISO", string.Format("Explorer não reiniciado: {0}", ex.Message));
                }
            }

            Logger.Write("INFO", "Aplicativos removidos não são reinstalados automaticamente: use a Microsoft Store se precisar de algum.");
            return result;
        }

        public static UndoResult InvokeWindowsDefaultsRestore(bool isWin11 = true)
        {
            var actions = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>("Serviços SysMain e Windows Search", delegate
                {
                    RestoreServiceDefault("SysMain", "Automatic", true);
                    RestoreServiceDefault("WSearch", "Automatic", true);
                }),
                new KeyValuePair<string, Action>("Serviços de telemetria", delegate
                {
                    RestoreServiceDefault("DiagTrack", "Automatic", false);
                    RestoreServiceDefault("dmwappushservice", "Manual", false);
                }),
                new KeyValuePair<string, Action>("Menu de contexto do Windows 11", delegate
                {
                    if (isWin11)
                    {
                        RegistryLocation loc = ChangeJournal.ConvertToRegistryLocation(@"HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}");
                        using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(loc.Hive))
                        {
                            baseKey.DeleteSubKeyTree(loc.SubKey, false);
                        }
                    }
                }),
                new KeyValuePair<string, Action>("Protocolos e gravação do Xbox", delegate
                {
                    GamingManager.RestoreGameBarProtocols();
                    ChangeJournal.SetRegistryValue(@"HKCU:\System\GameConfigStore", "GameDVR_Enabled", 1, RegistryValueKind.DWord);
                    ChangeJournal.SetRegistryValue(@"HKCU:\SOFTWARE\Microsoft\GameBar", "UseNexusForGameBarEnabled", 1, RegistryValueKind.DWord);
                }),
                new KeyValuePair<string, Action>("Efeitos visuais e transparência", delegate
                {
                    ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0, RegistryValueKind.DWord);
                    ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1, RegistryValueKind.DWord);
                    ChangeJournal.SetRegistryValue(@"HKCU:\Control Panel\Desktop\WindowMetrics", "MinAnimate", "1", RegistryValueKind.String);
                }),
                new KeyValuePair<string, Action>("Hibernação e Inicialização Rápida", delegate
                {
                    SystemInfo.RunProcess("powercfg.exe", "/hibernate on", 15000);
                    ChangeJournal.SetRegistryValue(@"HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 1, RegistryValueKind.DWord);
                }),
                new KeyValuePair<string, Action>("Compartilhamentos administrativos padrão (LanmanServer)", delegate
                {
                    RegistryLocation loc = ChangeJournal.ConvertToRegistryLocation(SecurityManager.LanmanParametersPath);
                    using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(loc.Hive))
                    using (RegistryKey key = baseKey.OpenSubKey(loc.SubKey, true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue("AutoShareWks", false);
                            key.DeleteValue("AutoShareServer", false);
                        }
                    }
                    SecurityManager.RestartServiceIfRunning("LanmanServer");
                })
            };

            int restored = 0;
            int failed = 0;
            foreach (var item in actions)
            {
                try
                {
                    item.Value();
                    restored++;
                    Logger.Write("OK", string.Format("{0} restaurado(s).", item.Key));
                }
                catch (Exception ex)
                {
                    failed++;
                    Logger.Write("ERRO", string.Format("{0}: {1}", item.Key, ex.Message));
                }
            }

            return new UndoResult { Restored = restored, Failed = failed };
        }

        private static void RestoreServiceDefault(string name, string startMode, bool tryStart)
        {
            if (!ChangeJournal.ServiceExists(name)) return;
            ChangeJournal.SetServiceStartupType(name, startMode);
            if (tryStart)
            {
                try
                {
                    using (var sc = new ServiceController(name))
                    {
                        if (sc.Status != ServiceControllerStatus.Running)
                        {
                            sc.Start();
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
