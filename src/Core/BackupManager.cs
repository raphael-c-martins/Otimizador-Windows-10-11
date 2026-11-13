using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public sealed class RestorePointInfo
    {
        public string Description { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public sealed class RestorePointRecentAvailableException : InvalidOperationException
    {
        public RestorePointInfo RecentPoint { get; private set; }

        public RestorePointRecentAvailableException(string message, RestorePointInfo recentPoint)
            : base(message)
        {
            RecentPoint = recentPoint;
        }
    }

    public static class BackupManager
    {
        private const uint BeginSystemChange = 100;
        private const uint ModifySettings = 12;
        private const int MaxLabelLength = 32;
        private const string SystemRestorePolicyKey = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";
        private const string SystemRestoreConfigKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        private const string FrequencyValueName = "SystemRestorePointCreationFrequency";

        public static bool InvokeSystemRestorePoint(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Descrição do Ponto de Restauração não informada.");
            }

            Logger.Write("INFO", "Verificando a Proteção do Sistema e criando o Ponto de Restauração...");
            if (SystemInfo.GetOSInfo().IsServer)
            {
                throw new InvalidOperationException("O Windows Server não possui o recurso Restauração do Sistema (SystemRestore).");
            }
            EnsureNotBlockedByPolicy();

            DateTime startedAt = DateTime.Now;
            bool vssWasDisabled = PrepareServiceIfDisabled("VSS", "Cópias de Sombra de Volume (VSS)", true);
            bool swprvWasDisabled = PrepareServiceIfDisabled("swprv", "Provedor de Cópias de Sombra de Software da Microsoft (swprv)", false);
            try
            {
                string sysDrive = (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\").TrimEnd('\\') + "\\";
                EnableSystemProtection(sysDrive);
                EnsureShadowStorageAllocated(sysDrive);
                CreateRestorePointWithFrequencyBypass(description);
            }
            finally
            {
                if (swprvWasDisabled)
                {
                    TryRestoreServiceDisabled("swprv");
                }
                if (vssWasDisabled)
                {
                    TryRestoreServiceDisabled("VSS");
                }
            }

            RestorePointInfo match = WaitForRestorePoint(description, startedAt);
            if (match == null)
            {
                RestorePointInfo newest = FindNewestRestorePoint();
                string sysDriveDiag = (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\").TrimEnd('\\');
                string diagnostic = CollectRestoreDiagnostics(sysDriveDiag);
                string message = BuildNotConfirmedMessage(newest, diagnostic);
                if (IsRecentRestorePoint(newest, DateTime.Now, 24))
                {
                    throw new RestorePointRecentAvailableException(message, newest);
                }
                throw new InvalidOperationException(message);
            }

            Logger.Write("OK", string.Format("Ponto de Restauração criado e confirmado: '{0}' ({1:dd/MM/yyyy HH:mm:ss}).", match.Description, match.CreationTime));
            return true;
        }

        public static string NewRestorePointDescription(string label = "Otimizador")
        {
            return string.Format("Otimizador_{0}_{1}", SanitizeRestorePointLabel(label), DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        }

        // O rótulo pode vir do PresetName de um perfil .json personalizado (entrada não confiável):
        // mantém só letras ASCII, dígitos, '_' e '-', com limite de tamanho, para a descrição não ser truncada pelo Windows
        // (o que faria a confirmação por descrição falhar).
        public static string SanitizeRestorePointLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return "Personalizado";

            var sb = new StringBuilder();
            foreach (char ch in label.Trim())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-')
                {
                    sb.Append(ch);
                }
                else if (char.IsWhiteSpace(ch) && sb.Length > 0 && sb[sb.Length - 1] != '_')
                {
                    sb.Append('_');
                }
                if (sb.Length >= MaxLabelLength) break;
            }

            string result = sb.ToString().Trim('_');
            return result.Length == 0 ? "Personalizado" : result;
        }

        public static bool IsMatchingRestorePoint(string foundDescription, DateTime foundCreation, string expectedDescription, DateTime startedAt)
        {
            if (!string.Equals(foundDescription, expectedDescription, StringComparison.Ordinal)) return false;
            // Se o WMI não retornou CreationTime válido, a descrição exclusiva com timestamp já confirma a criação.
            if (foundCreation == DateTime.MinValue) return true;
            // Tolerância para diferenças de relógio/arredondamento entre o WMI e o processo
            return foundCreation >= startedAt.AddMinutes(-2);
        }

        public static bool IsRecentRestorePoint(RestorePointInfo rp, DateTime now, int maxAgeHours = 24)
        {
            if (rp == null || rp.CreationTime == DateTime.MinValue || maxAgeHours <= 0) return false;
            TimeSpan age = now - rp.CreationTime;
            return age <= TimeSpan.FromHours(maxAgeHours) && age >= TimeSpan.FromMinutes(-5);
        }

        public static string BuildNotConfirmedMessage(RestorePointInfo newest, string extraDiagnostic = null)
        {
            string baseMsg = "O Windows aceitou o pedido, mas não registrou um novo Ponto de Restauração.";
            string message;
            if (newest != null)
            {
                message = string.Format("{0} Provável causa: o Windows ignora pontos novos quando já existe um recente (evento 8216 do System Restore). Ponto mais recente disponível: '{1}' em {2:dd/MM/yyyy HH:mm}.",
                    baseMsg, newest.Description, newest.CreationTime);
            }
            else
            {
                message = baseMsg + " Verifique se a Proteção do Sistema está ativa e se há espaço reservado para pontos de restauração.";
            }
            if (!string.IsNullOrWhiteSpace(extraDiagnostic))
            {
                message = message + " Detalhe do Windows: " + extraDiagnostic.Trim();
            }
            return message;
        }

        public static bool NeedsShadowStorageSetup(string vssOutput)
        {
            if (string.IsNullOrWhiteSpace(vssOutput)) return true;

            var metricValues = new List<string>();
            string maxCandidate = null;
            string[] lines = vssOutput.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                int colonIndex = rawLine.IndexOf(':');
                if (colonIndex <= 0 || colonIndex >= rawLine.Length - 1) continue;

                string label = rawLine.Substring(0, colonIndex).Trim();
                string value = rawLine.Substring(colonIndex + 1).Trim();
                bool isMetricValue =
                    value.IndexOf("UNBOUNDED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("ILIMITADO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("SEM LIMITE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (char.IsDigit(value[0]) && Regex.IsMatch(value, @"(?:bytes?|kb|mb|gb|tb|%)", RegexOptions.IgnoreCase));
                if (!isMetricValue) continue;

                metricValues.Add(value);
                if (label.IndexOf("Máximo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("Maximo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("Maximum", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    maxCandidate = value;
                }
            }

            if (metricValues.Count == 0) return true;
            if (string.IsNullOrWhiteSpace(maxCandidate))
            {
                maxCandidate = metricValues[metricValues.Count - 1];
            }

            if (maxCandidate.IndexOf("UNBOUNDED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                maxCandidate.IndexOf("ILIMITADO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                maxCandidate.IndexOf("SEM LIMITE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return Regex.IsMatch(maxCandidate, @"^0+(?:[.,]0+)?\s*(?:bytes?|b|kb|mb|gb|tb)?(?:\s*\(\s*0+\s*%\s*\))?$", RegexOptions.IgnoreCase);
        }

        private static void EnsureShadowStorageAllocated(string sysDrive)
        {
            string driveArg = (sysDrive ?? "C:\\").TrimEnd('\\');
            if (!Regex.IsMatch(driveArg, @"^[A-Za-z]:$")) return;

            try
            {
                string vssadminExe = SystemInfo.ResolveSystemExecutable("vssadmin.exe");
                var listRes = SystemInfo.RunProcess(vssadminExe, string.Format("list shadowstorage /for={0}", driveArg), 15000);
                string combined = (listRes.StdOut ?? string.Empty) + "\n" + (listRes.StdErr ?? string.Empty);
                if (!NeedsShadowStorageSetup(combined)) return;

                Logger.Write("INFO", string.Format("Armazenamento de Cópias de Sombra (VSS) não alocado ou zerado em {0}. Reservando 5%...", driveArg));
                var addRes = SystemInfo.RunProcess(vssadminExe, string.Format("add shadowstorage /for={0} /on={0} /maxsize=5%", driveArg), 20000);
                if (addRes.ExitCode == 0)
                {
                    Logger.Write("INFO", string.Format("Espaço de restauração (5%) configurado em {0}.", driveArg));
                    return;
                }

                var resizeRes = SystemInfo.RunProcess(vssadminExe, string.Format("resize shadowstorage /for={0} /on={0} /maxsize=5%", driveArg), 20000);
                if (resizeRes.ExitCode == 0)
                {
                    Logger.Write("INFO", string.Format("Espaço de restauração ajustado para 5% em {0}.", driveArg));
                    return;
                }

                Logger.Write("AVISO", string.Format("vssadmin não conseguiu alocar espaço em {0} (add={1}, resize={2}).", driveArg, addRes.ExitCode, resizeRes.ExitCode));
            }
            catch (Exception ex)
            {
                Logger.Write("AVISO", string.Format("Falha ao verificar/ajustar armazenamento VSS em {0}: {1}", driveArg, ex.Message));
            }
        }

        private static string CollectRestoreDiagnostics(string driveArg)
        {
            var parts = new List<string>();
            try
            {
                string vssadminExe = SystemInfo.ResolveSystemExecutable("vssadmin.exe");
                var listRes = SystemInfo.RunProcess(vssadminExe, string.Format("list shadowstorage /for={0}", driveArg), 10000);
                string combined = (listRes.StdOut ?? string.Empty) + "\n" + (listRes.StdErr ?? string.Empty);
                if (NeedsShadowStorageSetup(combined))
                {
                    parts.Add("armazenamento VSS sem cota disponível em " + driveArg);
                }
            }
            catch
            {
            }

            try
            {
                string queryStr = "*[System[(Provider[@Name='SystemRestore'] or Provider[@Name='VSS'] or Provider[@Name='SPP']) and (Level=1 or Level=2 or Level=3 or EventID=8216) and TimeCreated[timediff(@SystemTime) <= 120000]]]";
                var query = new EventLogQuery("Application", PathType.LogName, queryStr) { ReverseDirection = true };
                using (var reader = new EventLogReader(query))
                {
                    EventRecord ev;
                    int count = 0;
                    while ((ev = reader.ReadEvent()) != null && count < 2)
                    {
                        using (ev)
                        {
                            string desc = string.Empty;
                            try { desc = ev.FormatDescription() ?? string.Empty; } catch { }
                            desc = Regex.Replace(desc, @"\s+", " ").Trim();
                            if (desc.Length > 160) desc = desc.Substring(0, 157) + "...";
                            parts.Add(string.IsNullOrWhiteSpace(desc)
                                ? string.Format("Evento {0} ({1})", ev.Id, ev.ProviderName)
                                : string.Format("Evento {0} ({1}): {2}", ev.Id, ev.ProviderName, desc));
                            count++;
                        }
                    }
                }
            }
            catch
            {
            }

            return string.Join(" | ", parts.ToArray());
        }

        public static string DescribeSystemRestoreReturnCode(uint code)
        {
            switch (code)
            {
                case 0: return "sucesso";
                case 1058: return "serviço necessário desativado (1058)";
                case 1460: return "tempo esgotado (1460)";
                case 112: return "espaço em disco insuficiente (112)";
                case 5: return "acesso negado (5)";
                default: return string.Format("código {0} (0x{0:X8})", code);
            }
        }

        private static void EnsureNotBlockedByPolicy()
        {
            using (RegistryKey hklm = ChangeJournal.OpenBaseRegistryKey(RegistryHive.LocalMachine))
            using (RegistryKey policy = hklm.OpenSubKey(SystemRestorePolicyKey))
            {
                if (policy == null) return;
                object disableSr = policy.GetValue("DisableSR");
                if (disableSr != null && Convert.ToInt32(disableSr) == 1)
                {
                    throw new InvalidOperationException("A Restauração do Sistema está desativada por política de grupo (DisableSR = 1). Fale com o administrador da rede.");
                }
            }
        }

        // Retorna true se o serviço estava desativado e foi liberado temporariamente (para ser desativado de novo ao final).
        private static bool PrepareServiceIfDisabled(string serviceName, string friendlyName, bool required)
        {
            if (!ChangeJournal.ServiceExists(serviceName))
            {
                if (required)
                {
                    throw new InvalidOperationException(string.Format("O serviço {0} não existe neste sistema.", friendlyName));
                }
                return false;
            }

            bool wasDisabled;
            using (var svc = new ServiceController(serviceName))
            {
                wasDisabled = svc.StartType == ServiceStartMode.Disabled;
            }

            if (wasDisabled)
            {
                Logger.Write("INFO", string.Format("Serviço {0} estava desativado; liberando temporariamente (Manual) para criar o ponto.", serviceName));
                ChangeJournal.SetServiceStartupType(serviceName, "Manual");
            }
            return wasDisabled;
        }

        private static void TryRestoreServiceDisabled(string serviceName)
        {
            try
            {
                ChangeJournal.SetServiceStartupType(serviceName, "Disabled");
                Logger.Write("INFO", string.Format("Serviço {0} devolvido ao estado original (Desativado).", serviceName));
            }
            catch (Exception ex)
            {
                Logger.Write("AVISO", string.Format("Não foi possível devolver o serviço {0} ao estado Desativado ({1}).", serviceName, ex.Message));
            }
        }

        private static void EnableSystemProtection(string sysDrive)
        {
            using (var srClass = new ManagementClass("root\\default", "SystemRestore", null))
            using (ManagementBaseObject enableParams = srClass.GetMethodParameters("Enable"))
            {
                enableParams["Drive"] = sysDrive;
                enableParams["WaitTillEnabled"] = true;
                using (ManagementBaseObject enableOut = srClass.InvokeMethod("Enable", enableParams, null))
                {
                    uint enableCode = enableOut != null ? Convert.ToUInt32(enableOut["ReturnValue"]) : 1;
                    if (enableCode != 0)
                    {
                        throw new InvalidOperationException(string.Format("Não foi possível ativar a Proteção do Sistema em {0}: {1}.", sysDrive, DescribeSystemRestoreReturnCode(enableCode)));
                    }
                }
            }
            Logger.Write("INFO", string.Format("Proteção do Sistema ativa em {0}.", sysDrive));
        }

        // Libera temporariamente o limite de frequência (padrão do Windows: 1 ponto a cada 24h via WMI) e restaura o valor original.
        private static void CreateRestorePointWithFrequencyBypass(string description)
        {
            using (RegistryKey hklm = ChangeJournal.OpenBaseRegistryKey(RegistryHive.LocalMachine))
            using (RegistryKey key = hklm.CreateSubKey(SystemRestoreConfigKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Não foi possível abrir a chave de configuração do SystemRestore.");
                }

                string[] names = key.GetValueNames();
                bool existed = Array.Exists(names, delegate(string n) { return string.Equals(n, FrequencyValueName, StringComparison.OrdinalIgnoreCase); });
                object original = existed ? key.GetValue(FrequencyValueName) : null;
                RegistryValueKind originalKind = existed ? key.GetValueKind(FrequencyValueName) : RegistryValueKind.DWord;

                key.SetValue(FrequencyValueName, 0, RegistryValueKind.DWord);
                try
                {
                    using (var srClass = new ManagementClass("root\\default", "SystemRestore", null))
                    using (ManagementBaseObject createParams = srClass.GetMethodParameters("CreateRestorePoint"))
                    {
                        createParams["Description"] = description;
                        createParams["EventType"] = BeginSystemChange;
                        createParams["RestorePointType"] = ModifySettings;
                        using (ManagementBaseObject createOut = srClass.InvokeMethod("CreateRestorePoint", createParams, null))
                        {
                            uint createCode = createOut != null ? Convert.ToUInt32(createOut["ReturnValue"]) : 1;
                            if (createCode != 0)
                            {
                                throw new InvalidOperationException(string.Format("O Windows recusou a criação do Ponto de Restauração: {0}.", DescribeSystemRestoreReturnCode(createCode)));
                            }
                        }
                    }
                }
                finally
                {
                    try
                    {
                        if (existed && original != null)
                        {
                            key.SetValue(FrequencyValueName, original, originalKind);
                        }
                        else
                        {
                            key.DeleteValue(FrequencyValueName, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Write("AVISO", string.Format("Não foi possível restaurar '{0}' ao valor original ({1}).", FrequencyValueName, ex.Message));
                    }
                }
            }
        }

        // O WMI SystemRestore pode levar alguns segundos após CreateRestorePoint retornar 0
        // até expor o snapshot recém-criado na enumeração.
        private static RestorePointInfo WaitForRestorePoint(string description, DateTime startedAt)
        {
            int[] waitDelaysMs = new int[] { 0, 1500, 2000, 2500, 3000 };
            foreach (int delayMs in waitDelaysMs)
            {
                if (delayMs > 0) Thread.Sleep(delayMs);
                RestorePointInfo match = FindRestorePoint(description, startedAt);
                if (match != null) return match;
            }
            return null;
        }

        private static RestorePointInfo FindRestorePoint(string description, DateTime startedAt)
        {
            RestorePointInfo found = null;
            EnumerateRestorePoints(delegate(RestorePointInfo rp)
            {
                if (IsMatchingRestorePoint(rp.Description, rp.CreationTime, description, startedAt))
                {
                    found = rp;
                }
            });
            return found;
        }

        private static RestorePointInfo FindNewestRestorePoint()
        {
            RestorePointInfo newest = null;
            try
            {
                EnumerateRestorePoints(delegate(RestorePointInfo rp)
                {
                    if (newest == null || rp.CreationTime > newest.CreationTime) newest = rp;
                });
            }
            catch (Exception ex)
            {
                Logger.Write("AVISO", string.Format("Não foi possível listar os pontos de restauração existentes ({0}).", ex.Message));
            }
            return newest;
        }

        private static void EnumerateRestorePoints(Action<RestorePointInfo> visitor)
        {
            using (var searcher = new ManagementObjectSearcher("root\\default", "SELECT Description, CreationTime FROM SystemRestore"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementBaseObject obj in results)
                {
                    using (obj)
                    {
                        DateTime created = DateTime.MinValue;
                        string rawTime = obj["CreationTime"] as string;
                        if (!string.IsNullOrWhiteSpace(rawTime))
                        {
                            try { created = ManagementDateTimeConverter.ToDateTime(rawTime); }
                            catch (ArgumentOutOfRangeException) { }
                        }
                        visitor(new RestorePointInfo { Description = obj["Description"] as string, CreationTime = created });
                    }
                }
            }
        }
    }
}
