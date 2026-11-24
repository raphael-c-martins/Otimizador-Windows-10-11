using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Otimizador.Core
{
    internal sealed class OneDriveUninstallCommand
    {
        public string ExePath { get; set; }
        public string Arguments { get; set; }
    }

    public static class OneDriveManager
    {
        private const string UninstallRelSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe";
        private const string MachineUninstallKey64 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe";
        private const string MachineUninstallKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe";
        private const string RunRelSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string MachineRunKey64 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string MachineRunKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        private const string OneDriveClsid = @"{018D5C66-4533-4307-9B53-224DE2ED1FE6}";

        private static readonly string[] ProcessNames = new string[]
        {
            "OneDrive",
            "OneDriveStandaloneUpdater",
            "OneDrive.Sync.Service",
            "FileCoAuth",
            "OneDriveSetup"
        };

        public static string EnsureAllUsersArgs(string arguments)
        {
            var tokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                foreach (string token in arguments.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    tokens.Add(token);
                }
            }

            bool hasUninstall = tokens.Exists(delegate(string t) { return string.Equals(t, "/uninstall", StringComparison.OrdinalIgnoreCase); });
            bool hasAllUsers = tokens.Exists(delegate(string t) { return string.Equals(t, "/allusers", StringComparison.OrdinalIgnoreCase); });

            if (!hasUninstall) tokens.Insert(0, "/uninstall");
            if (!hasAllUsers) tokens.Add("/allusers");

            return string.Join(" ", tokens.ToArray());
        }

        public static bool IsOneDriveRunValueName(string valueName)
        {
            if (string.IsNullOrWhiteSpace(valueName)) return false;
            string trimmed = valueName.Trim();
            return string.Equals(trimmed, "OneDrive", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "OneDriveSetup", StringComparison.OrdinalIgnoreCase);
        }

        public static string[] GetTrustedOneDriveRoots(string userSid = null)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string effectiveSid = !string.IsNullOrWhiteSpace(userSid) ? userSid : SystemInfo.GetInteractiveUserSid();
            string interactiveProfile = SystemInfo.GetUserProfilePath(effectiveSid);

            var roots = new List<string>();
            if (!string.IsNullOrWhiteSpace(localAppData)) roots.Add(Path.Combine(localAppData, @"Microsoft\OneDrive"));
            if (!string.IsNullOrWhiteSpace(interactiveProfile)) roots.Add(Path.Combine(interactiveProfile, @"AppData\Local\Microsoft\OneDrive"));
            if (!string.IsNullOrWhiteSpace(pf)) roots.Add(Path.Combine(pf, @"Microsoft OneDrive"));
            if (!string.IsNullOrWhiteSpace(pf86)) roots.Add(Path.Combine(pf86, @"Microsoft OneDrive"));
            if (!string.IsNullOrWhiteSpace(winDir))
            {
                roots.Add(Path.Combine(winDir, "System32"));
                roots.Add(Path.Combine(winDir, "SysWOW64"));
            }
            return roots.ToArray();
        }

        private static void StopOneDriveProcesses()
        {
            foreach (string procName in ProcessNames)
            {
                foreach (Process proc in Process.GetProcessesByName(procName))
                {
                    try { proc.Kill(); proc.WaitForExit(5000); }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
        }

        private static RegistryLocation ResolveUserScopedRegistryLocation(string subKey, string userSid)
        {
            if (!string.IsNullOrWhiteSpace(userSid))
            {
                return new RegistryLocation
                {
                    Hive = RegistryHive.Users,
                    HiveName = "Users",
                    SubKey = string.Format("{0}\\{1}", userSid, subKey)
                };
            }
            return ChangeJournal.ConvertToRegistryLocation(@"HKCU:\" + subKey);
        }

        private static bool RegistrySubKeyExists(RegistryHive hive, string subKey)
        {
            try
            {
                using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(hive))
                using (RegistryKey key = baseKey.OpenSubKey(subKey))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ReadUninstallString(RegistryHive hive, string subKey)
        {
            try
            {
                using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(hive))
                using (RegistryKey key = baseKey.OpenSubKey(subKey))
                {
                    if (key == null) return null;
                    return Convert.ToString(key.GetValue("UninstallString"));
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool HasOneDriveBinaryInDirectory(string dir, string binaryName)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir) || SystemInfo.IsReparsePoint(dir))
            {
                return false;
            }

            try
            {
                if (File.Exists(Path.Combine(dir, binaryName))) return true;
                foreach (string subDir in Directory.GetDirectories(dir))
                {
                    if (SystemInfo.IsReparsePoint(subDir)) continue;
                    if (File.Exists(Path.Combine(subDir, binaryName))) return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool HasAnyOneDriveBinary(string dir)
        {
            return HasOneDriveBinaryInDirectory(dir, "OneDrive.exe")
                || HasOneDriveBinaryInDirectory(dir, "OneDriveSetup.exe");
        }

        private static string FindOneDriveSetupInDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir) || SystemInfo.IsReparsePoint(dir))
            {
                return null;
            }
            try
            {
                string direct = Path.Combine(dir, "OneDriveSetup.exe");
                if (File.Exists(direct)) return direct;
                foreach (string subDir in Directory.GetDirectories(dir))
                {
                    if (SystemInfo.IsReparsePoint(subDir)) continue;
                    string nested = Path.Combine(subDir, "OneDriveSetup.exe");
                    if (File.Exists(nested)) return nested;
                }
            }
            catch
            {
            }
            return null;
        }

        private static void TryAddUninstallCommand(
            List<OneDriveUninstallCommand> list,
            HashSet<string> seen,
            string rawCommand,
            string[] trustedRoots,
            bool requireAllUsers)
        {
            string exe;
            string args;
            if (!AppManager.TryParseUninstallCommand(rawCommand, out exe, out args)) return;

            if (!AppManager.IsTrustedUninstallExecutable(exe, "OneDriveSetup.exe", trustedRoots))
            {
                Logger.Write("AVISO", string.Format("Caminho não confiável ignorado no registro do OneDrive: '{0}'.", exe));
                return;
            }

            string finalArgs = requireAllUsers
                ? EnsureAllUsersArgs(args)
                : (string.IsNullOrWhiteSpace(args) ? "/uninstall" : args);

            string dedupKey = exe + "|" + finalArgs;
            if (seen.Add(dedupKey))
            {
                list.Add(new OneDriveUninstallCommand { ExePath = exe, Arguments = finalArgs });
            }
        }

        private static void TryAddSystemCandidateCommand(
            List<OneDriveUninstallCommand> list,
            HashSet<string> seen,
            string[] systemCandidates,
            bool requireAllUsers)
        {
            foreach (string c in systemCandidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                {
                    string finalArgs = requireAllUsers ? "/uninstall /allusers" : "/uninstall";
                    string dedupKey = c + "|" + finalArgs;
                    if (seen.Add(dedupKey))
                    {
                        list.Add(new OneDriveUninstallCommand { ExePath = c, Arguments = finalArgs });
                    }
                    return;
                }
            }
        }

        private static bool HasMatchingOneDriveAppx(PackageCache cache)
        {
            if (cache == null) return false;
            foreach (var pkg in cache.Installed)
            {
                if (AppManager.IsPackageMatch(pkg.Id.Name, "*OneDriveSync*")) return true;
            }
            foreach (var prov in cache.Provisioned)
            {
                if (AppManager.IsPackageMatch(prov.DisplayName, "*OneDriveSync*")
                    || AppManager.IsPackageMatch(prov.PackageName, "*OneDriveSync*"))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasOneDriveRunValue(RegistryHive hive, string subKey)
        {
            try
            {
                using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(hive))
                using (RegistryKey key = baseKey.OpenSubKey(subKey))
                {
                    if (key == null) return false;
                    foreach (string name in key.GetValueNames())
                    {
                        if (IsOneDriveRunValueName(name)) return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static void DeleteRunValuesIfPresent(RegistryHive hive, string subKey)
        {
            try
            {
                using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(hive))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, true))
                {
                    if (key == null) return;
                    foreach (string name in key.GetValueNames())
                    {
                        if (IsOneDriveRunValueName(name))
                        {
                            key.DeleteValue(name, false);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void DeleteRegistrySubKeyIfPresent(RegistryHive hive, string subKey)
        {
            try
            {
                using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(hive))
                {
                    baseKey.DeleteSubKeyTree(subKey, false);
                }
            }
            catch
            {
            }
        }

        private static void DisableOneDriveScheduledTasks()
        {
            dynamic scheduler = null;
            dynamic rootFolder = null;
            dynamic tasks = null;
            var taskNames = new List<string>();

            try
            {
                Type schedType = Type.GetTypeFromProgID("Schedule.Service");
                if (schedType == null) return;
                scheduler = Activator.CreateInstance(schedType);
                scheduler.Connect();
                rootFolder = scheduler.GetFolder("\\");
                tasks = rootFolder.GetTasks(1);
                foreach (dynamic task in tasks)
                {
                    try
                    {
                        string name = Convert.ToString(task.Name);
                        if (!string.IsNullOrWhiteSpace(name) && name.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase))
                        {
                            taskNames.Add(name);
                        }
                    }
                    finally
                    {
                        if (task != null && Marshal.IsComObject(task))
                        {
                            try { Marshal.FinalReleaseComObject(task); } catch { }
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (tasks != null && Marshal.IsComObject(tasks)) { try { Marshal.FinalReleaseComObject(tasks); } catch { } }
                if (rootFolder != null && Marshal.IsComObject(rootFolder)) { try { Marshal.FinalReleaseComObject(rootFolder); } catch { } }
                if (scheduler != null && Marshal.IsComObject(scheduler)) { try { Marshal.FinalReleaseComObject(scheduler); } catch { } }
            }

            foreach (string taskName in taskNames)
            {
                ChangeJournal.DisableScheduledTaskTracked("\\" + taskName);
            }
        }

        private static void DeleteShortcutIfSafe(string shortcutPath)
        {
            if (string.IsNullOrWhiteSpace(shortcutPath)) return;
            try
            {
                string dir = Path.GetDirectoryName(shortcutPath);
                if (string.IsNullOrWhiteSpace(dir) || SystemInfo.IsReparsePoint(dir)) return;
                if (File.Exists(shortcutPath) && !SystemInfo.IsReparsePoint(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
            catch
            {
            }
        }

        private static void UnpinOneDriveFromExplorer(string userSid)
        {
            try
            {
                string classesPrefix = !string.IsNullOrWhiteSpace(userSid) && string.IsNullOrWhiteSpace(ChangeJournal.GetUserRegistryTarget())
                    ? string.Format(@"HKU:\{0}\Software\Classes", userSid)
                    : @"HKCU:\Software\Classes";

                ChangeJournal.SetRegistryValue(classesPrefix + @"\CLSID\" + OneDriveClsid, "System.IsPinnedToNameSpaceTree", 0, RegistryValueKind.DWord);
                ChangeJournal.SetRegistryValue(classesPrefix + @"\Wow6432Node\CLSID\" + OneDriveClsid, "System.IsPinnedToNameSpaceTree", 0, RegistryValueKind.DWord);
            }
            catch
            {
            }
        }

        public static void RemoveOneDriveDeep(string userSid = null)
        {
            StopOneDriveProcesses();

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string commonProgData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string effectiveSid = !string.IsNullOrWhiteSpace(userSid) ? userSid : SystemInfo.GetInteractiveUserSid();
            string interactiveProfile = SystemInfo.GetUserProfilePath(effectiveSid);
            bool isDifferentUser = !string.IsNullOrWhiteSpace(userSid);

            RegistryLocation userUninstallLoc = ResolveUserScopedRegistryLocation(UninstallRelSubKey, userSid);
            RegistryLocation userRunLoc = ResolveUserScopedRegistryLocation(RunRelSubKey, userSid);

            string pfOneDrive = string.IsNullOrWhiteSpace(pf) ? null : Path.Combine(pf, "Microsoft OneDrive");
            string pf86OneDrive = string.IsNullOrWhiteSpace(pf86) ? null : Path.Combine(pf86, "Microsoft OneDrive");
            string currentUserOneDrive = string.IsNullOrWhiteSpace(localAppData) ? null : Path.Combine(localAppData, @"Microsoft\OneDrive");
            string interactiveOneDrive = string.IsNullOrWhiteSpace(interactiveProfile) ? null : Path.Combine(interactiveProfile, @"AppData\Local\Microsoft\OneDrive");

            string[] trustedRoots = GetTrustedOneDriveRoots(userSid);
            string[] systemCandidates = new string[]
            {
                string.IsNullOrWhiteSpace(winDir) ? null : Path.Combine(winDir, @"System32\OneDriveSetup.exe"),
                string.IsNullOrWhiteSpace(winDir) ? null : Path.Combine(winDir, @"SysWOW64\OneDriveSetup.exe"),
                string.IsNullOrWhiteSpace(pfOneDrive) ? null : Path.Combine(pfOneDrive, "OneDriveSetup.exe"),
                string.IsNullOrWhiteSpace(pf86OneDrive) ? null : Path.Combine(pf86OneDrive, "OneDriveSetup.exe")
            };

            string hklmUn64 = ReadUninstallString(RegistryHive.LocalMachine, MachineUninstallKey64);
            string hklmUn32 = ReadUninstallString(RegistryHive.LocalMachine, MachineUninstallKey32);
            string hkcuUn = ReadUninstallString(userUninstallLoc.Hive, userUninstallLoc.SubKey);

            bool hasPerMachineDir = HasAnyOneDriveBinary(pfOneDrive) || HasAnyOneDriveBinary(pf86OneDrive);
            bool hasPerMachineRun = HasOneDriveRunValue(RegistryHive.LocalMachine, MachineRunKey64)
                || HasOneDriveRunValue(RegistryHive.LocalMachine, MachineRunKey32);
            bool hasPerMachineInstall = !string.IsNullOrWhiteSpace(hklmUn64) || !string.IsNullOrWhiteSpace(hklmUn32) || hasPerMachineDir || hasPerMachineRun;
            bool hasPerUserRun = HasOneDriveRunValue(userRunLoc.Hive, userRunLoc.SubKey);
            bool hasPerUserInstall = !string.IsNullOrWhiteSpace(hkcuUn) || HasAnyOneDriveBinary(interactiveOneDrive) || HasAnyOneDriveBinary(currentUserOneDrive) || hasPerUserRun;

            PackageCache appxCache = AppManager.GetPackageCache();
            bool hasAppxOneDrive = HasMatchingOneDriveAppx(appxCache);

            if (!hasPerMachineInstall && !hasPerUserInstall && !hasAppxOneDrive)
            {
                WinGetUninstallStatus wgStatus = AppManager.InvokeWinGetUninstallWithStatus("Microsoft.OneDrive");
                if (wgStatus == WinGetUninstallStatus.Removed)
                {
                    Logger.Write("INFO", "OneDrive desinstalado via WinGet. Os arquivos locais da pasta OneDrive foram preservados.");
                }
                else if (wgStatus == WinGetUninstallStatus.NotInstalled)
                {
                    Logger.Write("INFO", "OneDrive não encontrado (já removido ou não instalado).");
                }
                return;
            }

            var commands = new List<OneDriveUninstallCommand>();
            var seenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(hklmUn64)) TryAddUninstallCommand(commands, seenCommands, hklmUn64, trustedRoots, true);
            if (!string.IsNullOrWhiteSpace(hklmUn32)) TryAddUninstallCommand(commands, seenCommands, hklmUn32, trustedRoots, true);
            if (hasPerMachineInstall && commands.Count == 0)
            {
                TryAddSystemCandidateCommand(commands, seenCommands, systemCandidates, true);
            }

            if (!isDifferentUser && hasPerUserInstall)
            {
                if (!string.IsNullOrWhiteSpace(hkcuUn))
                {
                    TryAddUninstallCommand(commands, seenCommands, hkcuUn, trustedRoots, false);
                }
                string userLocalSetup = FindOneDriveSetupInDirectory(currentUserOneDrive) ?? FindOneDriveSetupInDirectory(interactiveOneDrive);
                if (!string.IsNullOrWhiteSpace(userLocalSetup) && AppManager.IsTrustedUninstallExecutable(userLocalSetup, "OneDriveSetup.exe", trustedRoots))
                {
                    string key = userLocalSetup + "|/uninstall";
                    if (seenCommands.Add(key))
                    {
                        commands.Add(new OneDriveUninstallCommand { ExePath = userLocalSetup, Arguments = "/uninstall" });
                    }
                }
                if (commands.Count == 0)
                {
                    TryAddSystemCandidateCommand(commands, seenCommands, systemCandidates, false);
                }
            }
            else if (isDifferentUser && hasPerUserInstall)
            {
                Logger.Write("INFO", "Programa elevado com outra conta: o instalador padrão do OneDrive não atua no perfil conectado; aplicando remoção direta segura para o usuário conectado.");
            }

            foreach (OneDriveUninstallCommand cmd in commands)
            {
                var res = SystemInfo.RunProcess(cmd.ExePath, cmd.Arguments, 180000, true, false);
                if (res.ExitCode != 0)
                {
                    Logger.Write("INFO", string.Format("Desinstalador do OneDrive retornou código {0} (0x{1:X8}); verificando remoção complementar.",
                        res.ExitCode, unchecked((uint)res.ExitCode)));
                }
            }

            if (hasAppxOneDrive)
            {
                try
                {
                    AppManager.RemoveAppByDefinition(new AppItem
                    {
                        Id = "OneDriveSync",
                        FriendlyName = "Microsoft OneDrive (Store)",
                        PackageName = "*OneDriveSync*"
                    }, appxCache);
                }
                catch (Exception ex)
                {
                    Logger.Write("AVISO", string.Format("Falha ao remover pacote Store do OneDrive ({0}).", ex.Message));
                }
            }

            StopOneDriveProcesses();

            bool stillHasMachine = RegistrySubKeyExists(RegistryHive.LocalMachine, MachineUninstallKey64)
                || RegistrySubKeyExists(RegistryHive.LocalMachine, MachineUninstallKey32)
                || HasAnyOneDriveBinary(pfOneDrive)
                || HasAnyOneDriveBinary(pf86OneDrive)
                || HasOneDriveRunValue(RegistryHive.LocalMachine, MachineRunKey64)
                || HasOneDriveRunValue(RegistryHive.LocalMachine, MachineRunKey32);

            bool stillHasUser = RegistrySubKeyExists(userUninstallLoc.Hive, userUninstallLoc.SubKey)
                || HasAnyOneDriveBinary(interactiveOneDrive)
                || (!isDifferentUser && HasAnyOneDriveBinary(currentUserOneDrive))
                || HasOneDriveRunValue(userRunLoc.Hive, userRunLoc.SubKey);

            if (!stillHasMachine && !stillHasUser)
            {
                Logger.Write("INFO", "OneDrive desinstalado. Os arquivos locais da pasta OneDrive foram preservados.");
                return;
            }

            if (stillHasMachine)
            {
                AppManager.InvokeWinGetUninstallWithStatus("Microsoft.OneDrive");
                StopOneDriveProcesses();
            }

            DisableOneDriveScheduledTasks();
            UnpinOneDriveFromExplorer(userSid);

            DeleteRunValuesIfPresent(userRunLoc.Hive, userRunLoc.SubKey);
            if (hasPerMachineInstall)
            {
                DeleteRunValuesIfPresent(RegistryHive.LocalMachine, MachineRunKey64);
                DeleteRunValuesIfPresent(RegistryHive.LocalMachine, MachineRunKey32);
            }

            var allowedAppDirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(interactiveOneDrive)) allowedAppDirs.Add(interactiveOneDrive);
            if (!isDifferentUser && !string.IsNullOrWhiteSpace(currentUserOneDrive)) allowedAppDirs.Add(currentUserOneDrive);
            if (hasPerMachineInstall)
            {
                if (!string.IsNullOrWhiteSpace(pfOneDrive)) allowedAppDirs.Add(pfOneDrive);
                if (!string.IsNullOrWhiteSpace(pf86OneDrive)) allowedAppDirs.Add(pf86OneDrive);
            }
            string[] allowedRootsArray = allowedAppDirs.ToArray();

            int inUseDiscard;
            if (!string.IsNullOrWhiteSpace(interactiveOneDrive) && HasAnyOneDriveBinary(interactiveOneDrive))
            {
                PerformanceManager.DeleteDirectoryTreeSafely(interactiveOneDrive, allowedRootsArray, out inUseDiscard);
            }
            if (!isDifferentUser && !string.IsNullOrWhiteSpace(currentUserOneDrive) && HasAnyOneDriveBinary(currentUserOneDrive))
            {
                PerformanceManager.DeleteDirectoryTreeSafely(currentUserOneDrive, allowedRootsArray, out inUseDiscard);
            }
            if (hasPerMachineInstall)
            {
                if (!string.IsNullOrWhiteSpace(pfOneDrive) && HasAnyOneDriveBinary(pfOneDrive))
                {
                    PerformanceManager.DeleteDirectoryTreeSafely(pfOneDrive, allowedRootsArray, out inUseDiscard);
                }
                if (!string.IsNullOrWhiteSpace(pf86OneDrive) && HasAnyOneDriveBinary(pf86OneDrive))
                {
                    PerformanceManager.DeleteDirectoryTreeSafely(pf86OneDrive, allowedRootsArray, out inUseDiscard);
                }
            }

            if (!string.IsNullOrWhiteSpace(interactiveProfile))
            {
                DeleteShortcutIfSafe(Path.Combine(interactiveProfile, @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk"));
            }
            if (!string.IsNullOrWhiteSpace(commonProgData))
            {
                DeleteShortcutIfSafe(Path.Combine(commonProgData, @"Microsoft\Windows\Start Menu\Programs\OneDrive.lnk"));
            }

            if (!HasAnyOneDriveBinary(interactiveOneDrive)
                && (isDifferentUser || !HasAnyOneDriveBinary(currentUserOneDrive)))
            {
                DeleteRegistrySubKeyIfPresent(userUninstallLoc.Hive, userUninstallLoc.SubKey);
            }
            if (hasPerMachineInstall && !HasAnyOneDriveBinary(pfOneDrive) && !HasAnyOneDriveBinary(pf86OneDrive))
            {
                DeleteRegistrySubKeyIfPresent(RegistryHive.LocalMachine, MachineUninstallKey64);
                DeleteRegistrySubKeyIfPresent(RegistryHive.LocalMachine, MachineUninstallKey32);
            }

            bool anyBinaryRemaining = HasAnyOneDriveBinary(interactiveOneDrive)
                || (!isDifferentUser && HasAnyOneDriveBinary(currentUserOneDrive))
                || (hasPerMachineInstall && (HasAnyOneDriveBinary(pfOneDrive) || HasAnyOneDriveBinary(pf86OneDrive)));

            if (anyBinaryRemaining)
            {
                Logger.Write("AVISO", "Parte dos binários do OneDrive permaneceu em uso no disco; a inicialização automática foi desativada e os arquivos pessoais foram preservados.");
            }
            else
            {
                Logger.Write("INFO", "OneDrive removido com limpeza complementar. Os arquivos locais da pasta OneDrive foram preservados.");
            }
        }
    }
}
