using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace Otimizador.Core
{
    public sealed class ProvisionedPackageInfo
    {
        public string DisplayName { get; set; }
        public string PackageName { get; set; }
        public string FamilyName { get; set; }
        public bool FromWinRt { get; set; }
    }

    public sealed class PackageCache
    {
        public List<Package> Installed { get; set; }
        public List<ProvisionedPackageInfo> Provisioned { get; set; }
    }

    public static class AppManager
    {
        private const int WinGetTimeoutMs = 300000;
        private const int WinGetNoPackageFound = -1978335212; // 0x8A150014

        public static string ConvertToPackageWildcard(string pattern)
        {
            return (pattern ?? string.Empty).Trim();
        }

        public static bool IsPackageMatch(string value, string pattern)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern)) return false;
            string cleanPattern = ConvertToPackageWildcard(pattern);
            if (cleanPattern.IndexOf('*') < 0 && cleanPattern.IndexOf('?') < 0)
            {
                return string.Equals(value, cleanPattern, StringComparison.OrdinalIgnoreCase);
            }

            string regexPattern = "^" + Regex.Escape(cleanPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
        }

        private const int WinRtTimeoutMs = 120000;

        private static DeploymentResult WaitWinRtOp(IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> op)
        {
            using (var done = new ManualResetEventSlim(false))
            {
                op.Completed = delegate { done.Set(); };
                if (op.Status != AsyncStatus.Completed && op.Status != AsyncStatus.Error && op.Status != AsyncStatus.Canceled)
                {
                    if (!done.Wait(WinRtTimeoutMs))
                    {
                        try { op.Cancel(); } catch { }
                        throw new TimeoutException(string.Format("Operação do PackageManager excedeu {0} segundos.", WinRtTimeoutMs / 1000));
                    }
                }
                else
                {
                    done.Set();
                }

                DeploymentResult result = null;
                Exception getResultsEx = null;
                try
                {
                    result = op.GetResults();
                }
                catch (Exception ex)
                {
                    getResultsEx = ex;
                }

                if (op.Status == AsyncStatus.Error || getResultsEx != null)
                {
                    string msg = result != null && !string.IsNullOrWhiteSpace(result.ErrorText)
                        ? result.ErrorText
                        : (op.ErrorCode != null
                            ? op.ErrorCode.Message
                            : (getResultsEx != null ? getResultsEx.Message : "Erro desconhecido no PackageManager."));
                    throw new InvalidOperationException(msg);
                }
                return result;
            }
        }

        public static PackageCache GetPackageCache()
        {
            var cache = new PackageCache
            {
                Installed = new List<Package>(),
                Provisioned = new List<ProvisionedPackageInfo>()
            };

            var pm = new PackageManager();
            try
            {
                foreach (Package pkg in pm.FindPackages())
                {
                    cache.Installed.Add(pkg);
                }
            }
            catch
            {
                try
                {
                    foreach (Package pkg in pm.FindPackagesForUser(string.Empty))
                    {
                        cache.Installed.Add(pkg);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write("AVISO", string.Format("Não foi possível listar pacotes Appx instalados ({0}).", ex.Message));
                }
            }

            bool loadedProvisioned = false;
            try
            {
                foreach (Package prov in pm.FindProvisionedPackages())
                {
                    cache.Provisioned.Add(new ProvisionedPackageInfo
                    {
                        DisplayName = prov.Id.Name,
                        PackageName = prov.Id.FullName,
                        FamilyName = prov.Id.FamilyName,
                        FromWinRt = true
                    });
                }
                loadedProvisioned = true;
            }
            catch
            {
            }

            if (!loadedProvisioned)
            {
                try
                {
                    cache.Provisioned = GetProvisionedPackagesViaDism();
                }
                catch (Exception ex)
                {
                    Logger.Write("AVISO", string.Format("Não foi possível listar os pacotes provisionados ({0}).", ex.Message));
                }
            }

            return cache;
        }

        private static List<ProvisionedPackageInfo> GetProvisionedPackagesViaDism()
        {
            var list = new List<ProvisionedPackageInfo>();
            var res = SystemInfo.RunProcess("dism.exe", "/Online /Get-ProvisionedAppxPackages /Format:List", 120000);
            if (res.ExitCode != 0)
            {
                throw new InvalidOperationException(string.Format("DISM retornou código {0}.", res.ExitCode));
            }

            string currentDisplay = null;
            string currentPkg = null;
            foreach (string rawLine in (res.StdOut ?? string.Empty).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                int idx = line.IndexOf(':');
                if (idx <= 0) continue;
                string key = line.Substring(0, idx).Trim();
                string val = line.Substring(idx + 1).Trim();

                if (string.Equals(key, "DisplayName", StringComparison.OrdinalIgnoreCase))
                {
                    currentDisplay = val;
                }
                else if (string.Equals(key, "PackageName", StringComparison.OrdinalIgnoreCase))
                {
                    currentPkg = val;
                    if (!string.IsNullOrWhiteSpace(currentDisplay) || !string.IsNullOrWhiteSpace(currentPkg))
                    {
                        list.Add(new ProvisionedPackageInfo
                        {
                            DisplayName = currentDisplay ?? currentPkg,
                            PackageName = currentPkg,
                            FromWinRt = false
                        });
                    }
                    currentDisplay = null;
                    currentPkg = null;
                }
            }
            return list;
        }

        private static void RemoveProvisionedPackage(PackageManager pm, ProvisionedPackageInfo prov)
        {
            if (prov.FromWinRt && !string.IsNullOrWhiteSpace(prov.FamilyName))
            {
                try
                {
                    WaitWinRtOp(pm.DeprovisionPackageForAllUsersAsync(prov.FamilyName));
                    return;
                }
                catch
                {
                }
            }

            var res = SystemInfo.RunProcess("dism.exe", string.Format("/Online /Remove-ProvisionedAppxPackage /PackageName:\"{0}\" /NoRestart", prov.PackageName), 120000);
            if (res.ExitCode != 0)
            {
                throw new InvalidOperationException(string.Format("DISM falhou ao remover '{0}' (código {1}).", prov.PackageName, res.ExitCode));
            }
        }

        public static void RemoveAppByDefinition(AppItem appInfo, PackageCache cache = null)
        {
            if (cache == null) cache = GetPackageCache();

            var pm = new PackageManager();
            var errors = new List<string>();
            int found = 0;
            var removedInstalled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var removedProvisioned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] patterns = (appInfo.PackageName ?? string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawPattern in patterns)
            {
                string wildPattern = ConvertToPackageWildcard(rawPattern);
                if (string.IsNullOrWhiteSpace(wildPattern)) continue;

                foreach (Package pkg in cache.Installed)
                {
                    if (!IsPackageMatch(pkg.Id.Name, wildPattern)) continue;
                    if (!removedInstalled.Add(pkg.Id.FullName)) continue;

                    found++;
                    try
                    {
                        WaitWinRtOp(pm.RemovePackageAsync(pkg.Id.FullName, RemovalOptions.RemoveForAllUsers));
                    }
                    catch
                    {
                        try
                        {
                            WaitWinRtOp(pm.RemovePackageAsync(pkg.Id.FullName));
                        }
                        catch (Exception ex)
                        {
                            errors.Add(string.Format("{0}: {1}", pkg.Id.Name, ex.Message));
                        }
                    }
                }

                foreach (ProvisionedPackageInfo prov in cache.Provisioned)
                {
                    if (!IsPackageMatch(prov.DisplayName, wildPattern) && !IsPackageMatch(prov.PackageName, wildPattern)) continue;
                    if (!removedProvisioned.Add(prov.PackageName)) continue;

                    found++;
                    try
                    {
                        RemoveProvisionedPackage(pm, prov);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(string.Format("{0} (provisionado): {1}", prov.DisplayName, ex.Message));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(appInfo.WinGetId) && InvokeWinGetUninstall(appInfo.WinGetId))
            {
                found++;
            }

            if (found == 0)
            {
                Logger.Write("INFO", string.Format("{0}: não instalado (nada a remover).", appInfo.FriendlyName));
            }
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" | ", errors.ToArray()));
            }
        }

        public static bool InvokeWinGetUninstall(string winGetId)
        {
            string wingetExe = SystemInfo.FindExecutableInPath("winget.exe");
            if (string.IsNullOrWhiteSpace(wingetExe)) return false;

            string args = string.Format("uninstall --id \"{0}\" --exact --silent --force --disable-interactivity --accept-source-agreements", winGetId);
            var res = SystemInfo.RunProcess(wingetExe, args, WinGetTimeoutMs, true, false);
            if (res.TimedOut)
            {
                Logger.Write("AVISO", string.Format("WinGet excedeu {0} s ao remover '{1}' e foi interrompido.", WinGetTimeoutMs / 1000, winGetId));
                return false;
            }

            if (res.ExitCode == 0)
            {
                Logger.Write("INFO", string.Format("WinGet removeu '{0}'.", winGetId));
                return true;
            }
            else if (res.ExitCode != WinGetNoPackageFound)
            {
                Logger.Write("AVISO", string.Format("WinGet retornou código {0} ao remover '{1}'.", res.ExitCode, winGetId));
            }
            return false;
        }

        public static void RemoveOneDriveDeep()
        {
            foreach (Process proc in Process.GetProcessesByName("OneDrive"))
            {
                try { proc.Kill(); proc.WaitForExit(5000); } catch { }
                finally { proc.Dispose(); }
            }

            string setup = null;
            string arguments = "/uninstall";

            RegistryLocation loc = ChangeJournal.ConvertToRegistryLocation(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe");
            using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(loc.Hive))
            using (RegistryKey key = baseKey.OpenSubKey(loc.SubKey))
            {
                if (key != null)
                {
                    string unStr = Convert.ToString(key.GetValue("UninstallString")) ?? string.Empty;
                    Match m = Regex.Match(unStr, "^\\s*\"([^\"]+)\"\\s*(.*)$");
                    if (m.Success)
                    {
                        setup = m.Groups[1].Value;
                        arguments = m.Groups[2].Value;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(setup))
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string[] candidates = new string[]
                {
                    Path.Combine(winDir, @"System32\OneDriveSetup.exe"),
                    Path.Combine(winDir, @"SysWOW64\OneDriveSetup.exe")
                };
                foreach (string c in candidates)
                {
                    if (File.Exists(c)) { setup = c; break; }
                }
            }

            if (string.IsNullOrWhiteSpace(setup) || !File.Exists(setup))
            {
                Logger.Write("INFO", "OneDrive não encontrado (já removido ou não instalado).");
                return;
            }

            var res = SystemInfo.RunProcess(setup, arguments, 180000, true, false);
            if (res.ExitCode != 0)
            {
                Logger.Write("AVISO", string.Format("Desinstalador do OneDrive retornou código {0}.", res.ExitCode));
            }
            else
            {
                Logger.Write("INFO", "OneDrive desinstalado. Os arquivos locais da pasta OneDrive foram preservados.");
            }
        }
    }
}
