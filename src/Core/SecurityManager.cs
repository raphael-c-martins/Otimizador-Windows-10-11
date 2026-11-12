using System;
using System.Collections.Generic;
using System.Management;
using System.Security;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public sealed class RegistrySettingSpec
    {
        public string Path { get; private set; }
        public string Name { get; private set; }
        public int Value { get; private set; }

        public RegistrySettingSpec(string path, string name, int value)
        {
            Path = path;
            Name = name;
            Value = value;
        }
    }

    public static class SecurityManager
    {
        public const string LanmanParametersPath = @"HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters";
        public const string LanmanWorkstationServicePath = @"HKLM:\SYSTEM\CurrentControlSet\Services\LanmanWorkstation";
        public const string LanmanWorkstationParametersPath = @"HKLM:\SYSTEM\CurrentControlSet\Services\LanmanWorkstation\Parameters";
        public const string LanmanWorkstationPolicyPath = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\LanmanWorkstation";
        public const string MrxSmb10ServicePath = @"HKLM:\SYSTEM\CurrentControlSet\Services\mrxsmb10";
        public const string WDigestPath = @"HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\WDigest";
        public const string DnsClientPolicyPath = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient";
        public const string ExplorerMachinePolicyPath = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        public const string ExplorerUserPolicyPath = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        public const string AutoplayHandlersUserPath = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers";
        public const string LsaPath = @"HKLM:\SYSTEM\CurrentControlSet\Control\Lsa";
        public const string RemoteAssistancePath = @"HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance";
        public const string DefenderPolicyPath = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender";
        public const string DefenderMpEnginePolicyPath = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\MpEngine";
        private static readonly Regex _adminShareRegex = new Regex(@"^(?:ADMIN\$|[A-Z]\$)$", RegexOptions.IgnoreCase);

        public static string GetAutoShareValueName(bool isServer)
        {
            return isServer ? "AutoShareServer" : "AutoShareWks";
        }

        public static bool IsAdministrativeDefaultShareName(string shareName)
        {
            if (string.IsNullOrWhiteSpace(shareName)) return false;
            return _adminShareRegex.IsMatch(shareName.Trim());
        }

        public static List<string> FilterAdministrativeShareNames(IEnumerable<string> shareNames)
        {
            var found = new List<string>();
            if (shareNames == null) return found;
            foreach (string name in shareNames)
            {
                if (IsAdministrativeDefaultShareName(name))
                {
                    found.Add(name.Trim());
                }
            }
            return found;
        }

        public static List<string> ParseNetShareOutput(string output)
        {
            var shares = new List<string>();
            if (string.IsNullOrWhiteSpace(output)) return shares;

            bool pastSeparator = false;
            string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("---"))
                {
                    pastSeparator = true;
                    continue;
                }
                if (!pastSeparator) continue;

                // Linhas de continuação de caminho/observação começam com espaço
                if (line.Length > 0 && char.IsWhiteSpace(line[0])) continue;

                // Ignora linha final de status do net.exe ("Comando concluído com êxito." / "The command completed successfully.")
                if (trimmed.EndsWith(".") && trimmed.IndexOf('$') < 0 && trimmed.IndexOf(' ') > 0)
                {
                    string lower = trimmed.ToLowerInvariant();
                    if (lower.Contains("comando") || lower.Contains("command")) continue;
                }

                // Nome do compartilhamento é o trecho antes de 2+ espaços, ou a linha inteira se quebrou para a linha seguinte
                Match m = Regex.Match(line, @"^([^\s].*?)(?:\s{2,}|$)");
                if (m.Success)
                {
                    string shareName = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(shareName))
                    {
                        shares.Add(shareName);
                    }
                }
            }

            return shares;
        }

        public static List<string> GetActiveShareNames()
        {
            var shares = new List<string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Share"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"] as string;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            shares.Add(name.Trim());
                        }
                    }
                }
                return shares;
            }
            catch
            {
            }

            var res = SystemInfo.RunProcess("net.exe", "share", 15000, true, true, Encoding.GetEncoding(850));
            if (res.ExitCode == 0)
            {
                return ParseNetShareOutput(res.StdOut);
            }
            return shares;
        }

        public static void RestartServiceIfRunning(string serviceName)
        {
            if (!ChangeJournal.ServiceExists(serviceName)) return;

            using (var sc = new ServiceController(serviceName))
            {
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    Logger.Write("INFO", string.Format("Serviço '{0}' não está em execução; reinício não necessário.", serviceName));
                    return;
                }
            }

            RestartServiceWithDependents(serviceName);
        }

        public static List<string> StopServiceWithDependents(string serviceName)
        {
            var stoppedDependents = new List<string>();
            CollectRunningDependentsInStopOrder(serviceName, stoppedDependents, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            foreach (string depName in stoppedDependents)
            {
                using (var dep = new ServiceController(depName))
                {
                    if (dep.Status == ServiceControllerStatus.Running)
                    {
                        dep.Stop();
                        dep.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                }
            }

            using (var sc = new ServiceController(serviceName))
            {
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
            }

            return stoppedDependents;
        }

        public static void RestartServiceWithDependents(string serviceName)
        {
            List<string> stoppedDependents = StopServiceWithDependents(serviceName);

            using (var sc = new ServiceController(serviceName))
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }

            // Sobe os dependentes na ordem inversa em que foram parados
            for (int i = stoppedDependents.Count - 1; i >= 0; i--)
            {
                string depName = stoppedDependents[i];
                try
                {
                    using (var dep = new ServiceController(depName))
                    {
                        if (dep.Status != ServiceControllerStatus.Running)
                        {
                            dep.Start();
                            dep.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write("AVISO", string.Format("Serviço dependente '{0}' não reiniciou automaticamente ({1}).", depName, ex.Message));
                }
            }
        }

        private static void CollectRunningDependentsInStopOrder(
            string serviceName,
            List<string> stopOrder,
            HashSet<string> visited)
        {
            using (var sc = new ServiceController(serviceName))
            {
                foreach (ServiceController dep in sc.DependentServices)
                {
                    using (dep)
                    {
                        if (!visited.Add(dep.ServiceName)) continue;
                        if (dep.Status == ServiceControllerStatus.Running)
                        {
                            CollectRunningDependentsInStopOrder(dep.ServiceName, stopOrder, visited);
                            stopOrder.Add(dep.ServiceName);
                        }
                    }
                }
            }
        }

        public static void DisableAdminShares(bool isServer)
        {
            string valueName = GetAutoShareValueName(isServer);
            Logger.Write("INFO", string.Format("Sistema detectado: {0} (parâmetro {1} = 0).",
                isServer ? "Windows Server" : "Windows Cliente", valueName));

            // Registra reinício antes da chave no diário: no Undo (ordem inversa), a chave volta primeiro e o LanmanServer reinicia depois.
            ChangeJournal.RegisterServiceRestartForUndo("LanmanServer");
            ChangeJournal.SetRegistryValue(LanmanParametersPath, valueName, 0, RegistryValueKind.DWord);

            if (!ChangeJournal.ServiceExists("LanmanServer"))
            {
                Logger.Write("INFO", "Política aplicada no Registro. Serviço LanmanServer não encontrado neste sistema.");
                return;
            }

            bool isRunning;
            using (var sc = new ServiceController("LanmanServer"))
            {
                isRunning = (sc.Status == ServiceControllerStatus.Running);
            }

            if (!isRunning)
            {
                Logger.Write("INFO", string.Format("Política {0}=0 gravada no Registro. Serviço LanmanServer não está em execução; nenhuma conexão SMB foi interrompida.", valueName));
                return;
            }

            Logger.Write("INFO", "Reiniciando serviço LanmanServer para aplicar a desativação de C$, D$ e ADMIN$ (conexões SMB ativas podem ser interrompidas temporariamente)...");
            RestartServiceWithDependents("LanmanServer");

            List<string> activeShares = GetActiveShareNames();
            List<string> remainingAdmin = FilterAdministrativeShareNames(activeShares);
            if (remainingAdmin.Count > 0)
            {
                Logger.Write("AVISO", string.Format("Compartilhamentos administrativos ainda listados após reiniciar o LanmanServer: {0}. Pode haver política de domínio (GPO) ou necessidade de reinicialização.",
                    string.Join(", ", remainingAdmin.ToArray())));
            }
            else
            {
                string preserved = activeShares.Count > 0 ? string.Join(", ", activeShares.ToArray()) : "nenhum";
                Logger.Write("INFO", string.Format("Confirmação de compartilhamentos ativos: C$, D$ e ADMIN$ removidos. Ativos preservados: {0}.", preserved));
            }
        }

        public static string BuildMappedTestPath(string originalPath, string hkcuSubKeyPrefix)
        {
            if (string.IsNullOrWhiteSpace(originalPath))
            {
                throw new ArgumentException("Caminho de registro original inválido.");
            }
            if (string.IsNullOrWhiteSpace(hkcuSubKeyPrefix))
            {
                return originalPath;
            }
            string cleanedPrefix = hkcuSubKeyPrefix.Trim('\\');
            string relative = originalPath.Replace(":\\", "\\");
            return string.Format(@"HKCU:\{0}\{1}", cleanedPrefix, relative);
        }

        public static void ApplyRegistrySettings(IEnumerable<RegistrySettingSpec> settings, string hkcuTestSubKeyPrefix = null)
        {
            if (settings == null) return;
            foreach (RegistrySettingSpec spec in settings)
            {
                string targetPath = BuildMappedTestPath(spec.Path, hkcuTestSubKeyPrefix);
                ChangeJournal.SetRegistryValue(targetPath, spec.Name, spec.Value, RegistryValueKind.DWord);
            }
        }

        public static string[] FilterDependOnService(string[] currentDependencies, string driverToRemove)
        {
            if (currentDependencies == null || string.IsNullOrWhiteSpace(driverToRemove)) return null;

            bool removed = false;
            var kept = new List<string>();
            foreach (string dep in currentDependencies)
            {
                if (string.Equals(dep, driverToRemove, StringComparison.OrdinalIgnoreCase))
                {
                    removed = true;
                }
                else if (!string.IsNullOrWhiteSpace(dep))
                {
                    kept.Add(dep);
                }
            }
            return removed ? kept.ToArray() : null;
        }

        public static RegistrySettingSpec[] GetSMBv1ServerSettings()
        {
            return new RegistrySettingSpec[]
            {
                new RegistrySettingSpec(LanmanParametersPath, "SMB1", 0)
            };
        }

        public static RegistrySettingSpec[] GetWDigestSettings()
        {
            return new RegistrySettingSpec[]
            {
                new RegistrySettingSpec(WDigestPath, "UseLogonCredential", 0)
            };
        }

        public static RegistrySettingSpec[] GetLLMNRSettings()
        {
            return new RegistrySettingSpec[]
            {
                new RegistrySettingSpec(DnsClientPolicyPath, "EnableMulticast", 0)
            };
        }

        public static RegistrySettingSpec[] GetAutoRunAndAutoPlaySettings()
        {
            return new RegistrySettingSpec[]
            {
                new RegistrySettingSpec(ExplorerMachinePolicyPath, "NoDriveTypeAutoRun", 255),
                new RegistrySettingSpec(ExplorerMachinePolicyPath, "NoAutorun", 1),
                new RegistrySettingSpec(ExplorerUserPolicyPath, "NoDriveTypeAutoRun", 255),
                new RegistrySettingSpec(ExplorerUserPolicyPath, "NoAutorun", 1),
                new RegistrySettingSpec(AutoplayHandlersUserPath, "DisableAutoplay", 1)
            };
        }

        public static RegistrySettingSpec[] GetAnonymousAccessSettings()
        {
            return new RegistrySettingSpec[]
            {
                new RegistrySettingSpec(LsaPath, "RestrictAnonymous", 1),
                new RegistrySettingSpec(LsaPath, "RestrictAnonymousSAM", 1)
            };
        }

        public static RegistrySettingSpec[] GetSMBInsecureGuestAuthSettings()
        {
            return new RegistrySettingSpec[]
            {
                new RegistrySettingSpec(LanmanWorkstationParametersPath, "AllowInsecureGuestAuth", 0),
                new RegistrySettingSpec(LanmanWorkstationPolicyPath, "AllowInsecureGuestAuth", 0)
            };
        }

        public static RegistrySettingSpec[] GetRemoteAssistanceSettings()
        {
            return new RegistrySettingSpec[]
            {
                new RegistrySettingSpec(RemoteAssistancePath, "fAllowToGetHelp", 0),
                new RegistrySettingSpec(RemoteAssistancePath, "fAllowFullControl", 0)
            };
        }

        public static RegistrySettingSpec[] GetDefenderPUAProtectionSettings()
        {
            return new RegistrySettingSpec[]
            {
                new RegistrySettingSpec(DefenderPolicyPath, "PUAProtection", 1),
                new RegistrySettingSpec(DefenderMpEnginePolicyPath, "MpEnablePus", 1)
            };
        }

        public static void DisableSMBv1()
        {
            ApplyRegistrySettings(GetSMBv1ServerSettings());

            RegistryLocation workstationLoc = ChangeJournal.ConvertToRegistryLocation(LanmanWorkstationServicePath);
            using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(workstationLoc.Hive))
            using (RegistryKey key = baseKey.OpenSubKey(workstationLoc.SubKey))
            {
                if (key != null)
                {
                    string[] deps = key.GetValue("DependOnService") as string[];
                    string[] filtered = FilterDependOnService(deps, "MRxSmb10");
                    if (filtered != null)
                    {
                        ChangeJournal.SetRegistryValue(LanmanWorkstationServicePath, "DependOnService", filtered, RegistryValueKind.MultiString);
                    }
                }
            }

            if (ChangeJournal.TestRegistryKeyExists(MrxSmb10ServicePath))
            {
                ChangeJournal.SetRegistryValue(MrxSmb10ServicePath, "Start", 4, RegistryValueKind.DWord);
                Logger.Write("INFO", "Protocolo SMBv1 desativado no servidor (SMB1=0) e driver cliente mrxsmb10 desabilitado (Start=4). Reinicie o Windows para efeito completo.");
            }
            else
            {
                Logger.Write("INFO", "Protocolo SMBv1 desativado no servidor (SMB1=0). Driver mrxsmb10 não está instalado neste sistema; reinicie o Windows se necessário.");
            }
        }

        public static void DisableWDigest()
        {
            ApplyRegistrySettings(GetWDigestSettings());
            Logger.Write("INFO", "Armazenamento de senhas em texto claro no WDigest bloqueado (UseLogonCredential=0).");
        }

        public static void DisableLLMNR()
        {
            ApplyRegistrySettings(GetLLMNRSettings());
            Logger.Write("INFO", "Resolução de nomes multicast LLMNR desativada por diretiva (EnableMulticast=0).");
        }

        public static void DisableAutoRunAndAutoPlay()
        {
            ApplyRegistrySettings(GetAutoRunAndAutoPlaySettings());
            Logger.Write("INFO", "AutoRun e Reprodução Automática desativados para todas as unidades no sistema e no usuário atual.");
        }

        public static void DisableAnonymousAccess()
        {
            ApplyRegistrySettings(GetAnonymousAccessSettings());
            Logger.Write("INFO", "Acesso anônimo e enumeração de contas SAM restringidos no LSA (RestrictAnonymous=1, RestrictAnonymousSAM=1).");
        }

        public static void DisableSMBInsecureGuestAuth()
        {
            ApplyRegistrySettings(GetSMBInsecureGuestAuthSettings());
            Logger.Write("INFO", "Logon de convidado inseguro sem senha no cliente SMB desativado (AllowInsecureGuestAuth=0).");
        }

        public static void DisableRemoteAssistance()
        {
            ApplyRegistrySettings(GetRemoteAssistanceSettings());
            Logger.Write("INFO", "Assistência Remota do Windows desativada (fAllowToGetHelp=0, fAllowFullControl=0).");
        }

        public static void EnableDefenderPUAProtection()
        {
            try
            {
                ApplyRegistrySettings(GetDefenderPUAProtectionSettings());
                Logger.Write("INFO", "Proteção contra aplicativos potencialmente indesejados (PUA) habilitada nas diretivas do Microsoft Defender.");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Write("AVISO", string.Format("Não foi possível gravar a diretiva de proteção PUA no Microsoft Defender ({0}). Verifique se a Proteção contra Adulterações ou política corporativa bloqueia alterações.", ex.Message));
            }
            catch (SecurityException ex)
            {
                Logger.Write("AVISO", string.Format("Permissão negada ao configurar proteção PUA no Microsoft Defender ({0}).", ex.Message));
            }
        }
    }
}
