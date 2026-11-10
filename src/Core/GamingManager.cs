using System;
using Microsoft.Win32;

namespace Otimizador.Core
{
    public static class GamingManager
    {
        private static readonly string[] GameBarProtocols = new string[]
        {
            "ms-gamebar",
            "ms-gamebarservices",
            "ms-gamingoverlay"
        };

        public static void DisableGameDVRBackgroundCapture()
        {
            const string gameConfig = @"HKCU:\System\GameConfigStore";
            ChangeJournal.SetRegistryValue(gameConfig, "GameDVR_Enabled", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(gameConfig, "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(gameConfig, "GameDVR_HonorUserFSEBehaviorMode", 1, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(gameConfig, "GameDVR_DXGIHonorFSEWindowsCompatible", 1, RegistryValueKind.DWord);

            const string gameDvr = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR";
            string[] values = new string[]
            {
                "AppCaptureEnabled", "AudioCaptureEnabled", "CursorCaptureEnabled", "HistoricalCaptureEnabled"
            };
            foreach (string valueName in values)
            {
                ChangeJournal.SetRegistryValue(gameDvr, valueName, 0, RegistryValueKind.DWord);
            }

            ChangeJournal.SetRegistryValue(@"HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(@"HKCU:\SOFTWARE\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord);
        }

        private static string GetGameBarProtocolRoot(string protocol)
        {
            string hkcuPath = string.Format(@"HKCU:\Software\Classes\{0}", protocol);
            if (ChangeJournal.TestRegistryKeyExists(hkcuPath))
            {
                return hkcuPath;
            }
            return string.Format(@"HKLM:\SOFTWARE\Classes\{0}", protocol);
        }

        public static void SetGameBarProtocolNullRoute()
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string systrayPath = System.IO.Path.Combine(winDir, @"System32\systray.exe");

            foreach (string proto in GameBarProtocols)
            {
                string rootKey = GetGameBarProtocolRoot(proto);
                ChangeJournal.SetRegistryValue(rootKey, "(default)", "URL:" + proto, RegistryValueKind.String);
                ChangeJournal.SetRegistryValue(rootKey, "URL Protocol", string.Empty, RegistryValueKind.String);
                ChangeJournal.SetRegistryValue(rootKey, "NoOpenWith", string.Empty, RegistryValueKind.String);
                ChangeJournal.SetRegistryValue(rootKey + @"\shell\open\command", "(default)", systrayPath, RegistryValueKind.String);
            }
        }

        public static void RestoreGameBarProtocols()
        {
            foreach (string proto in GameBarProtocols)
            {
                string[] paths = new string[]
                {
                    string.Format(@"HKLM:\SOFTWARE\Classes\{0}\shell\open\command", proto),
                    string.Format(@"HKCU:\Software\Classes\{0}\shell\open\command", proto)
                };

                foreach (string path in paths)
                {
                    RegistryLocation loc = ChangeJournal.ConvertToRegistryLocation(path);
                    using (RegistryKey baseKey = ChangeJournal.OpenBaseRegistryKey(loc.Hive))
                    using (RegistryKey key = baseKey.OpenSubKey(loc.SubKey))
                    {
                        if (key == null) continue;
                        string target = Convert.ToString(key.GetValue(string.Empty)) ?? string.Empty;
                        if (target.ToLowerInvariant().EndsWith("systray.exe"))
                        {
                            string protoSubKey = loc.SubKey.Substring(0, loc.SubKey.Length - @"\shell\open\command".Length);
                            baseKey.DeleteSubKeyTree(protoSubKey, false);
                        }
                    }
                }
            }
        }
    }
}
