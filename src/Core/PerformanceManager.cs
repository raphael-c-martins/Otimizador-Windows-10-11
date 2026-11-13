using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public static class PerformanceManager
    {
        private const string UltimatePerformanceTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        private const string HighPerformanceScheme = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private static readonly Regex _guidRegex = new Regex(@"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");

        public static void ShowFileExtensions()
        {
            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, RegistryValueKind.DWord);
        }

        public static void HideTaskbarExtraButtons(bool isWin11)
        {
            const string expAdv = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            ChangeJournal.SetRegistryValue(expAdv, "ShowTaskViewButton", 0, RegistryValueKind.DWord);
            if (isWin11)
            {
                ChangeJournal.SetRegistryValue(expAdv, "TaskbarMn", 0, RegistryValueKind.DWord);
            }
            else
            {
                ChangeJournal.SetRegistryValue(expAdv, "PeopleBand", 0, RegistryValueKind.DWord);
            }
        }

        public static void EnableClassicContextMenu(bool isWin11)
        {
            if (!isWin11)
            {
                Logger.Write("INFO", "O menu de contexto clássico já é o padrão no Windows 10 (nada a fazer).");
                return;
            }
            const string key = @"HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
            ChangeJournal.SetRegistryValue(key, "(default)", string.Empty, RegistryValueKind.String);
        }

        public static void SetOptimizedVisualEffects()
        {
            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(@"HKCU:\Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", RegistryValueKind.String);
            ChangeJournal.SetRegistryValue(@"HKCU:\Control Panel\Desktop", "FontSmoothing", "2", RegistryValueKind.String);
            ChangeJournal.SetRegistryValue(@"HKCU:\Control Panel\Desktop", "FontSmoothingType", 2, RegistryValueKind.DWord);
        }

        public static void DisableFastStartup(bool isPortable)
        {
            ChangeJournal.SetRegistryValue(@"HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, RegistryValueKind.DWord);
            if (isPortable)
            {
                Logger.Write("INFO", "Notebook detectado: a hibernação foi mantida (o Windows a usa quando a bateria fica crítica).");
                return;
            }

            ChangeJournal.RegisterPowerStateSnapshot();
            var res = SystemInfo.RunProcess("powercfg.exe", "/hibernate off", 20000);
            if (res.ExitCode != 0)
            {
                throw new InvalidOperationException(string.Format("powercfg não conseguiu desativar a hibernação (código {0}).", res.ExitCode));
            }
        }

        private static List<string> GetPowerSchemeGuids()
        {
            var guids = new List<string>();
            var res = SystemInfo.RunProcess("powercfg.exe", "/list", 15000);
            foreach (string line in (res.StdOut ?? string.Empty).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match m = _guidRegex.Match(line);
                if (m.Success)
                {
                    guids.Add(m.Groups[1].Value.ToLowerInvariant());
                }
            }
            return guids;
        }

        public static void EnableUltimatePerformancePlan(bool isPortable, string stateDirectory)
        {
            if (isPortable)
            {
                Logger.Write("INFO", "Notebook detectado: o plano Ultimate Performance não foi ativado para preservar a bateria.");
                return;
            }

            if (string.IsNullOrWhiteSpace(stateDirectory))
            {
                stateDirectory = Constants.OptimizerDataRoot;
            }

            ChangeJournal.RegisterPowerStateSnapshot();
            List<string> existing = GetPowerSchemeGuids();
            string guidFile = Path.Combine(stateDirectory, "plano-ultimate.guid");
            string target = null;

            if (File.Exists(guidFile))
            {
                string saved = File.ReadAllText(guidFile).Trim().ToLowerInvariant();
                if (existing.Contains(saved)) target = saved;
            }

            if (target == null && existing.Contains(UltimatePerformanceTemplate))
            {
                target = UltimatePerformanceTemplate;
            }

            if (target == null)
            {
                var dupRes = SystemInfo.RunProcess("powercfg.exe", string.Format("/duplicatescheme {0}", UltimatePerformanceTemplate), 15000);
                string combined = (dupRes.StdOut ?? string.Empty) + " " + (dupRes.StdErr ?? string.Empty);
                Match m = _guidRegex.Match(combined);
                if (dupRes.ExitCode == 0 && m.Success)
                {
                    target = m.Groups[1].Value.ToLowerInvariant();
                    ChangeJournal.RegisterPowerSchemeCreated(target);
                    if (!Directory.Exists(stateDirectory)) Directory.CreateDirectory(stateDirectory);
                    File.WriteAllText(guidFile, target, System.Text.Encoding.ASCII);
                }
            }

            if (target == null)
            {
                if (existing.Contains(HighPerformanceScheme))
                {
                    Logger.Write("AVISO", "Plano Ultimate Performance indisponível nesta edição; ativado o plano Alto Desempenho.");
                    target = HighPerformanceScheme;
                }
                else
                {
                    throw new InvalidOperationException("Nem o plano Ultimate Performance nem o Alto Desempenho estão disponíveis neste equipamento.");
                }
            }

            var actRes = SystemInfo.RunProcess("powercfg.exe", string.Format("/setactive {0}", target), 15000);
            if (actRes.ExitCode != 0)
            {
                throw new InvalidOperationException(string.Format("powercfg não conseguiu ativar o plano {0} (código {1}).", target, actRes.ExitCode));
            }
        }

        public static void DisableLegacyHeavyServices()
        {
            ChangeJournal.SetServiceStartupTracked("WSearch", "Disabled", true);
            ChangeJournal.SetServiceStartupTracked("SysMain", "Disabled", true);
        }

        public static void RemoveMicrosoftEdge()
        {
            ChangeJournal.SetRegistryValue(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev", "AllowUninstall", 1, RegistryValueKind.DWord);

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            try
            {
                string edgeStub = Path.Combine(winDir, @"SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe");
                if (!Directory.Exists(edgeStub)) Directory.CreateDirectory(edgeStub);
                string exeStub = Path.Combine(edgeStub, "MicrosoftEdge.exe");
                if (!File.Exists(exeStub)) File.WriteAllBytes(exeStub, new byte[0]);
            }
            catch (Exception ex)
            {
                Logger.Write("AVISO", string.Format("Não foi possível criar o arquivo de compatibilidade do Edge ({0}).", ex.Message));
            }

            string[] candidateKeys = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"
            };

            string foundSubKey = null;
            string unStr = null;
            using (RegistryKey hklm = ChangeJournal.OpenBaseRegistryKey(RegistryHive.LocalMachine))
            {
                foreach (string subKey in candidateKeys)
                {
                    using (RegistryKey k = hklm.OpenSubKey(subKey))
                    {
                        if (k != null)
                        {
                            foundSubKey = subKey;
                            unStr = Convert.ToString(k.GetValue("UninstallString"));
                            break;
                        }
                    }
                }
            }

            if (foundSubKey == null)
            {
                Logger.Write("INFO", "Microsoft Edge não encontrado (já removido ou não instalado).");
                return;
            }
            if (string.IsNullOrWhiteSpace(unStr))
            {
                throw new InvalidOperationException("Comando de desinstalação do Edge não encontrado no registro.");
            }

            string exe;
            string arguments;
            Match quoted = Regex.Match(unStr, "^\\s*\"([^\"]+)\"\\s*(.*)$");
            if (quoted.Success)
            {
                exe = quoted.Groups[1].Value;
                arguments = quoted.Groups[2].Value;
            }
            else
            {
                Match plain = Regex.Match(unStr, @"^\s*(\S+)\s*(.*)$");
                if (!plain.Success)
                {
                    throw new InvalidOperationException(string.Format("Comando de desinstalação do Edge em formato inesperado: '{0}'.", unStr));
                }
                exe = plain.Groups[1].Value;
                arguments = plain.Groups[2].Value;
            }

            string[] edgeRoots = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge")
            };
            if (!AppManager.IsTrustedUninstallExecutable(exe, "setup.exe", edgeRoots))
            {
                throw new InvalidOperationException(string.Format("Executável de desinstalação do Edge fora das pastas esperadas: '{0}'.", exe));
            }

            var res = SystemInfo.RunProcess(exe, (arguments + " --force-uninstall").Trim(), 180000, true, false);
            Logger.Write("INFO", string.Format("Desinstalador do Edge finalizado (código {0}).", res.ExitCode));

            using (RegistryKey hklm = ChangeJournal.OpenBaseRegistryKey(RegistryHive.LocalMachine))
            using (RegistryKey check = hklm.OpenSubKey(foundSubKey))
            {
                if (check != null)
                {
                    Logger.Write("AVISO", "O Edge continua registrado: o Windows pode ter bloqueado a remoção nesta região/edição.");
                }
            }

            string publicDir = Environment.GetEnvironmentVariable("PUBLIC") ?? string.Empty;
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var shortcuts = new List<string>();
            if (!string.IsNullOrWhiteSpace(publicDir)) shortcuts.Add(Path.Combine(publicDir, @"Desktop\Microsoft Edge.lnk"));
            if (!string.IsNullOrWhiteSpace(userProfile)) shortcuts.Add(Path.Combine(userProfile, @"Desktop\Microsoft Edge.lnk"));
            if (!string.IsNullOrWhiteSpace(progData)) shortcuts.Add(Path.Combine(progData, @"Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk"));
            if (!string.IsNullOrWhiteSpace(appData)) shortcuts.Add(Path.Combine(appData, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Microsoft Edge.lnk"));
            foreach (string lnk in shortcuts)
            {
                try
                {
                    if (File.Exists(lnk)) File.Delete(lnk);
                }
                catch
                {
                }
            }
        }

        public static bool TestPathIsWithin(string path, string parent)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(parent)) return false;

            string[] pathParts = Path.GetFullPath(path).TrimEnd('\\', '/').Split('\\', '/');
            string[] parentParts = Path.GetFullPath(parent).TrimEnd('\\', '/').Split('\\', '/');

            if (pathParts.Length < parentParts.Length) return false;
            for (int i = 0; i < parentParts.Length; i++)
            {
                if (!string.Equals(pathParts[i], parentParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsProtectedTempEntry(string itemFullPath, IEnumerable<string> protectedPaths)
        {
            if (protectedPaths == null) return false;
            foreach (string keep in protectedPaths)
            {
                if (string.IsNullOrWhiteSpace(keep)) continue;
                if (TestPathIsWithin(keep, itemFullPath) || TestPathIsWithin(itemFullPath, keep))
                {
                    return true;
                }
            }
            return false;
        }

        private static void DeleteTempEntrySafely(FileSystemInfo entry, string allowedRoot, IEnumerable<string> protectedPaths, ref int removed, ref int inUse, ref int protectedCount)
        {
            if (IsProtectedTempEntry(entry.FullName, protectedPaths))
            {
                protectedCount++;
                return;
            }
            if (!TestPathIsWithin(entry.FullName, allowedRoot))
            {
                protectedCount++;
                return;
            }

            try
            {
                bool isReparse = (entry.Attributes & FileAttributes.ReparsePoint) != 0;
                if (entry is DirectoryInfo)
                {
                    if (isReparse)
                    {
                        // Remove apenas o ponto de junção/symlink sem seguir seu destino (proteção contra traversal)
                        Directory.Delete(entry.FullName, false);
                    }
                    else
                    {
                        int childInUse = 0;
                        DeleteDirectoryContentsSafely((DirectoryInfo)entry, ref childInUse);
                        try
                        {
                            Directory.Delete(entry.FullName, false);
                        }
                        catch
                        {
                            inUse += childInUse > 0 ? childInUse : 1;
                            return;
                        }
                    }
                }
                else
                {
                    if ((entry.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(entry.FullName, entry.Attributes & ~FileAttributes.ReadOnly);
                    }
                    File.Delete(entry.FullName);
                }
                removed++;
            }
            catch
            {
                inUse++;
            }
        }

        private static void DeleteDirectoryContentsSafely(DirectoryInfo dir, ref int inUse)
        {
            foreach (FileSystemInfo child in dir.GetFileSystemInfos())
            {
                try
                {
                    if (child is DirectoryInfo)
                    {
                        if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            Directory.Delete(child.FullName, false);
                        }
                        else
                        {
                            DeleteDirectoryContentsSafely((DirectoryInfo)child, ref inUse);
                            Directory.Delete(child.FullName, false);
                        }
                    }
                    else
                    {
                        if ((child.Attributes & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(child.FullName, child.Attributes & ~FileAttributes.ReadOnly);
                        }
                        File.Delete(child.FullName);
                    }
                }
                catch
                {
                    inUse++;
                }
            }
        }

        public static void ClearSystemTempFiles(string[] protectedPaths, string[] tempRoots = null)
        {
            if (tempRoots == null)
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                tempRoots = new string[] { Path.GetTempPath(), Path.Combine(winDir, "Temp") };
            }

            int removed = 0;
            int inUse = 0;
            int protectedCount = 0;

            foreach (string rawRoot in tempRoots)
            {
                if (string.IsNullOrWhiteSpace(rawRoot) || !Directory.Exists(rawRoot)) continue;
                string fullRoot = Path.GetFullPath(rawRoot);
                var rootInfo = new DirectoryInfo(fullRoot);

                FileSystemInfo[] entries;
                try { entries = rootInfo.GetFileSystemInfos(); }
                catch { continue; }

                foreach (FileSystemInfo item in entries)
                {
                    DeleteTempEntrySafely(item, fullRoot, protectedPaths, ref removed, ref inUse, ref protectedCount);
                }
            }

            Logger.Write("INFO", string.Format("Temporários: {0} itens removidos, {1} em uso mantidos, {2} protegidos.", removed, inUse, protectedCount));
        }

        public static void RestartWindowsExplorer()
        {
            int sessionId = Process.GetCurrentProcess().SessionId;
            var explorers = new List<Process>();
            foreach (Process p in Process.GetProcessesByName("explorer"))
            {
                if (p.SessionId == sessionId) explorers.Add(p);
                else p.Dispose();
            }

            if (explorers.Count == 0) return;

            foreach (Process p in explorers)
            {
                try { p.Kill(); p.WaitForExit(5000); }
                finally { p.Dispose(); }
            }

            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(500);
                foreach (Process p in Process.GetProcessesByName("explorer"))
                {
                    bool sameSession = (p.SessionId == sessionId);
                    p.Dispose();
                    if (sameSession) return;
                }
            }

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string runasExe = Path.Combine(winDir, @"System32\runas.exe");
            string explorerExe = Path.Combine(winDir, "explorer.exe");
            var psi = new ProcessStartInfo
            {
                FileName = runasExe,
                Arguments = string.Format("/trustlevel:0x20000 \"{0}\"", explorerExe),
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using (Process.Start(psi)) { }
            Logger.Write("AVISO", "O Explorer não reiniciou sozinho e foi iniciado manualmente.");
        }
    }
}
