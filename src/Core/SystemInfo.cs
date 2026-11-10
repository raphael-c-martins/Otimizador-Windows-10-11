using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public sealed class ProcessRunResult
    {
        public int ExitCode { get; set; }
        public bool TimedOut { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }
    }

    public static class SystemInfo
    {
        public static bool TestIsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static OSInfo GetOSInfo()
        {
            string caption = "Windows";
            int build = Environment.OSVersion.Version.Build;
            int productType = 1;

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, BuildNumber, ProductType FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["Caption"] != null) caption = obj["Caption"].ToString().Trim();
                        if (obj["BuildNumber"] != null)
                        {
                            int parsedBuild;
                            if (int.TryParse(obj["BuildNumber"].ToString(), out parsedBuild)) build = parsedBuild;
                        }
                        if (obj["ProductType"] != null)
                        {
                            int parsedType;
                            if (int.TryParse(obj["ProductType"].ToString(), out parsedType)) productType = parsedType;
                        }
                        break;
                    }
                }
            }
            catch
            {
            }

            return new OSInfo
            {
                Caption = caption,
                Build = build,
                ProductType = productType,
                IsWin11 = (build >= 22000),
                IsServer = (productType != 1)
            };
        }

        public static bool TestIsPortableDevice()
        {
            try
            {
                using (var batSearcher = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_Battery"))
                {
                    if (batSearcher.Get().Count > 0) return true;
                }

                var portableChassis = new HashSet<int>(new int[] { 8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32 });
                using (var encSearcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                {
                    foreach (ManagementObject obj in encSearcher.Get())
                    {
                        var types = obj["ChassisTypes"] as Array;
                        if (types == null) continue;
                        foreach (object t in types)
                        {
                            if (portableChassis.Contains(Convert.ToInt32(t))) return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static string GetInteractiveUserSid()
        {
            try
            {
                using (var csSearcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in csSearcher.Get())
                    {
                        var userName = obj["UserName"] as string;
                        if (!string.IsNullOrWhiteSpace(userName))
                        {
                            var account = new NTAccount(userName);
                            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
                            return sid.Value;
                        }
                    }
                }
            }
            catch
            {
            }

            try
            {
                int sessionId = Process.GetCurrentProcess().SessionId;
                string query = string.Format("SELECT Handle FROM Win32_Process WHERE Name='explorer.exe' AND SessionId={0}", sessionId);
                using (var procSearcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject proc in procSearcher.Get())
                    {
                        ManagementBaseObject outParams = proc.InvokeMethod("GetOwnerSid", null, null);
                        if (outParams != null && outParams["Sid"] != null)
                        {
                            string sid = outParams["Sid"].ToString();
                            if (!string.IsNullOrWhiteSpace(sid)) return sid;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static UserTargetInfo ResolveUserRegistrySid()
        {
            string currentSid = null;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                if (identity.User != null) currentSid = identity.User.Value;
            }

            string interactiveSid = GetInteractiveUserSid();
            if (string.IsNullOrWhiteSpace(interactiveSid))
            {
                return new UserTargetInfo
                {
                    Sid = null,
                    Message = "Usuário conectado não identificado: ajustes de usuário serão aplicados ao perfil que executa o programa.",
                    IsWarning = true
                };
            }

            if (string.Equals(interactiveSid, currentSid, StringComparison.OrdinalIgnoreCase))
            {
                return new UserTargetInfo
                {
                    Sid = null,
                    Message = "Ajustes de usuário aplicados ao perfil atual.",
                    IsWarning = false
                };
            }

            using (var usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64))
            using (var subKey = usersKey.OpenSubKey(interactiveSid))
            {
                if (subKey != null)
                {
                    return new UserTargetInfo
                    {
                        Sid = interactiveSid,
                        Message = string.Format("Programa elevado com outra conta: ajustes de usuário serão aplicados ao usuário conectado ({0}).", interactiveSid),
                        IsWarning = false
                    };
                }
            }

            return new UserTargetInfo
            {
                Sid = null,
                Message = string.Format("Perfil do usuário conectado ({0}) não está carregado: ajustes de usuário irão para o perfil que executa o programa.", interactiveSid),
                IsWarning = true
            };
        }

        public static ProcessRunResult RunProcess(
            string fileName,
            string arguments,
            int timeoutMs = 120000,
            bool hidden = true,
            bool captureOutput = true,
            Encoding outputEncoding = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = hidden,
                WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureOutput
            };

            if (captureOutput && outputEncoding != null)
            {
                psi.StandardOutputEncoding = outputEncoding;
                psi.StandardErrorEncoding = outputEncoding;
            }

            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            using (var process = new Process { StartInfo = psi })
            {
                if (captureOutput)
                {
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (stdOut) { stdOut.AppendLine(e.Data); }
                    };
                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (stdErr) { stdErr.AppendLine(e.Data); }
                    };
                }

                if (!process.Start())
                {
                    throw new InvalidOperationException(string.Format("Não foi possível iniciar o processo '{0}'.", fileName));
                }

                if (captureOutput)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                bool exited = process.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    if (captureOutput)
                    {
                        try { process.CancelOutputRead(); } catch { }
                        try { process.CancelErrorRead(); } catch { }
                    }

                    string outTimeout;
                    string errTimeout;
                    lock (stdOut) { outTimeout = stdOut.ToString(); }
                    lock (stdErr) { errTimeout = stdErr.ToString(); }

                    return new ProcessRunResult
                    {
                        ExitCode = -1,
                        TimedOut = true,
                        StdOut = outTimeout,
                        StdErr = errTimeout
                    };
                }

                if (captureOutput)
                {
                    process.WaitForExit();
                }

                string outText;
                string errText;
                lock (stdOut) { outText = stdOut.ToString(); }
                lock (stdErr) { errText = stdErr.ToString(); }

                return new ProcessRunResult
                {
                    ExitCode = process.ExitCode,
                    TimedOut = false,
                    StdOut = outText,
                    StdErr = errText
                };
            }
        }

        public static string FindExecutableInPath(string exeName)
        {
            if (string.IsNullOrWhiteSpace(exeName)) return null;
            if (Path.IsPathRooted(exeName))
            {
                return File.Exists(exeName) ? exeName : null;
            }

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                }
            }
            return null;
        }
    }
}
