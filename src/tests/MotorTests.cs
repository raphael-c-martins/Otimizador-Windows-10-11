using System;
using System.Collections.Generic;
using System.IO;
using Otimizador.Core;

namespace Otimizador.Tests
{
    public static class MotorTests
    {
        public static void Run(TestSuiteRunner runner, string repoRoot)
        {
            OptimizerCatalog catalog = OptimizationEngine.GetCatalog(null);
            string tempDir = TestHelpers.NewTempDirectory("OtimizadorMotorTest");

            try
            {
                runner.Describe("Validação de perfis e seleções");

                runner.It("rejeita perfil inexistente", delegate
                {
                    Assert.Throws(delegate { OptimizationEngine.ImportPreset(catalog, "NaoExiste"); });
                });

                runner.It("rejeita nome de perfil com caminho relativo", delegate
                {
                    Assert.Throws(delegate { OptimizationEngine.ImportPreset(catalog, @"..\..\src\Config\Apps"); });
                });

                runner.It("carrega perfil personalizado a partir de arquivo .json", delegate
                {
                    string path = Path.Combine(tempDir, "MeuPerfil.json");
                    File.WriteAllText(path,
                        "{ \"PresetName\": \"Meu\", \"DisplayName\": \"Meu Perfil\", \"RemoveApps\": [\"BingNews\"], \"PreserveApps\": [], \"ApplyTweaks\": [\"ShowFileExtensions\"] }");
                    OptimizerPreset loaded = OptimizationEngine.ImportPreset(catalog, path);
                    Assert.Equal("Meu Perfil", loaded.DisplayName);
                });

                runner.It("rejeita perfil .json com ajuste inexistente", delegate
                {
                    string path = Path.Combine(tempDir, "Invalido.json");
                    File.WriteAllText(path,
                        "{ \"DisplayName\": \"Invalido\", \"RemoveApps\": [], \"ApplyTweaks\": [\"AjusteQueNaoExiste\"] }");
                    Assert.Throws(delegate { OptimizationEngine.ImportPreset(catalog, path); });
                });

                runner.It("rejeita IDs desconhecidos antes de executar qualquer etapa", delegate
                {
                    Assert.Throws(delegate
                    {
                        OptimizationEngine.InvokeOptimizationPlan(catalog, new string[] { "AppQueNaoExiste" }, null, new ExecutionContext(), true);
                    });
                    Assert.Throws(delegate
                    {
                        OptimizationEngine.InvokeOptimizationPlan(catalog, null, new string[] { "AjusteQueNaoExiste" }, new ExecutionContext(), true);
                    });
                });

                runner.Describe("Simulação (-DryRun)");

                runner.It("não executa nada, não cria diário e conta as etapas corretamente", delegate
                {
                    ExecutionResult result = OptimizationEngine.InvokeOptimizationPlan(
                        catalog,
                        new string[] { "BingNews" },
                        new string[] { "ShowFileExtensions" },
                        new ExecutionContext { RestartExplorer = true },
                        true);
                    Assert.Equal(3, result.Simulated);
                    Assert.Equal(0, result.Failed);
                    Assert.IsNull(result.JournalPath);
                });

                runner.It("inclui FixGameBarProtocols automaticamente ao remover XboxGamingOverlay", delegate
                {
                    ExecutionResult result = OptimizationEngine.InvokeOptimizationPlan(
                        catalog,
                        new string[] { "XboxGamingOverlay" },
                        null,
                        new ExecutionContext { RestartExplorer = false },
                        true);

                    TweakItem fixTweak = catalog.Tweaks.Find(delegate(TweakItem t) { return t.Id == "FixGameBarProtocols"; });
                    Assert.IsNotNull(fixTweak);
                    var stepNames = new List<string>();
                    foreach (StepResult s in result.Steps) stepNames.Add(s.Name);
                    Assert.Contains(fixTweak.Name, stepNames);
                });

                runner.Describe("Execução com falhas isoladas");

                var fakeCatalog = new OptimizerCatalog
                {
                    BaseDirectory = repoRoot,
                    Apps = new List<AppItem>(),
                    Categories = new List<TweakCategory> { new TweakCategory { Id = "Teste", Name = "Teste" } },
                    Tweaks = new List<TweakItem>
                    {
                        new TweakItem { Id = "Falha", Function = "Test-AjusteQueFalha", Name = "Ajuste que falha", Category = "Teste" },
                        new TweakItem { Id = "Ok", Function = "Test-AjusteOk", Name = "Ajuste ok", Category = "Teste" },
                        new TweakItem { Id = "Aviso", Function = "Test-AjusteComAviso", Name = "Ajuste com aviso", Category = "Teste" }
                    }
                };

                var customHandlers = new Dictionary<string, Action<ExecutionContext>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Test-AjusteQueFalha", delegate { throw new InvalidOperationException("falha simulada"); } },
                    { "Test-AjusteOk", delegate { } },
                    { "Test-AjusteComAviso", delegate { Logger.Write("AVISO", "aviso simulado"); } }
                };

                runner.It("reporta a falha sem interromper as demais etapas", delegate
                {
                    var ctx = new ExecutionContext
                    {
                        JournalDirectory = Path.Combine(tempDir, "estado-falhas"),
                        RestartExplorer = false
                    };
                    ExecutionResult result = OptimizationEngine.InvokeOptimizationPlan(
                        fakeCatalog,
                        null,
                        new string[] { "Falha", "Ok", "Aviso" },
                        ctx,
                        false,
                        customHandlers);

                    Assert.Equal(3, result.Steps.Count);
                    Assert.Equal(1, result.Failed);
                    Assert.Equal(1, result.Ok);
                    Assert.Equal(1, result.Warnings);
                    StepResult failedStep = result.Steps.Find(delegate(StepResult s) { return s.Name == "Ajuste que falha"; });
                    Assert.IsNotNull(failedStep);
                    Assert.Equal("falha simulada", failedStep.Detail);
                });

                runner.It("rejeita ajuste que aponta para função inexistente", delegate
                {
                    var brokenCatalog = new OptimizerCatalog
                    {
                        BaseDirectory = repoRoot,
                        Apps = new List<AppItem>(),
                        Categories = new List<TweakCategory>(),
                        Tweaks = new List<TweakItem>
                        {
                            new TweakItem { Id = "X", Function = "Funcao-QueNaoExiste", Name = "X", Category = "Teste" }
                        }
                    };
                    Assert.Throws(delegate
                    {
                        OptimizationEngine.InvokeOptimizationPlan(
                            brokenCatalog,
                            null,
                            new string[] { "X" },
                            new ExecutionContext { JournalDirectory = Path.Combine(tempDir, "estado-broken"), RestartExplorer = false },
                            false);
                    });
                });
            }
            finally
            {
                TestHelpers.DeleteDirectorySafe(tempDir);
            }
        }
    }
}
