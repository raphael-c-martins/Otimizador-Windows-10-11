using System;
using System.Diagnostics;
using System.IO;
using Otimizador.Core;

namespace Otimizador.Tests
{
    public static class LimpezaTests
    {
        public static void Run(TestSuiteRunner runner)
        {
            runner.Describe("TestPathIsWithin (comparação por componentes)");

            runner.It("aceita o próprio caminho e subpastas", delegate
            {
                Assert.IsTrue(PerformanceManager.TestPathIsWithin(@"C:\Temp\App", @"C:\Temp\App"));
                Assert.IsTrue(PerformanceManager.TestPathIsWithin(@"C:\Temp\App\logs\x.log", @"C:\Temp\App"));
                Assert.IsTrue(PerformanceManager.TestPathIsWithin(@"c:\temp\app\", @"C:\Temp\App"));
            });

            runner.It("rejeita pastas com prefixo parecido", delegate
            {
                Assert.IsFalse(PerformanceManager.TestPathIsWithin(@"C:\Temp\Aplicativo", @"C:\Temp\App"));
                Assert.IsFalse(PerformanceManager.TestPathIsWithin(@"C:\Temp\App", @"C:\Temp\Ap"));
            });

            runner.It("resolve '..' antes de comparar", delegate
            {
                Assert.IsFalse(PerformanceManager.TestPathIsWithin(@"C:\Temp\App\..\Outro", @"C:\Temp\App"));
                Assert.IsTrue(PerformanceManager.TestPathIsWithin(@"C:\Temp\Outro\..\App\x", @"C:\Temp\App"));
            });

            runner.Describe("ClearSystemTempFiles");

            string testRoot = TestHelpers.NewTempDirectory("OtimizadorLimpezaTest");
            try
            {
                runner.It("apaga temporários e preserva a pasta do programa e a de logs", delegate
                {
                    string temp = Path.Combine(testRoot, "temp");
                    string appDir = Path.Combine(temp, @"Otimizador-Windows-Package\app");
                    string logDir = Path.Combine(temp, "logs-otimizador");
                    string lixoDir = Path.Combine(temp, "Lixo");
                    Directory.CreateDirectory(appDir);
                    Directory.CreateDirectory(logDir);
                    Directory.CreateDirectory(lixoDir);

                    File.WriteAllText(Path.Combine(temp, "a.tmp"), "x");
                    File.WriteAllText(Path.Combine(lixoDir, "b.tmp"), "x");
                    File.WriteAllText(Path.Combine(appDir, "Otimizador-Windows.exe"), "x");
                    File.WriteAllText(Path.Combine(logDir, "sessao.log"), "x");

                    PerformanceManager.ClearSystemTempFiles(new string[] { appDir, logDir }, new string[] { temp });

                    Assert.IsFalse(File.Exists(Path.Combine(temp, "a.tmp")));
                    Assert.IsFalse(Directory.Exists(lixoDir));
                    Assert.IsTrue(File.Exists(Path.Combine(appDir, "Otimizador-Windows.exe")));
                    Assert.IsTrue(File.Exists(Path.Combine(logDir, "sessao.log")));
                });

                runner.It("remove junções NTFS em temporários sem apagar o conteúdo do diretório de destino", delegate
                {
                    string temp = Path.Combine(testRoot, "temp-junction");
                    string nested = Path.Combine(temp, "subpasta");
                    string target = Path.Combine(testRoot, "destino-protegido");
                    string targetFile = Path.Combine(target, "nao-apagar.txt");
                    string rootJunction = Path.Combine(temp, "atalho-raiz");
                    string nestedJunction = Path.Combine(nested, "atalho-aninhado");

                    Directory.CreateDirectory(temp);
                    Directory.CreateDirectory(nested);
                    Directory.CreateDirectory(target);
                    File.WriteAllText(targetFile, "dados-importantes");

                    var psi1 = new ProcessStartInfo("cmd.exe", string.Format("/c mklink /J \"{0}\" \"{1}\"", rootJunction, target))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (Process p1 = Process.Start(psi1))
                    {
                        p1.WaitForExit(10000);
                        Assert.Equal(0, p1.ExitCode, "Falha ao criar junção de teste na raiz.");
                    }

                    var psi2 = new ProcessStartInfo("cmd.exe", string.Format("/c mklink /J \"{0}\" \"{1}\"", nestedJunction, target))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (Process p2 = Process.Start(psi2))
                    {
                        p2.WaitForExit(10000);
                        Assert.Equal(0, p2.ExitCode, "Falha ao criar junção de teste aninhada.");
                    }

                    PerformanceManager.ClearSystemTempFiles(new string[0], new string[] { temp });

                    Assert.IsFalse(Directory.Exists(rootJunction), "Junção na raiz deveria ter sido desvinculada.");
                    Assert.IsFalse(Directory.Exists(nested), "Subpasta deveria ter sido removida.");
                    Assert.IsTrue(File.Exists(targetFile), "Arquivo fora da pasta temporária NUNCA pode ser apagado via junção.");
                });

                runner.It("continua removendo arquivos livres na mesma subpasta quando há um arquivo bloqueado", delegate
                {
                    string temp = Path.Combine(testRoot, "temp-locked");
                    string sub = Path.Combine(temp, "subpasta");
                    string unlocked = Path.Combine(sub, "livre.tmp");
                    string locked = Path.Combine(sub, "travado.tmp");
                    Directory.CreateDirectory(sub);
                    File.WriteAllText(unlocked, "ok");
                    File.WriteAllText(locked, "em-uso");

                    using (var lockHandle = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        PerformanceManager.ClearSystemTempFiles(new string[0], new string[] { temp });
                    }

                    Assert.IsFalse(File.Exists(unlocked), "Arquivo livre na mesma subpasta deveria ter sido removido.");
                    Assert.IsTrue(File.Exists(locked), "Arquivo bloqueado permanece até ser liberado.");
                });
            }
            finally
            {
                TestHelpers.DeleteDirectorySafe(testRoot);
            }
        }
    }
}
