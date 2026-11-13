using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public sealed class RegistryLocation
    {
        public RegistryHive Hive { get; set; }
        public string HiveName { get; set; }
        public string SubKey { get; set; }
    }

    public static class ChangeJournal
    {
        private static readonly UTF8Encoding _utf8Bom = new UTF8Encoding(true);
        private static readonly Regex _regPathRegex = new Regex(@"^(HKLM|HKCU|HKU):\\(.+)$", RegexOptions.IgnoreCase);
        private static readonly Regex _guidRegex = new Regex(@"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");

        private static List<JournalEntry> _entries;
        private static string _journalPath;
        private static HashSet<string> _keys;
        private static string _userRegistrySid;

        public static string ResolveUniqueJournalPath(string directory, string stamp)
        {
            string candidate = Path.Combine(directory, string.Format("estado_{0}.json", stamp));
            int counter = 2;
            while (File.Exists(candidate))
            {
                // Usa sufixo _NNN ('_' > '.' em ASCII) para manter ordenação cronológica decrescente no Undo
                candidate = Path.Combine(directory, string.Format("estado_{0}_{1:D3}.json", stamp, counter));
                counter++;
            }
            return candidate;
        }

        public static string Start(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            _journalPath = ResolveUniqueJournalPath(directory, stamp);
            _entries = new List<JournalEntry>();
            _keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return _journalPath;
        }

        public static void Stop()
        {
            Save();
            _entries = null;
            _keys = null;
            _journalPath = null;
        }

        public static void Save()
        {
            if (_entries == null || _entries.Count == 0 || string.IsNullOrWhiteSpace(_journalPath))
            {
                return;
            }
            var doc = new JournalDocument
            {
                Computador = Environment.MachineName,
                CriadoEm = DateTime.Now.ToString("s"),
                Entradas = new List<JournalEntry>(_entries)
            };
            string json = JsonHelper.Serialize(doc);
            File.WriteAllText(_journalPath, json, _utf8Bom);
        }

        public static void AddEntry(string key, JournalEntry entry)
        {
            if (_entries == null || _keys == null) return;
            if (_keys.Add(key))
            {
                _entries.Add(entry);
            }
        }

        public static void SetUserRegistryTarget(string sid)
        {
            _userRegistrySid = string.IsNullOrWhiteSpace(sid) ? null : sid;
        }

        public static string GetUserRegistryTarget()
        {
            return _userRegistrySid;
        }

        public static RegistryLocation ConvertToRegistryLocation(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(string.Format("Caminho de registro não suportado: '{0}'.", path));
            }
            Match match = _regPathRegex.Match(path);
            if (!match.Success)
            {
                throw new ArgumentException(string.Format("Caminho de registro não suportado: '{0}'.", path));
            }

            string root = match.Groups[1].Value.ToUpperInvariant();
            string subKey = match.Groups[2].Value.TrimEnd('\\');
            RegistryHive hive;
            string hiveName;

            switch (root)
            {
                case "HKLM":
                    hive = RegistryHive.LocalMachine;
                    hiveName = "LocalMachine";
                    break;
                case "HKU":
                    hive = RegistryHive.Users;
                    hiveName = "Users";
                    break;
                case "HKCU":
                    if (string.IsNullOrWhiteSpace(_userRegistrySid))
                    {
                        hive = RegistryHive.CurrentUser;
                        hiveName = "CurrentUser";
                    }
                    else
                    {
                        hive = RegistryHive.Users;
                        hiveName = "Users";
                        subKey = string.Format("{0}\\{1}", _userRegistrySid, subKey);
                    }
                    break;
                default:
                    throw new ArgumentException(string.Format("Caminho de registro não suportado: '{0}'.", path));
            }

            return new RegistryLocation
            {
                Hive = hive,
                HiveName = hiveName,
                SubKey = subKey
            };
        }

        public static RegistryKey OpenBaseRegistryKey(string hiveName)
        {
            var hive = (RegistryHive)Enum.Parse(typeof(RegistryHive), hiveName, true);
            return RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        }

        public static RegistryKey OpenBaseRegistryKey(RegistryHive hive)
        {
            return RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        }

        public static bool TestRegistryKeyExists(string path)
        {
            RegistryLocation loc = ConvertToRegistryLocation(path);
            using (RegistryKey baseKey = OpenBaseRegistryKey(loc.Hive))
            using (RegistryKey key = baseKey.OpenSubKey(loc.SubKey))
            {
                return key != null;
            }
        }

        private static void RegisterMissingRegistryKey(RegistryKey baseKey, RegistryLocation loc)
        {
            string[] parts = loc.SubKey.Split('\\');
            for (int i = 1; i <= parts.Length; i++)
            {
                string partial = string.Join("\\", parts, 0, i);
                using (RegistryKey probe = baseKey.OpenSubKey(partial))
                {
                    if (probe == null)
                    {
                        AddEntry(
                            string.Format("K|{0}|{1}", loc.HiveName, partial),
                            new JournalEntry
                            {
                                Kind = "RegistryKey",
                                Hive = loc.HiveName,
                                SubKey = partial
                            });
                        return;
                    }
                }
            }
        }

        private static void RegisterRegistryValueSnapshot(RegistryKey key, RegistryLocation loc, string valueName)
        {
            string[] existingNames = key.GetValueNames();
            bool existed = Array.Exists(existingNames, delegate(string n) { return string.Equals(n, valueName, StringComparison.OrdinalIgnoreCase); });

            var entry = new JournalEntry
            {
                Kind = "RegistryValue",
                Hive = loc.HiveName,
                SubKey = loc.SubKey,
                Name = valueName,
                Existed = existed
            };

            if (existed)
            {
                RegistryValueKind kind = key.GetValueKind(valueName);
                object raw = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                entry.ValueKind = kind.ToString();
                if (kind == RegistryValueKind.Binary && raw is byte[])
                {
                    entry.Value = Convert.ToBase64String((byte[])raw);
                }
                else
                {
                    entry.Value = raw;
                }
            }

            AddEntry(string.Format("V|{0}|{1}|{2}", loc.HiveName, loc.SubKey, valueName), entry);
        }

        public static void SetRegistryValue(
            string path,
            string name,
            object value,
            RegistryValueKind type = RegistryValueKind.DWord)
        {
            RegistryLocation loc = ConvertToRegistryLocation(path);
            string valueName = string.Equals(name, "(default)", StringComparison.OrdinalIgnoreCase) ? string.Empty : (name ?? string.Empty);

            using (RegistryKey baseKey = OpenBaseRegistryKey(loc.Hive))
            {
                RegisterMissingRegistryKey(baseKey, loc);
                using (RegistryKey key = baseKey.CreateSubKey(loc.SubKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException(string.Format("Não foi possível abrir ou criar a chave '{0}'.", path));
                    }

                    RegisterRegistryValueSnapshot(key, loc, valueName);
                    if (type == RegistryValueKind.DWord)
                    {
                        key.SetValue(valueName, Convert.ToInt32(value), RegistryValueKind.DWord);
                    }
                    else if (type == RegistryValueKind.QWord)
                    {
                        key.SetValue(valueName, Convert.ToInt64(value), RegistryValueKind.QWord);
                    }
                    else if (type == RegistryValueKind.Binary)
                    {
                        byte[] bytes = value as byte[];
                        if (bytes == null && value is string)
                        {
                            bytes = Convert.FromBase64String((string)value);
                        }
                        key.SetValue(valueName, bytes ?? new byte[0], RegistryValueKind.Binary);
                    }
                    else if (type == RegistryValueKind.MultiString)
                    {
                        string[] arr = value as string[];
                        if (arr == null)
                        {
                            arr = value == null ? new string[0] : new string[] { Convert.ToString(value) ?? string.Empty };
                        }
                        key.SetValue(valueName, arr, RegistryValueKind.MultiString);
                    }
                    else
                    {
                        key.SetValue(valueName, Convert.ToString(value) ?? string.Empty, type);
                    }
                }
            }
        }

        public static object ConvertFromJournalRegistryValue(JournalEntry entry)
        {
            var kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), entry.ValueKind, true);
            switch (kind)
            {
                case RegistryValueKind.Binary:
                    return Convert.FromBase64String(Convert.ToString(entry.Value) ?? string.Empty);
                case RegistryValueKind.DWord:
                    return Convert.ToInt32(entry.Value);
                case RegistryValueKind.QWord:
                    return Convert.ToInt64(entry.Value);
                case RegistryValueKind.MultiString:
                    if (entry.Value == null) return new string[0];
                    if (entry.Value is string[]) return (string[])entry.Value;
                    var list = new List<string>();
                    var enumerable = entry.Value as IEnumerable;
                    if (enumerable != null && !(entry.Value is string))
                    {
                        foreach (object item in enumerable)
                        {
                            list.Add(Convert.ToString(item) ?? string.Empty);
                        }
                        return list.ToArray();
                    }
                    return new string[] { Convert.ToString(entry.Value) ?? string.Empty };
                default:
                    return Convert.ToString(entry.Value) ?? string.Empty;
            }
        }

        public static void SetServiceStartupType(string serviceName, string startupType)
        {
            try
            {
                string wmiPath = string.Format("Win32_Service.Name='{0}'", serviceName.Replace("'", "''"));
                using (var svcObj = new ManagementObject(wmiPath))
                {
                    svcObj.Get();
                    using (ManagementBaseObject inParams = svcObj.GetMethodParameters("ChangeStartMode"))
                    {
                        inParams["StartMode"] = startupType;
                        using (ManagementBaseObject outParams = svcObj.InvokeMethod("ChangeStartMode", inParams, null))
                        {
                            uint code = outParams != null ? Convert.ToUInt32(outParams["ReturnValue"]) : 1;
                            if (code == 0) return;
                        }
                    }
                }
            }
            catch
            {
            }

            string scMode;
            if (string.Equals(startupType, "Disabled", StringComparison.OrdinalIgnoreCase)) scMode = "disabled";
            else if (string.Equals(startupType, "Manual", StringComparison.OrdinalIgnoreCase)) scMode = "demand";
            else scMode = "auto";

            var res = SystemInfo.RunProcess("sc.exe", string.Format("config \"{0}\" start= {1}", serviceName, scMode), 15000);
            if (res.ExitCode != 0)
            {
                throw new InvalidOperationException(string.Format("Não foi possível alterar o tipo de inicialização do serviço '{0}' (código {1}).", serviceName, res.ExitCode));
            }
        }

        public static bool ServiceExists(string serviceName)
        {
            foreach (ServiceController sc in ServiceController.GetServices())
            {
                using (sc)
                {
                    if (string.Equals(sc.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static void SetServiceStartupTracked(string name, string startupType = "Disabled", bool stop = false)
        {
            if (!ServiceExists(name))
            {
                Logger.Write("INFO", string.Format("Serviço '{0}' não existe neste sistema (nada a fazer).", name));
                return;
            }

            using (RegistryKey baseKey = OpenBaseRegistryKey(RegistryHive.LocalMachine))
            using (RegistryKey key = baseKey.OpenSubKey(string.Format("SYSTEM\\CurrentControlSet\\Services\\{0}", name)))
            {
                if (key != null)
                {
                    AddEntry(string.Format("S|{0}", name), new JournalEntry
                    {
                        Kind = "Service",
                        Name = name,
                        Start = key.GetValue("Start"),
                        DelayedAutostart = key.GetValue("DelayedAutostart")
                    });
                }
            }

            SetServiceStartupType(name, startupType);

            if (stop)
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        try
                        {
                            SecurityManager.StopServiceWithDependents(name);
                        }
                        catch (Exception ex)
                        {
                            Logger.Write("AVISO", string.Format("Serviço '{0}' desativado, mas não pôde ser parado agora ({1}). Ficará parado após reiniciar.", name, ex.Message));
                        }
                    }
                }
            }
        }

        public static void RegisterServiceRestartForUndo(string serviceName)
        {
            AddEntry(string.Format("SR|{0}", serviceName), new JournalEntry
            {
                Kind = "ServiceRestart",
                Name = serviceName
            });
        }

        private static string NormalizeComFolderPath(string taskPath)
        {
            if (string.IsNullOrWhiteSpace(taskPath) || taskPath == "\\") return "\\";
            return taskPath.TrimEnd('\\');
        }

        private static void ReleaseComObject(object comObj)
        {
            if (comObj != null && Marshal.IsComObject(comObj))
            {
                try { Marshal.FinalReleaseComObject(comObj); } catch { }
            }
        }

        private static bool IsTaskMissingException(Exception ex)
        {
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException) return true;
            var comEx = ex as COMException;
            if (comEx != null)
            {
                uint hr = unchecked((uint)comEx.ErrorCode);
                return hr == 0x80070002 || hr == 0x80070003;
            }
            return false;
        }

        public static void DisableScheduledTaskTracked(string fullName)
        {
            int lastSlash = fullName.LastIndexOf('\\');
            string taskPath = fullName.Substring(0, lastSlash + 1);
            string taskName = fullName.Substring(lastSlash + 1);

            dynamic scheduler = null;
            dynamic folder = null;
            dynamic task = null;
            try
            {
                try
                {
                    Type schedType = Type.GetTypeFromProgID("Schedule.Service");
                    scheduler = Activator.CreateInstance(schedType);
                    scheduler.Connect();
                    folder = scheduler.GetFolder(NormalizeComFolderPath(taskPath));
                    task = folder.GetTask(taskName);
                }
                catch (Exception ex)
                {
                    if (IsTaskMissingException(ex))
                    {
                        Logger.Write("INFO", string.Format("Tarefa '{0}' não existe neste sistema (nada a fazer).", fullName));
                    }
                    else
                    {
                        Logger.Write("AVISO", string.Format("Não foi possível consultar a tarefa '{0}' ({1}).", fullName, ex.Message));
                    }
                    return;
                }

                if (task == null)
                {
                    Logger.Write("INFO", string.Format("Tarefa '{0}' não existe neste sistema (nada a fazer).", fullName));
                    return;
                }

                bool wasEnabled = false;
                try { wasEnabled = (bool)task.Enabled; } catch { }

                AddEntry(string.Format("T|{0}", fullName), new JournalEntry
                {
                    Kind = "Task",
                    TaskPath = taskPath,
                    TaskName = taskName,
                    WasEnabled = wasEnabled
                });

                try
                {
                    task.Enabled = false;
                }
                catch (Exception ex)
                {
                    Logger.Write("AVISO", string.Format("Tarefa '{0}' não pôde ser desativada ({1}).", fullName, ex.Message));
                }
            }
            finally
            {
                ReleaseComObject(task);
                ReleaseComObject(folder);
                ReleaseComObject(scheduler);
            }
        }

        public static string GetActivePowerSchemeGuid()
        {
            var res = SystemInfo.RunProcess("powercfg.exe", "/getactivescheme", 15000);
            Match m = _guidRegex.Match(res.StdOut ?? string.Empty);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static void RegisterPowerStateSnapshot()
        {
            object hibernate = null;
            using (RegistryKey baseKey = OpenBaseRegistryKey(RegistryHive.LocalMachine))
            using (RegistryKey key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power"))
            {
                if (key != null)
                {
                    hibernate = key.GetValue("HibernateEnabled");
                }
            }

            AddEntry("P|Estado", new JournalEntry
            {
                Kind = "Power",
                ActiveScheme = GetActivePowerSchemeGuid(),
                HibernateEnabled = hibernate
            });
        }

        public static void RegisterPowerSchemeCreated(string guid)
        {
            AddEntry(string.Format("P|Criado|{0}", guid), new JournalEntry
            {
                Kind = "PowerSchemeCreated",
                Guid = guid
            });
        }

        public static void RestoreEntry(JournalEntry entry)
        {
            switch (entry.Kind)
            {
                case "RegistryValue":
                    using (RegistryKey baseKey = OpenBaseRegistryKey(entry.Hive))
                    {
                        RegistryKey key = baseKey.OpenSubKey(entry.SubKey, true);
                        if (key == null)
                        {
                            if (!entry.Existed) return;
                            key = baseKey.CreateSubKey(entry.SubKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
                        }
                        using (key)
                        {
                            string valueName = entry.Name ?? string.Empty;
                            if (entry.Existed)
                            {
                                object value = ConvertFromJournalRegistryValue(entry);
                                var kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), entry.ValueKind, true);
                                key.SetValue(valueName, value, kind);
                            }
                            else
                            {
                                string[] names = key.GetValueNames();
                                if (Array.Exists(names, delegate(string n) { return string.Equals(n, valueName, StringComparison.OrdinalIgnoreCase); }))
                                {
                                    key.DeleteValue(valueName, false);
                                }
                            }
                        }
                    }
                    break;

                case "RegistryKey":
                    using (RegistryKey baseKey = OpenBaseRegistryKey(entry.Hive))
                    {
                        baseKey.DeleteSubKeyTree(entry.SubKey, false);
                    }
                    break;

                case "Service":
                    if (entry.Start == null) return;
                    int startVal = Convert.ToInt32(entry.Start);
                    string mode;
                    if (startVal == 2) mode = "Automatic";
                    else if (startVal == 3) mode = "Manual";
                    else if (startVal == 4) mode = "Disabled";
                    else return;

                    SetServiceStartupType(entry.Name, mode);
                    if (entry.DelayedAutostart != null)
                    {
                        using (RegistryKey baseKey = OpenBaseRegistryKey(RegistryHive.LocalMachine))
                        using (RegistryKey key = baseKey.OpenSubKey(string.Format("SYSTEM\\CurrentControlSet\\Services\\{0}", entry.Name), true))
                        {
                            if (key != null)
                            {
                                key.SetValue("DelayedAutostart", Convert.ToInt32(entry.DelayedAutostart), RegistryValueKind.DWord);
                            }
                        }
                    }
                    if (startVal == 2)
                    {
                        try
                        {
                            using (var sc = new ServiceController(entry.Name))
                            {
                                if (sc.Status != ServiceControllerStatus.Running)
                                {
                                    sc.Start();
                                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Write("AVISO", string.Format("Serviço '{0}' teve o tipo de inicialização restaurado, mas não iniciou automaticamente ({1}).", entry.Name, ex.Message));
                        }
                    }
                    break;

                case "ServiceRestart":
                    SecurityManager.RestartServiceIfRunning(entry.Name);
                    break;

                case "Task":
                    if (entry.WasEnabled)
                    {
                        dynamic scheduler = null;
                        dynamic folder = null;
                        dynamic task = null;
                        try
                        {
                            Type schedType = Type.GetTypeFromProgID("Schedule.Service");
                            scheduler = Activator.CreateInstance(schedType);
                            scheduler.Connect();
                            folder = scheduler.GetFolder(NormalizeComFolderPath(entry.TaskPath));
                            task = folder.GetTask(entry.TaskName);
                            task.Enabled = true;
                        }
                        finally
                        {
                            ReleaseComObject(task);
                            ReleaseComObject(folder);
                            ReleaseComObject(scheduler);
                        }
                    }
                    break;

                case "Power":
                    if (!string.IsNullOrWhiteSpace(entry.ActiveScheme))
                    {
                        var res = SystemInfo.RunProcess("powercfg.exe", string.Format("/setactive {0}", entry.ActiveScheme), 15000);
                        if (res.ExitCode != 0)
                        {
                            throw new InvalidOperationException(string.Format("powercfg não conseguiu reativar o plano {0}.", entry.ActiveScheme));
                        }
                    }
                    if (entry.HibernateEnabled != null && Convert.ToInt32(entry.HibernateEnabled) == 1)
                    {
                        SystemInfo.RunProcess("powercfg.exe", "/hibernate on", 15000);
                    }
                    break;

                case "PowerSchemeCreated":
                    SystemInfo.RunProcess("powercfg.exe", string.Format("/delete {0}", entry.Guid), 15000);
                    break;

                default:
                    throw new InvalidOperationException(string.Format("Tipo de entrada desconhecido no diário: '{0}'.", entry.Kind));
            }
        }

        public static FileInfo[] GetPendingJournals(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return new FileInfo[0];
            }
            var dir = new DirectoryInfo(directory);
            var files = new List<FileInfo>();
            foreach (FileInfo file in dir.GetFiles("estado_*.json"))
            {
                if (!file.Name.EndsWith(".restaurado.json", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(file);
                }
            }
            files.Sort(delegate(FileInfo a, FileInfo b) { return string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase); });
            return files.ToArray();
        }

        public static UndoResult RestoreJournalFile(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            var doc = JsonHelper.Deserialize<JournalDocument>(json);
            var entries = doc != null && doc.Entradas != null ? new List<JournalEntry>(doc.Entradas) : new List<JournalEntry>();
            entries.Reverse();

            int restored = 0;
            int failed = 0;
            foreach (JournalEntry entry in entries)
            {
                try
                {
                    RestoreEntry(entry);
                    restored++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Logger.Write("ERRO", string.Format("Falha ao reverter {0} [{1}{2}{3}]: {4}",
                        entry.Kind, entry.SubKey, entry.Name, entry.TaskName, ex.Message));
                }
            }

            return new UndoResult { Restored = restored, Failed = failed };
        }
    }
}
