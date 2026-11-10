using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Otimizador.Core
{
    public static class JsonHelper
    {
        public static JavaScriptSerializer CreateSerializer()
        {
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            return serializer;
        }

        public static T Deserialize<T>(string json)
        {
            return CreateSerializer().Deserialize<T>(json);
        }

        public static string Serialize(object value)
        {
            return CreateSerializer().Serialize(value);
        }
    }

    public sealed class AppsDocument
    {
        public string Version { get; set; }
        public List<AppItem> Apps { get; set; }

        public AppsDocument()
        {
            Apps = new List<AppItem>();
        }
    }

    public sealed class AppItem
    {
        public string Id { get; set; }
        public string FriendlyName { get; set; }
        public string PackageName { get; set; }
        public string WinGetId { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public bool SelectedByDefault { get; set; }
        public string Recommendation { get; set; }
        public List<string> RequiresTweaks { get; set; }

        public AppItem()
        {
            RequiresTweaks = new List<string>();
        }
    }

    public sealed class TweaksDocument
    {
        public string Version { get; set; }
        public List<TweakCategory> Categories { get; set; }
        public List<TweakItem> Tweaks { get; set; }

        public TweaksDocument()
        {
            Categories = new List<TweakCategory>();
            Tweaks = new List<TweakItem>();
        }
    }

    public sealed class TweakCategory
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public sealed class TweakItem
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Function { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool SelectedByDefault { get; set; }
    }

    public sealed class OptimizerCatalog
    {
        public string BaseDirectory { get; set; }
        public List<AppItem> Apps { get; set; }
        public List<TweakItem> Tweaks { get; set; }
        public List<TweakCategory> Categories { get; set; }

        public OptimizerCatalog()
        {
            Apps = new List<AppItem>();
            Tweaks = new List<TweakItem>();
            Categories = new List<TweakCategory>();
        }
    }

    public sealed class OptimizerPreset
    {
        public string PresetName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public List<string> RemoveApps { get; set; }
        public List<string> PreserveApps { get; set; }
        public List<string> ApplyTweaks { get; set; }

        public OptimizerPreset()
        {
            RemoveApps = new List<string>();
            PreserveApps = new List<string>();
            ApplyTweaks = new List<string>();
        }
    }

    public sealed class OSInfo
    {
        public string Caption { get; set; }
        public int Build { get; set; }
        public int ProductType { get; set; }
        public bool IsWin11 { get; set; }
        public bool IsServer { get; set; }
    }

    public sealed class UserTargetInfo
    {
        public string Sid { get; set; }
        public string Message { get; set; }
        public bool IsWarning { get; set; }
    }

    public sealed class ExecutionContext
    {
        public bool IsWin11 { get; set; }
        public bool IsServer { get; set; }
        public bool IsPortable { get; set; }
        public List<string> ProtectedPaths { get; set; }
        public string StateDirectory { get; set; }
        public string JournalDirectory { get; set; }
        public string UserSid { get; set; }
        public bool RestartExplorer { get; set; }

        public ExecutionContext()
        {
            ProtectedPaths = new List<string>();
            RestartExplorer = true;
        }
    }

    public sealed class StepResult
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }
        public double Duration { get; set; }
    }

    public sealed class ExecutionResult
    {
        public List<StepResult> Steps { get; private set; }
        public int Ok { get; set; }
        public int Warnings { get; set; }
        public int Failed { get; set; }
        public int Simulated { get; set; }
        public bool DryRun { get; set; }
        public string JournalPath { get; set; }

        public ExecutionResult()
        {
            Steps = new List<StepResult>();
        }
    }

    public sealed class UndoResult
    {
        public string Mode { get; set; }
        public int Files { get; set; }
        public int Restored { get; set; }
        public int Failed { get; set; }
    }

    public sealed class JournalDocument
    {
        public string Computador { get; set; }
        public string CriadoEm { get; set; }
        public List<JournalEntry> Entradas { get; set; }

        public JournalDocument()
        {
            Entradas = new List<JournalEntry>();
        }
    }

    public sealed class JournalEntry
    {
        public string Kind { get; set; }
        public string Hive { get; set; }
        public string SubKey { get; set; }
        public string Name { get; set; }
        public bool Existed { get; set; }
        public string ValueKind { get; set; }
        public object Value { get; set; }
        public object Start { get; set; }
        public object DelayedAutostart { get; set; }
        public string TaskPath { get; set; }
        public string TaskName { get; set; }
        public bool WasEnabled { get; set; }
        public string ActiveScheme { get; set; }
        public object HibernateEnabled { get; set; }
        public string Guid { get; set; }
    }
}
