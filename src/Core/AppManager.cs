using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

    public enum WinGetUninstallStatus
    {
        NotInstalled,
        Removed,
        Failed
    }

    internal sealed class DismMissingPackageEntry
    {
        public string DisplayName { get; set; }
        public string PackageName { get; set; }
        public int ExitCode { get; set; }
    }

    public static class AppManager
    {
        private const int WinGetTimeoutMs = 300000;
        private const int WinGetNoPackageFound = -1978335212; // 0x8A150014
        private const int WinRtTimeoutMs = 120000;
        private static readonly Regex _winGetIdRegex = new Regex("^[A-Za-z0-9][A-Za-z0-9._+-]*$");
        private static readonly Regex _ansiEscapeRegex = new Regex(@"\x1B\[[0-9;?]*[A-Za-z]");

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

        /// <summary>
        /// Extrai o PackageFamilyName (&lt;Nome&gt;_&lt;PublisherId&gt;) a partir de um PackageFullName de 5 segmentos.
        /// </summary>
        public static string TryExtractPackageFamilyName(string packageFullName)
        {
            if (string.IsNullOrWhiteSpace(packageFullName)) return null;
            string[] parts = packageFullName.Trim().Split('_');
            if (parts.Length != 5) return null;
            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[4])) return null;
            return parts[0].Trim() + "_" + parts[4].Trim();
        }

        public static bool IsDismMissingPackageExitCode(int exitCode)
        {
            return exitCode == 2 || exitCode == 3;
        }

        /// <summary>
        /// Classifica falhas do DISM por arquivo/caminho ausente como AVISO quando nenhuma cópia instalada falhou.
        /// </summary>
        public static string ClassifyProvisionedDismOutcome(int exitCode, bool hasInstalledRemovalError)
        {
            if (IsDismMissingPackageExitCode(exitCode) && !hasInstalledRemovalError)
            {
                return "AVISO";
            }
            return "ERRO";
        }

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
                            FamilyName = TryExtractPackageFamilyName(currentPkg),
                            FromWinRt = false
                        });
                    }
                    currentDisplay = null;
                    currentPkg = null;
                }
            }
            return list;
        }

        private static string NormalizeSingleLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string collapsed = Regex.Replace(text, @"[\r\n\t]+", " ").Trim();
            return collapsed.Length > 220 ? collapsed.Substring(0, 217) + "..." : collapsed;
        }

        private static bool TryRemoveProvisionedPackage(PackageManager pm, ProvisionedPackageInfo prov, out int missingDismExitCode)
        {
            missingDismExitCode = 0;

            string familyName = !string.IsNullOrWhiteSpace(prov.FamilyName)
                ? prov.FamilyName
                : TryExtractPackageFamilyName(prov.PackageName);

            if (!string.IsNullOrWhiteSpace(familyName))
            {
                try
                {
                    WaitWinRtOp(pm.DeprovisionPackageForAllUsersAsync(familyName));
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Write("INFO", string.Format("Desprovisionamento WinRT indisponível para '{0}' ({1}); tentando via DISM.",
                        prov.DisplayName, NormalizeSingleLine(ex.Message)));
                }
            }

            string dismExe = SystemInfo.ResolveSystemExecutable("dism.exe");
            var res = SystemInfo.RunProcess(dismExe, string.Format("/Online /Remove-ProvisionedAppxPackage /PackageName:\"{0}\" /NoRestart", prov.PackageName), 120000);
            if (res.ExitCode == 0)
            {
                return true;
            }
            if (IsDismMissingPackageExitCode(res.ExitCode))
            {
                missingDismExitCode = res.ExitCode;
                return false;
            }
            throw new InvalidOperationException(string.Format("DISM falhou ao remover '{0}' (código {1}).", prov.PackageName, res.ExitCode));
        }

        private static bool IsProtectedSystemShellPackage(Package pkg)
        {
            if (pkg == null) return false;
            try
            {
                if (pkg.IsFramework) return true;
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (string.IsNullOrWhiteSpace(winDir) || pkg.InstalledLocation == null) return false;
                string installedPath = pkg.InstalledLocation.Path;
                if (string.IsNullOrWhiteSpace(installedPath)) return false;
                string systemAppsRoot = Path.Combine(winDir, "SystemApps");
                return PerformanceManager.TestPathIsWithin(installedPath, systemAppsRoot);
            }
            catch
            {
                return false;
            }
        }

        public static void RemoveAppByDefinition(AppItem appInfo, PackageCache cache = null)
        {
            if (cache == null) cache = GetPackageCache();

            var pm = new PackageManager();
            var errors = new List<string>();
            var missingDismEntries = new List<DismMissingPackageEntry>();
            int found = 0;
            int installedErrorCount = 0;

            var matchedInstalled = new List<Package>();
            var seenInstalled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matchedProvisioned = new List<ProvisionedPackageInfo>();
            var seenProvisioned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] patterns = (appInfo.PackageName ?? string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawPattern in patterns)
            {
                string wildPattern = ConvertToPackageWildcard(rawPattern);
                if (string.IsNullOrWhiteSpace(wildPattern)) continue;

                foreach (ProvisionedPackageInfo prov in cache.Provisioned)
                {
                    if (!IsPackageMatch(prov.DisplayName, wildPattern) && !IsPackageMatch(prov.PackageName, wildPattern)) continue;
                    if (seenProvisioned.Add(prov.PackageName))
                    {
                        matchedProvisioned.Add(prov);
                    }
                }

                foreach (Package pkg in cache.Installed)
                {
                    if (!IsPackageMatch(pkg.Id.Name, wildPattern)) continue;
                    if (IsProtectedSystemShellPackage(pkg))
                    {
                        Logger.Write("INFO", string.Format("Componente protegido do sistema preservado ({0}).", pkg.Id.Name));
                        continue;
                    }
                    if (seenInstalled.Add(pkg.Id.FullName))
                    {
                        matchedInstalled.Add(pkg);
                    }
                }
            }

            // 1. Desprovisiona primeiro para preservar manifestos/arquivos em disco durante a remoção global
            foreach (ProvisionedPackageInfo prov in matchedProvisioned)
            {
                found++;
                try
                {
                    int missingCode;
                    if (!TryRemoveProvisionedPackage(pm, prov, out missingCode))
                    {
                        missingDismEntries.Add(new DismMissingPackageEntry
                        {
                            DisplayName = prov.DisplayName,
                            PackageName = prov.PackageName,
                            ExitCode = missingCode
                        });
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(string.Format("{0} (provisionado): {1}", prov.DisplayName, NormalizeSingleLine(ex.Message)));
                }
            }

            // 2. Remove pacotes instalados nos perfis existentes
            foreach (Package pkg in matchedInstalled)
            {
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
                        installedErrorCount++;
                        errors.Add(string.Format("{0}: {1}", pkg.Id.Name, NormalizeSingleLine(ex.Message)));
                    }
                }
            }

            // 3. Só avalia DISM código 2/3 após saber se alguma cópia instalada falhou ao ser removida
            foreach (DismMissingPackageEntry missing in missingDismEntries)
            {
                string outcome = ClassifyProvisionedDismOutcome(missing.ExitCode, installedErrorCount > 0);
                if (outcome == "AVISO")
                {
                    Logger.Write("AVISO", string.Format(
                        "Pacote provisionado '{0}' sem arquivos ou manifesto no disco (registro órfão/stub do Windows, DISM código {1}); nenhuma cópia instalada permaneceu ativa.",
                        missing.DisplayName, missing.ExitCode));
                }
                else
                {
                    errors.Add(string.Format("{0} (provisionado): DISM falhou ao remover '{1}' (código {2}).",
                        missing.DisplayName, missing.PackageName, missing.ExitCode));
                }
            }

            bool winGetFailed = false;
            if (!string.IsNullOrWhiteSpace(appInfo.WinGetId))
            {
                WinGetUninstallStatus wgStatus = InvokeWinGetUninstallWithStatus(appInfo.WinGetId);
                if (wgStatus == WinGetUninstallStatus.Removed)
                {
                    found++;
                }
                else if (wgStatus == WinGetUninstallStatus.Failed)
                {
                    winGetFailed = true;
                }
            }

            if (found == 0 && !winGetFailed)
            {
                Logger.Write("INFO", string.Format("{0}: não instalado (nada a remover).", appInfo.FriendlyName));
            }
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" | ", errors.ToArray()));
            }
        }

        public static bool IsValidWinGetPackageId(string winGetId)
        {
            if (string.IsNullOrWhiteSpace(winGetId)) return false;
            return _winGetIdRegex.IsMatch(winGetId);
        }

        public static bool TryParseUninstallCommand(string uninstallString, out string exePath, out string arguments)
        {
            exePath = null;
            arguments = string.Empty;
            if (string.IsNullOrWhiteSpace(uninstallString)) return false;

            Match quoted = Regex.Match(uninstallString, "^\\s*\"([^\"]+)\"\\s*(.*)$");
            if (quoted.Success)
            {
                exePath = quoted.Groups[1].Value;
                arguments = (quoted.Groups[2].Value ?? string.Empty).Trim();
                return !string.IsNullOrWhiteSpace(exePath);
            }

            Match plain = Regex.Match(uninstallString, @"^\s*(\S+)\s*(.*)$");
            if (plain.Success)
            {
                exePath = plain.Groups[1].Value;
                arguments = (plain.Groups[2].Value ?? string.Empty).Trim();
                return !string.IsNullOrWhiteSpace(exePath);
            }

            return false;
        }

        public static string ExtractLastDiagnosticLine(string stdErr, string stdOut)
        {
            string[] sources = new string[] { stdErr, stdOut };
            foreach (string raw in sources)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string cleaned = _ansiEscapeRegex.Replace(raw, string.Empty);
                string[] lines = cleaned.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line == "-" || line == "\\" || line == "|" || line == "/") continue;
                    if (Regex.IsMatch(line, @"^[\s\-\|\\/\.\u2580-\u259F]+$")) continue;
                    if (line.Length > 220) line = line.Substring(0, 217) + "...";
                    return line;
                }
            }
            return string.Empty;
        }

        public static bool IsTrustedUninstallExecutable(string exePath, string expectedExeName, string[] allowedRoots)
        {
            if (string.IsNullOrWhiteSpace(exePath) || string.IsNullOrWhiteSpace(expectedExeName) || allowedRoots == null)
            {
                return false;
            }
            if (!Path.IsPathRooted(exePath)) return false;

            string fullExe;
            try
            {
                fullExe = Path.GetFullPath(exePath);
            }
            catch
            {
                return false;
            }

            if (!string.Equals(Path.GetFileName(fullExe), expectedExeName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (!File.Exists(fullExe)) return false;

            foreach (string root in allowedRoots)
            {
                if (!string.IsNullOrWhiteSpace(root) && PerformanceManager.TestPathIsWithin(fullExe, root))
                {
                    return true;
                }
            }
            return false;
        }

        public static WinGetUninstallStatus InvokeWinGetUninstallWithStatus(string winGetId)
        {
            if (!IsValidWinGetPackageId(winGetId))
            {
                Logger.Write("AVISO", string.Format("Identificador WinGet inválido ignorado: '{0}'.", winGetId ?? string.Empty));
                return WinGetUninstallStatus.Failed;
            }

            string wingetExe = SystemInfo.FindExecutableInPath("winget.exe");
            if (string.IsNullOrWhiteSpace(wingetExe)) return WinGetUninstallStatus.NotInstalled;

            string args = string.Format("uninstall --id \"{0}\" --exact --silent --force --disable-interactivity --accept-source-agreements", winGetId);
            var res = SystemInfo.RunProcess(wingetExe, args, WinGetTimeoutMs, true, true, Encoding.UTF8);
            if (res.TimedOut)
            {
                Logger.Write("AVISO", string.Format("WinGet excedeu {0} s ao remover '{1}' e foi interrompido.", WinGetTimeoutMs / 1000, winGetId));
                return WinGetUninstallStatus.Failed;
            }

            if (res.ExitCode == 0)
            {
                Logger.Write("INFO", string.Format("WinGet removeu '{0}'.", winGetId));
                return WinGetUninstallStatus.Removed;
            }
            if (res.ExitCode == WinGetNoPackageFound)
            {
                return WinGetUninstallStatus.NotInstalled;
            }

            string detail = ExtractLastDiagnosticLine(res.StdErr, res.StdOut);
            if (string.IsNullOrWhiteSpace(detail))
            {
                Logger.Write("AVISO", string.Format("WinGet retornou código {0} (0x{1:X8}) ao remover '{2}'.",
                    res.ExitCode, unchecked((uint)res.ExitCode), winGetId));
            }
            else
            {
                Logger.Write("AVISO", string.Format("WinGet retornou código {0} (0x{1:X8}) ao remover '{2}': {3}",
                    res.ExitCode, unchecked((uint)res.ExitCode), winGetId, detail));
            }
            return WinGetUninstallStatus.Failed;
        }

        public static bool InvokeWinGetUninstall(string winGetId)
        {
            return InvokeWinGetUninstallWithStatus(winGetId) == WinGetUninstallStatus.Removed;
        }

        public static void RemoveOneDriveDeep(string userSid = null)
        {
            OneDriveManager.RemoveOneDriveDeep(userSid);
        }
    }
}
