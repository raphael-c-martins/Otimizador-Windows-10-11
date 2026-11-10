using System;
using System.Collections.Generic;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public static class SecurityManager
    {
        public const string LanmanParametersPath = @"HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters";
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
    }
}
