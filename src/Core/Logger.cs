using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Otimizador.Core
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly UTF8Encoding _utf8Bom = new UTF8Encoding(true);

        private static string _logFilePath;
        private static string _accessibleLogFilePath;
        private static Action<string, string> _logSink;
        private static Action<int, int, string> _progressSink;
        private static int _stepWarningCount;

        public static void InitializeSecureDataDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("O caminho da pasta de dados não pode ser vazio.", "path");
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

            ApplySecureAclRecursively(new DirectoryInfo(path), adminSid, systemSid, usersSid);
        }

        private static void ApplySecureAclRecursively(
            DirectoryInfo dirInfo,
            SecurityIdentifier adminSid,
            SecurityIdentifier systemSid,
            SecurityIdentifier usersSid)
        {
            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0) return;

            ApplySecureDirectoryAcl(dirInfo, adminSid, systemSid, usersSid);
            foreach (FileInfo fileInfo in dirInfo.GetFiles())
            {
                if ((fileInfo.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    ApplySecureFileAcl(fileInfo, adminSid, systemSid, usersSid);
                }
            }
            foreach (DirectoryInfo subDir in dirInfo.GetDirectories())
            {
                ApplySecureAclRecursively(subDir, adminSid, systemSid, usersSid);
            }
        }

        private static void ApplySecureDirectoryAcl(
            DirectoryInfo dirInfo,
            SecurityIdentifier adminSid,
            SecurityIdentifier systemSid,
            SecurityIdentifier usersSid)
        {
            var security = new DirectorySecurity();
            security.SetOwner(adminSid);
            security.SetAccessRuleProtection(true, false);

            var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            security.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(usersSid, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));

            dirInfo.SetAccessControl(security);
        }

        private static void ApplySecureFileAcl(
            FileInfo fileInfo,
            SecurityIdentifier adminSid,
            SecurityIdentifier systemSid,
            SecurityIdentifier usersSid)
        {
            var security = new FileSecurity();
            security.SetOwner(adminSid);
            security.SetAccessRuleProtection(true, false);

            security.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(usersSid, FileSystemRights.ReadAndExecute, AccessControlType.Allow));

            fileInfo.SetAccessControl(security);
        }

        public static string ResolveUniqueFilePath(string candidatePath)
        {
            if (!File.Exists(candidatePath)) return candidatePath;
            string dir = Path.GetDirectoryName(candidatePath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(candidatePath);
            string ext = Path.GetExtension(candidatePath);
            int counter = 2;
            string next;
            do
            {
                next = Path.Combine(dir, string.Format("{0} ({1}){2}", baseName, counter, ext));
                counter++;
            }
            while (File.Exists(next));
            return next;
        }

        public static string NewOptimizerLogFile(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string candidate = Path.Combine(directory, string.Format("Otimizacao_{0}_{1}.log", Environment.MachineName, stamp));
            return ResolveUniqueFilePath(candidate);
        }

        public static void SetTarget(string filePath, Action<string, string> sink = null, Action<int, int, string> progressSink = null)
        {
            lock (_lock)
            {
                _logFilePath = filePath;
                _accessibleLogFilePath = null;
                _logSink = sink;
                _progressSink = progressSink;
            }
        }

        public static string SetAccessibleLogTarget(string filePath)
        {
            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    _accessibleLogFilePath = null;
                    return null;
                }

                try
                {
                    string dir = Path.GetDirectoryName(filePath);
                    if (string.IsNullOrWhiteSpace(dir)) return null;
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    if (SystemInfo.IsReparsePoint(dir)) return null;

                    string uniquePath = ResolveUniqueFilePath(filePath);
                    if (SystemInfo.IsReparsePoint(uniquePath)) return null;

                    if (!string.IsNullOrWhiteSpace(_logFilePath) && File.Exists(_logFilePath))
                    {
                        string existingContent = File.ReadAllText(_logFilePath, _utf8Bom);
                        if (!string.IsNullOrEmpty(existingContent))
                        {
                            File.WriteAllText(uniquePath, existingContent, _utf8Bom);
                        }
                    }

                    _accessibleLogFilePath = uniquePath;
                    return uniquePath;
                }
                catch
                {
                    _accessibleLogFilePath = null;
                    return null;
                }
            }
        }

        public static string GetLogFilePath()
        {
            lock (_lock)
            {
                return _logFilePath;
            }
        }

        public static string GetDisplayLogFilePath()
        {
            lock (_lock)
            {
                return !string.IsNullOrWhiteSpace(_accessibleLogFilePath) ? _accessibleLogFilePath : _logFilePath;
            }
        }

        public static void Write(string level, string message)
        {
            string normalizedLevel = string.IsNullOrWhiteSpace(level) ? "INFO" : level.Trim().ToUpperInvariant();
            string logPath;
            string accessiblePath;
            Action<string, string> sink;

            lock (_lock)
            {
                if (normalizedLevel == "AVISO")
                {
                    _stepWarningCount++;
                }
                logPath = _logFilePath;
                accessiblePath = _accessibleLogFilePath;
                sink = _logSink;
            }

            if (!string.IsNullOrWhiteSpace(logPath) || !string.IsNullOrWhiteSpace(accessiblePath))
            {
                string line = string.Format("[{0}] [{1}] {2}{3}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    normalizedLevel,
                    message ?? string.Empty,
                    Environment.NewLine);
                try
                {
                    lock (_lock)
                    {
                        if (!string.IsNullOrWhiteSpace(logPath))
                        {
                            File.AppendAllText(logPath, line, _utf8Bom);
                        }
                        if (!string.IsNullOrWhiteSpace(accessiblePath) &&
                            !string.Equals(accessiblePath, logPath, StringComparison.OrdinalIgnoreCase) &&
                            !SystemInfo.IsReparsePoint(accessiblePath))
                        {
                            File.AppendAllText(accessiblePath, line, _utf8Bom);
                        }
                    }
                }
                catch
                {
                }
            }

            if (sink != null)
            {
                try
                {
                    sink(normalizedLevel, message ?? string.Empty);
                }
                catch
                {
                }
            }
        }

        public static void WriteProgress(int current, int total, string activity)
        {
            Action<int, int, string> progressSink;
            lock (_lock)
            {
                progressSink = _progressSink;
            }
            if (progressSink != null)
            {
                try
                {
                    progressSink(current, total, activity ?? string.Empty);
                }
                catch
                {
                }
            }
        }

        public static void ResetStepWarningCount()
        {
            lock (_lock)
            {
                _stepWarningCount = 0;
            }
        }

        public static int GetStepWarningCount()
        {
            lock (_lock)
            {
                return _stepWarningCount;
            }
        }
    }
}
