using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Otimizador.Tests
{
    public sealed class TestSuiteRunner
    {
        public int Passed { get; private set; }
        public int Failed { get; private set; }
        private readonly List<string> _failures = new List<string>();

        public void Describe(string groupName)
        {
            Console.WriteLine();
            Console.WriteLine("== " + groupName + " ==");
        }

        public void It(string testName, Action body)
        {
            try
            {
                body();
                Passed++;
                Console.WriteLine("  [PASS] " + testName);
            }
            catch (Exception ex)
            {
                Failed++;
                string msg = string.Format("  [FAIL] {0}: {1}", testName, ex.Message);
                Console.WriteLine(msg);
                _failures.Add(string.Format("{0} -> {1}", testName, ex.ToString()));
            }
        }

        public void PrintSummary()
        {
            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine(string.Format("Total: {0} | Passaram: {1} | Falharam: {2}", Passed + Failed, Passed, Failed));
            if (_failures.Count > 0)
            {
                Console.WriteLine();
                foreach (string f in _failures)
                {
                    Console.WriteLine(f);
                }
            }
        }
    }

    public static class Assert
    {
        public static void IsTrue(bool condition, string message = "Condição esperada verdadeira.")
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void IsFalse(bool condition, string message = "Condição esperada falsa.")
        {
            if (condition) throw new InvalidOperationException(message);
        }

        public static void IsNull(object value, string message = "Valor esperado nulo.")
        {
            if (value != null) throw new InvalidOperationException(string.Format("{0} (obtido: '{1}')", message, value));
        }

        public static void IsNotNull(object value, string message = "Valor esperado não nulo.")
        {
            if (value == null) throw new InvalidOperationException(message);
        }

        public static void Equal<T>(T expected, T actual, string message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(string.Format("{0} Esperado: '{1}', Obtido: '{2}'.",
                    message ?? "Valores diferentes.", expected, actual));
            }
        }

        public static void EqualIgnoreCase(string expected, string actual, string message = null)
        {
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(string.Format("{0} Esperado: '{1}', Obtido: '{2}'.",
                    message ?? "Textos diferentes.", expected, actual));
            }
        }

        public static void GreaterThan(int actual, int threshold, string message = null)
        {
            if (actual <= threshold)
            {
                throw new InvalidOperationException(string.Format("{0} Esperado > {1}, Obtido: {2}.",
                    message ?? "Valor abaixo do limite.", threshold, actual));
            }
        }

        public static void Contains(string item, IEnumerable<string> collection, string message = null)
        {
            if (collection != null)
            {
                foreach (string v in collection)
                {
                    if (string.Equals(v, item, StringComparison.OrdinalIgnoreCase)) return;
                }
            }
            throw new InvalidOperationException(string.Format("{0} Item não encontrado: '{1}'.",
                message ?? "Coleção não contém item.", item));
        }

        public static void Throws(Action action, string message = "Era esperada uma exceção, mas nenhuma foi lançada.")
        {
            try
            {
                action();
            }
            catch
            {
                return;
            }
            throw new InvalidOperationException(message);
        }
    }

    public static class TestHelpers
    {
        public static string FindRepoRoot()
        {
            string dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, @"src\Config\Apps.json"))
                    && File.Exists(Path.Combine(dir, @"src\Config\Tweaks.json")))
                {
                    return dir;
                }
                DirectoryInfo parent = Directory.GetParent(dir);
                dir = parent != null ? parent.FullName : null;
            }
            throw new DirectoryNotFoundException("Não foi possível localizar a raiz do repositório (src/Config/Apps.json).");
        }

        public static string NewTempDirectory(string prefix)
        {
            string dir = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}", prefix, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void DeleteDirectorySafe(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
            try
            {
                foreach (string child in Directory.GetDirectories(dir))
                {
                    var info = new DirectoryInfo(child);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        Directory.Delete(child, false);
                    }
                    else
                    {
                        DeleteDirectorySafe(child);
                    }
                }
                foreach (string file in Directory.GetFiles(dir))
                {
                    FileAttributes attr = File.GetAttributes(file);
                    if ((attr & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(file, attr & ~FileAttributes.ReadOnly);
                    }
                    File.Delete(file);
                }
                Directory.Delete(dir, false);
            }
            catch
            {
            }
        }
    }

    public static class TestMain
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (!Console.IsOutputRedirected)
            {
                try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            }

            var runner = new TestSuiteRunner();
            string repoRoot = TestHelpers.FindRepoRoot();

            CatalogoTests.Run(runner, repoRoot);
            SegurancaTests.Run(runner);
            MotorTests.Run(runner, repoRoot);
            RegistroTests.Run(runner);
            LimpezaTests.Run(runner);
            ExecucaoTests.Run(runner, repoRoot);

            runner.PrintSummary();
            return runner.Failed > 0 ? 1 : 0;
        }
    }
}
