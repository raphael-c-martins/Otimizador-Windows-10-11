using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using Otimizador.Core;
using Otimizador.UI;

namespace Otimizador.Tests
{
    public static class ExecucaoTests
    {
        private const uint LoadLibraryAsDatafile = 0x00000002;
        private static readonly IntPtr RtGroupIcon = new IntPtr(14);

        private delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpType, EnumResNameProc lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private static int CountWin32GroupIcons(string exePath)
        {
            IntPtr hMod = LoadLibraryEx(exePath, IntPtr.Zero, LoadLibraryAsDatafile);
            if (hMod == IntPtr.Zero)
            {
                throw new InvalidOperationException("Não foi possível carregar o executável para ler recursos Win32.");
            }

            try
            {
                int count = 0;
                EnumResourceNames(hMod, RtGroupIcon, delegate(IntPtr mod, IntPtr type, IntPtr name, IntPtr param)
                {
                    count++;
                    return true;
                }, IntPtr.Zero);
                return count;
            }
            finally
            {
                FreeLibrary(hMod);
            }
        }
        public static void Run(TestSuiteRunner runner, string repoRoot)
        {
            string tempDir = TestHelpers.NewTempDirectory("OtimizadorExecucaoTest");

            try
            {
                runner.Describe("Execução assíncrona (modelo usado pela GUI)");

                runner.It("roda a simulação em outra thread e entrega logs, progresso e resultado", delegate
                {
                    OptimizerCatalog catalog = OptimizationEngine.GetCatalog(null);
                    string logFile = Path.Combine(tempDir, "segundo-plano.log");
                    var logEvents = new ConcurrentQueue<string>();
                    var progressCount = 0;
                    object progressLock = new object();

                    Logger.SetTarget(
                        logFile,
                        delegate(string lvl, string msg) { logEvents.Enqueue(lvl + ": " + msg); },
                        delegate(int cur, int tot, string act) { lock (progressLock) { progressCount++; } });

                    try
                    {
                        Task<ExecutionResult> task = Task<ExecutionResult>.Factory.StartNew(delegate
                        {
                            return OptimizationEngine.InvokeOptimizationPlan(
                                catalog,
                                new string[] { "BingNews" },
                                new string[] { "ShowFileExtensions" },
                                new ExecutionContext { StateDirectory = tempDir, RestartExplorer = true },
                                true);
                        }, TaskCreationOptions.LongRunning);

                        Assert.IsTrue(task.Wait(30000), "A tarefa em segundo plano demorou além do esperado.");
                        ExecutionResult result = task.Result;
                        Assert.Equal(3, result.Simulated);
                        Assert.Equal(0, result.Failed);
                        Assert.GreaterThan(logEvents.Count, 0);
                        Assert.GreaterThan(progressCount, 0);
                        Assert.IsTrue(File.Exists(logFile));
                    }
                    finally
                    {
                        Logger.SetTarget(null, null, null);
                    }
                });

                runner.Describe("Janela principal (WPF)");

                runner.It("carrega o XAML embutido com todos os controles nomeados, ícone e aba Privacidade & Segurança", delegate
                {
                    MainWindowHandles ui = MainWindowGUI.NewMainWindow();
                    try
                    {
                        Assert.IsNotNull(ui.Window.Icon, "A janela principal deve carregar o ícone embutido.");
                        Assert.GreaterThan(ui.Controls.Count, 30);
                        var navPrivacy = (Button)ui.Controls["NavBtnPrivacy"];
                        Assert.IsTrue(Convert.ToString(navPrivacy.Content).Contains("Segurança"));
                    }
                    finally
                    {
                        ui.Window.Close();
                    }
                });

                runner.It("mantém Otimizador-Windows.exe com ícone embutido diretamente na raiz do repositório", delegate
                {
                    string rootExe = Path.Combine(repoRoot, "Otimizador-Windows.exe");
                    string iconPath = Path.Combine(repoRoot, "src", "Assets", "Otimizador.ico");
                    Assert.IsTrue(File.Exists(iconPath), "Arquivo .ico fonte deve existir em src/Assets/Otimizador.ico.");
                    Assert.IsTrue(File.Exists(rootExe), "Executável principal deve ficar na raiz do repositório.");
                    Assert.GreaterThan((int)new FileInfo(rootExe).Length, 50000);
                    Assert.GreaterThan(CountWin32GroupIcons(rootExe), 0, "Otimizador-Windows.exe deve conter recurso de ícone Win32 (RT_GROUP_ICON).");
                });
            }
            finally
            {
                TestHelpers.DeleteDirectorySafe(tempDir);
            }
        }
    }
}
