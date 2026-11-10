using System;
using System.IO;
using System.Management;
using System.ServiceProcess;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public static class BackupManager
    {
        private const uint BeginSystemChange = 100;
        private const uint ModifySettings = 12;

        public static bool InvokeSystemRestorePoint(string description)
        {
            Logger.Write("INFO", "Verificando a Proteção do Sistema e criando o Ponto de Restauração...");

            // 1. Serviço de Cópias de Sombra de Volume (VSS)
            ChangeJournal.SetServiceStartupType("VSS", "Manual");
            using (var vss = new ServiceController("VSS"))
            {
                if (vss.Status != ServiceControllerStatus.Running)
                {
                    vss.Start();
                    vss.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
            }

            string sysDrive = (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\").TrimEnd('\\') + "\\";

            // 2. Habilita Proteção do Sistema na unidade do sistema
            using (var srClass = new ManagementClass("root\\default", "SystemRestore", null))
            {
                ManagementBaseObject enableParams = srClass.GetMethodParameters("Enable");
                enableParams["Drive"] = sysDrive;
                enableParams["WaitTillEnabled"] = true;
                ManagementBaseObject enableOut = srClass.InvokeMethod("Enable", enableParams, null);
                uint enableCode = enableOut != null ? Convert.ToUInt32(enableOut["ReturnValue"]) : 1;
                if (enableCode != 0)
                {
                    throw new InvalidOperationException(string.Format("Não foi possível ativar a Proteção do Sistema em {0} (código {1}).", sysDrive, enableCode));
                }
            }

            // 3. Libera temporariamente o limite de 24h e restaura o valor original ao final
            const string subKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
            const string valueName = "SystemRestorePointCreationFrequency";

            using (RegistryKey hklm = ChangeJournal.OpenBaseRegistryKey(RegistryHive.LocalMachine))
            using (RegistryKey key = hklm.CreateSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Não foi possível abrir a chave de configuração do SystemRestore.");
                }

                string[] names = key.GetValueNames();
                bool existed = Array.Exists(names, delegate(string n) { return string.Equals(n, valueName, StringComparison.OrdinalIgnoreCase); });
                object original = existed ? key.GetValue(valueName) : null;

                key.SetValue(valueName, 0, RegistryValueKind.DWord);
                try
                {
                    using (var srClass = new ManagementClass("root\\default", "SystemRestore", null))
                    {
                        ManagementBaseObject createParams = srClass.GetMethodParameters("CreateRestorePoint");
                        createParams["Description"] = description;
                        createParams["EventType"] = BeginSystemChange;
                        createParams["RestorePointType"] = ModifySettings;
                        ManagementBaseObject createOut = srClass.InvokeMethod("CreateRestorePoint", createParams, null);
                        uint createCode = createOut != null ? Convert.ToUInt32(createOut["ReturnValue"]) : 1;
                        if (createCode != 0)
                        {
                            throw new InvalidOperationException(string.Format("O Windows recusou a criação do Ponto de Restauração (código {0}).", createCode));
                        }
                    }
                }
                finally
                {
                    if (existed)
                    {
                        key.SetValue(valueName, Convert.ToInt32(original), RegistryValueKind.DWord);
                    }
                    else
                    {
                        key.DeleteValue(valueName, false);
                    }
                }
            }

            // 4. Confirma que o ponto realmente foi registrado
            bool confirmed = false;
            using (var searcher = new ManagementObjectSearcher("root\\default", "SELECT Description FROM SystemRestore"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string foundDesc = obj["Description"] as string;
                    if (string.Equals(foundDesc, description, StringComparison.Ordinal))
                    {
                        confirmed = true;
                        break;
                    }
                }
            }

            if (!confirmed)
            {
                throw new InvalidOperationException("O Windows não registrou o Ponto de Restauração (a Proteção do Sistema pode estar bloqueada por política).");
            }

            Logger.Write("OK", string.Format("Ponto de Restauração criado e confirmado: '{0}'.", description));
            return true;
        }

        public static string NewRestorePointDescription(string label = "Otimizador")
        {
            return string.Format("Otimizador_{0}_{1}", label, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        }
    }
}
