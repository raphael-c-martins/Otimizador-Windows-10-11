using System;
using System.Diagnostics;
using System.IO;
using Otimizador.Core;

namespace Otimizador.Tests
{
    public static class AppRemovalTests
    {
        public static void Run(TestSuiteRunner runner)
        {
            runner.Describe("Remoção de aplicativos (Appx/DISM, OneDrive e WinGet)");

            runner.It("extrai PackageFamilyName corretamente a partir de PackageFullName de 5 segmentos", delegate
            {
                Assert.Equal("Microsoft.Windows.DevHome_8wekyb3d8bbwe",
                    AppManager.TryExtractPackageFamilyName("Microsoft.Windows.DevHome_0.2101.858.0_neutral_~_8wekyb3d8bbwe"));
                Assert.Equal("Microsoft.OutlookForWindows_8wekyb3d8bbwe",
                    AppManager.TryExtractPackageFamilyName("Microsoft.OutlookForWindows_1.2026.818.0_x64__8wekyb3d8bbwe"));
                Assert.IsNull(AppManager.TryExtractPackageFamilyName(null));
                Assert.IsNull(AppManager.TryExtractPackageFamilyName("Microsoft.People"));
                Assert.IsNull(AppManager.TryExtractPackageFamilyName("a_b_c_d"));
            });

            runner.It("classifica DISM código 2 e 3 como AVISO apenas quando não há falha na cópia instalada", delegate
            {
                Assert.IsTrue(AppManager.IsDismMissingPackageExitCode(2));
                Assert.IsTrue(AppManager.IsDismMissingPackageExitCode(3));
                Assert.IsFalse(AppManager.IsDismMissingPackageExitCode(0));
                Assert.IsFalse(AppManager.IsDismMissingPackageExitCode(5));

                Assert.Equal("AVISO", AppManager.ClassifyProvisionedDismOutcome(2, false));
                Assert.Equal("AVISO", AppManager.ClassifyProvisionedDismOutcome(3, false));
                Assert.Equal("ERRO", AppManager.ClassifyProvisionedDismOutcome(2, true));
                Assert.Equal("ERRO", AppManager.ClassifyProvisionedDismOutcome(3, true));
                Assert.Equal("ERRO", AppManager.ClassifyProvisionedDismOutcome(5, false));
                Assert.Equal("ERRO", AppManager.ClassifyProvisionedDismOutcome(87, false));
            });

            runner.It("analisa comandos de desinstalação com e sem aspas no registro", delegate
            {
                string exe;
                string args;

                Assert.IsTrue(AppManager.TryParseUninstallCommand(
                    "\"C:\\Program Files\\Microsoft OneDrive\\25.087.0506.0001\\OneDriveSetup.exe\" /uninstall /allusers",
                    out exe, out args));
                Assert.Equal(@"C:\Program Files\Microsoft OneDrive\25.087.0506.0001\OneDriveSetup.exe", exe);
                Assert.Equal("/uninstall /allusers", args);

                Assert.IsTrue(AppManager.TryParseUninstallCommand(
                    @"C:\Windows\System32\OneDriveSetup.exe /uninstall",
                    out exe, out args));
                Assert.Equal(@"C:\Windows\System32\OneDriveSetup.exe", exe);
                Assert.Equal("/uninstall", args);

                Assert.IsTrue(AppManager.TryParseUninstallCommand(
                    "\"C:\\Edge\\setup.exe\"",
                    out exe, out args));
                Assert.Equal(@"C:\Edge\setup.exe", exe);
                Assert.Equal(string.Empty, args);

                Assert.IsFalse(AppManager.TryParseUninstallCommand("   ", out exe, out args));
                Assert.IsFalse(AppManager.TryParseUninstallCommand(null, out exe, out args));
            });

            runner.It("garante argumentos /uninstall /allusers para OneDrive por máquina sem duplicar flags", delegate
            {
                Assert.Equal("/uninstall /allusers", OneDriveManager.EnsureAllUsersArgs(null));
                Assert.Equal("/uninstall /allusers", OneDriveManager.EnsureAllUsersArgs("/uninstall"));
                Assert.Equal("/uninstall /silent /allusers", OneDriveManager.EnsureAllUsersArgs("/uninstall /silent"));
                Assert.Equal("/UNINSTALL /allusers /silent", OneDriveManager.EnsureAllUsersArgs("/UNINSTALL /allusers /silent"));
            });

            runner.It("reconhece apenas valores exatos do OneDrive na chave Run", delegate
            {
                Assert.IsTrue(OneDriveManager.IsOneDriveRunValueName("OneDrive"));
                Assert.IsTrue(OneDriveManager.IsOneDriveRunValueName("onedrivesetup"));
                Assert.IsFalse(OneDriveManager.IsOneDriveRunValueName("OneDriveBackup"));
                Assert.IsFalse(OneDriveManager.IsOneDriveRunValueName("GoogleDriveFS"));
                Assert.IsFalse(OneDriveManager.IsOneDriveRunValueName(null));
            });

            runner.It("extrai linha diagnóstica útil do WinGet ignorando barras de progresso e ANSI", delegate
            {
                string noisyOut = "\u001b[?25l   - \r\n   \\ \r\nFalha ao executar o comando de desinstalação.\r\n";
                Assert.Equal("Falha ao executar o comando de desinstalação.", AppManager.ExtractLastDiagnosticLine(string.Empty, noisyOut));
                Assert.Equal("Erro específico no stderr", AppManager.ExtractLastDiagnosticLine("Erro específico no stderr\r\n", noisyOut));
                Assert.Equal(string.Empty, AppManager.ExtractLastDiagnosticLine("   \r\n - \r\n \\ \r\n", ""));
            });

            runner.It("remove diretório de cliente OneDrive com segurança preservando pasta pessoal e alvos de junção", delegate
            {
                string tempRoot = TestHelpers.NewTempDirectory("OtimizadorOneDriveTreeTest");
                try
                {
                    string appRoot = Path.Combine(tempRoot, @"AppData\Local\Microsoft\OneDrive");
                    string versionSub = Path.Combine(appRoot, "25.087.0506.0001");
                    string personalOneDrive = Path.Combine(tempRoot, "OneDrive");
                    string personalFile = Path.Combine(personalOneDrive, "documento-importante.txt");
                    string junctionTarget = Path.Combine(tempRoot, "destino-externo");
                    string junctionTargetFile = Path.Combine(junctionTarget, "preservar.txt");
                    string innerJunction = Path.Combine(appRoot, "link-externo");

                    Directory.CreateDirectory(versionSub);
                    Directory.CreateDirectory(personalOneDrive);
                    Directory.CreateDirectory(junctionTarget);
                    File.WriteAllText(Path.Combine(versionSub, "OneDrive.exe"), "stub");
                    File.WriteAllText(personalFile, "dados-do-usuario");
                    File.WriteAllText(junctionTargetFile, "nao-apagar");

                    var psi = new ProcessStartInfo("cmd.exe", string.Format("/c mklink /J \"{0}\" \"{1}\"", innerJunction, junctionTarget))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (Process p = Process.Start(psi))
                    {
                        p.WaitForExit(10000);
                        Assert.Equal(0, p.ExitCode, "Falha ao criar junção de teste.");
                    }

                    // Rejeita caminhos fora das raízes permitidas (ex.: pasta pessoal ou traversal)
                    int inUseCount;
                    Assert.IsFalse(PerformanceManager.DeleteDirectoryTreeSafely(personalOneDrive, new string[] { appRoot }, out inUseCount));
                    Assert.IsFalse(PerformanceManager.DeleteDirectoryTreeSafely(Path.Combine(appRoot, @"..\..\..\OneDrive"), new string[] { appRoot }, out inUseCount));
                    Assert.IsTrue(File.Exists(personalFile), "Pasta pessoal OneDrive jamais pode ser apagada.");

                    // Remove a pasta de binários permitida sem atravessar a junção NTFS
                    Assert.IsTrue(PerformanceManager.DeleteDirectoryTreeSafely(appRoot, new string[] { appRoot }, out inUseCount));
                    Assert.IsFalse(Directory.Exists(appRoot), "Pasta de binários deveria ter sido removida.");
                    Assert.IsTrue(File.Exists(personalFile), "Arquivos pessoais do OneDrive devem permanecer intactos.");
                    Assert.IsTrue(File.Exists(junctionTargetFile), "Destino de junção NTFS jamais pode ser apagado.");
                }
                finally
                {
                    TestHelpers.DeleteDirectorySafe(tempRoot);
                }
            });
        }
    }
}
