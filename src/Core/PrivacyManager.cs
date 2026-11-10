using Microsoft.Win32;

namespace Otimizador.Core
{
    public static class PrivacyManager
    {
        public static void DisableSystemTelemetry()
        {
            ChangeJournal.SetServiceStartupTracked("DiagTrack", "Disabled", true);
            ChangeJournal.SetServiceStartupTracked("dmwappushservice", "Disabled", true);

            const string dataCollection = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection";
            ChangeJournal.SetRegistryValue(dataCollection, "AllowTelemetry", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(dataCollection, "MaxTelemetryAllowed", 0, RegistryValueKind.DWord);

            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(@"HKCU:\SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, RegistryValueKind.DWord);

            string[] tasks = new string[]
            {
                @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                @"\Microsoft\Windows\Autochk\Proxy",
                @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"
            };
            foreach (string task in tasks)
            {
                ChangeJournal.DisableScheduledTaskTracked(task);
            }
        }

        public static void DisableActivityHistory()
        {
            const string systemPolicy = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\System";
            ChangeJournal.SetRegistryValue(systemPolicy, "EnableActivityFeed", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(systemPolicy, "PublishUserActivities", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(systemPolicy, "UploadUserActivities", 0, RegistryValueKind.DWord);
        }

        public static void DisableWindowsSuggestionsAndAds()
        {
            const string cdm = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            string[] cdmValues = new string[]
            {
                "SubscribedContent-310093Enabled", "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled",
                "SubscribedContent-338393Enabled", "SubscribedContent-353694Enabled", "SubscribedContent-353696Enabled",
                "SubscribedContent-353698Enabled", "SystemPaneSuggestionsEnabled", "SoftLandingEnabled", "SilentInstalledAppsEnabled"
            };
            foreach (string valueName in cdmValues)
            {
                ChangeJournal.SetRegistryValue(cdm, valueName, 0, RegistryValueKind.DWord);
            }

            const string expAdv = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            string[] expValues = new string[]
            {
                "Start_IrisRecommendations", "ShowSyncProviderNotifications", "Start_AccountNotifications", "Start_TrackProgs"
            };
            foreach (string valueName in expValues)
            {
                ChangeJournal.SetRegistryValue(expAdv, valueName, 0, RegistryValueKind.DWord);
            }
        }

        public static void DisableStartMenuWebSearch()
        {
            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
        }

        public static void DisableWindowsRecallAndAI(bool isWin11)
        {
            if (!isWin11)
            {
                Logger.Write("INFO", "Recall e Click-to-Do existem apenas no Windows 11 (nada a fazer).");
                return;
            }

            const string ai = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
            ChangeJournal.SetRegistryValue(ai, "DisableAIDataAnalysis", 1, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(ai, "TurnOffWindowsRecall", 1, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(ai, "DisableClickToDo", 1, RegistryValueKind.DWord);

            ChangeJournal.SetServiceStartupTracked("WSAIFabricSvc", "Disabled", true);
        }

        public static void DisableWindowsCopilot()
        {
            ChangeJournal.SetRegistryValue(@"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
            ChangeJournal.SetRegistryValue(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0, RegistryValueKind.DWord);
        }

        public static void DisableEdgeBackgroundAndTelemetry()
        {
            const string edge = @"HKLM:\SOFTWARE\Policies\Microsoft\Edge";
            string[] edgePolicies = new string[]
            {
                "HubsSidebarEnabled", "SpotlightExperiencesAndRecommendationsEnabled", "StartupBoostEnabled",
                "BackgroundModeEnabled", "PersonalizationReportingEnabled", "DiagnosticData", "ComposeInlineEnabled"
            };
            foreach (string policy in edgePolicies)
            {
                ChangeJournal.SetRegistryValue(edge, policy, 0, RegistryValueKind.DWord);
            }
        }
    }
}
