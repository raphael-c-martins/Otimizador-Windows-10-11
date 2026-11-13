using System;
using System.Collections.Generic;
using Otimizador.Core;

namespace Otimizador.Tests
{
    public static class CatalogoTests
    {
        public static void Run(TestSuiteRunner runner, string repoRoot)
        {
            runner.Describe("Catálogos de aplicativos e ajustes (embutido e disco)");

            OptimizerCatalog embedded = OptimizationEngine.GetCatalog(null);
            OptimizerCatalog disk = OptimizationEngine.GetCatalog(repoRoot);

            runner.It("carrega aplicativos, ajustes e categorias no catálogo embutido e no disco", delegate
            {
                Assert.GreaterThan(embedded.Apps.Count, 0);
                Assert.GreaterThan(embedded.Tweaks.Count, 0);
                Assert.GreaterThan(embedded.Categories.Count, 0);
                Assert.Equal(disk.Apps.Count, embedded.Apps.Count, "Divergência entre Apps.json embutido e disco.");
                Assert.Equal(disk.Tweaks.Count, embedded.Tweaks.Count, "Divergência entre Tweaks.json embutido e disco.");
                Assert.Equal(disk.Categories.Count, embedded.Categories.Count, "Divergência entre categorias embutidas e disco.");
            });

            runner.It("não possui IDs duplicados", delegate
            {
                var appIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (AppItem a in embedded.Apps)
                {
                    Assert.IsTrue(appIds.Add(a.Id), "App duplicado: " + a.Id);
                }

                var tweakIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (TweakItem t in embedded.Tweaks)
                {
                    Assert.IsTrue(tweakIds.Add(t.Id), "Ajuste duplicado: " + t.Id);
                }
            });

            runner.It("cada ajuste aponta para uma função implementada no motor C#", delegate
            {
                foreach (TweakItem tweak in embedded.Tweaks)
                {
                    Assert.IsTrue(OptimizationEngine.HasTweakFunction(tweak.Function),
                        string.Format("Função ausente para '{0}': '{1}'.", tweak.Id, tweak.Function));
                }
            });

            runner.It("cada ajuste usa uma categoria declarada e inclui todos os ajustes de Cibersegurança (Lote 1)", delegate
            {
                var catIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (TweakCategory c in embedded.Categories) catIds.Add(c.Id);

                Assert.IsTrue(catIds.Contains("Security"), "Categoria 'Security' deve estar declarada.");
                foreach (TweakItem tweak in embedded.Tweaks)
                {
                    Assert.IsTrue(catIds.Contains(tweak.Category), "Categoria não declarada: " + tweak.Category);
                }

                var expectedSecurity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "DisableAdminShares", "Disable-AdminShares" },
                    { "DisableSMBv1", "Disable-SMBv1" },
                    { "DisableWDigest", "Disable-WDigest" },
                    { "DisableLLMNR", "Disable-LLMNR" },
                    { "DisableAutoRunAndAutoPlay", "Disable-AutoRunAndAutoPlay" },
                    { "DisableAnonymousAccess", "Disable-AnonymousAccess" },
                    { "DisableSMBInsecureGuestAuth", "Disable-SMBInsecureGuestAuth" },
                    { "DisableRemoteAssistance", "Disable-RemoteAssistance" },
                    { "EnableDefenderPUAProtection", "Enable-DefenderPUAProtection" }
                };

                foreach (KeyValuePair<string, string> pair in expectedSecurity)
                {
                    string expectedId = pair.Key;
                    TweakItem item = embedded.Tweaks.Find(delegate(TweakItem t) { return t.Id == expectedId; });
                    Assert.IsNotNull(item, string.Format("Ajuste de segurança '{0}' não encontrado.", expectedId));
                    Assert.EqualIgnoreCase(pair.Value, item.Function);
                    Assert.EqualIgnoreCase("Security", item.Category);
                    Assert.IsTrue(item.SelectedByDefault, string.Format("'{0}' deve vir marcado por padrão.", expectedId));
                }
            });

            runner.It("RequiresTweaks dos aplicativos referencia ajustes existentes", delegate
            {
                var tweakIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (TweakItem t in embedded.Tweaks) tweakIds.Add(t.Id);

                foreach (AppItem app in embedded.Apps)
                {
                    if (app.RequiresTweaks == null) continue;
                    foreach (string req in app.RequiresTweaks)
                    {
                        if (!string.IsNullOrWhiteSpace(req))
                        {
                            Assert.IsTrue(tweakIds.Contains(req), "RequiresTweaks inválido: " + req);
                        }
                    }
                }
            });

            runner.It("o Teams de trabalho (MSTeams) e o pessoal (MicrosoftTeams) usam nomes exatos e distintos", delegate
            {
                AppItem work = embedded.Apps.Find(delegate(AppItem a) { return a.Id == "MicrosoftTeams"; });
                AppItem legacy = embedded.Apps.Find(delegate(AppItem a) { return a.Id == "MSTeamsLegacy"; });
                Assert.IsNotNull(work);
                Assert.IsNotNull(legacy);
                Assert.Equal("MSTeams", work.PackageName);
                Assert.Equal("MicrosoftTeams", legacy.PackageName);
                Assert.IsFalse(AppManager.IsPackageMatch("MicrosoftTeams", "MSTeams"));
                Assert.IsFalse(AppManager.IsPackageMatch("MSTeams", "MicrosoftTeams"));
            });

            runner.It("o app People usa Microsoft.People sem atingir Microsoft.Windows.PeopleExperienceHost", delegate
            {
                AppItem people = embedded.Apps.Find(delegate(AppItem a) { return a.Id == "People"; });
                Assert.IsNotNull(people);
                Assert.Equal("Microsoft.People", people.PackageName);
                Assert.IsTrue(AppManager.IsPackageMatch("Microsoft.People", people.PackageName));
                Assert.IsFalse(AppManager.IsPackageMatch("Microsoft.Windows.PeopleExperienceHost", people.PackageName));
            });

            runner.Describe("Perfis (src/Config/Presets)");

            string[] presetNames = OptimizationEngine.GetPresetNames(repoRoot);
            Assert.Equal(4, presetNames.Length, "Esperados 4 perfis padrão.");

            foreach (string name in presetNames)
            {
                string currentName = name;
                runner.It(string.Format("perfil '{0}' é válido, preserva MSTeams e inclui DisableAdminShares", currentName), delegate
                {
                    OptimizerPreset pDisk = OptimizationEngine.ImportPreset(disk, currentName);
                    OptimizerPreset pEmb = OptimizationEngine.ImportPreset(embedded, currentName);
                    Assert.Equal(0, OptimizationEngine.TestPreset(embedded, pEmb).Length);
                    Assert.Equal(pDisk.DisplayName, pEmb.DisplayName);

                    var removed = new HashSet<string>(pEmb.RemoveApps ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                    foreach (AppItem app in embedded.Apps)
                    {
                        if (!removed.Contains(app.Id)) continue;
                        foreach (string pattern in (app.PackageName ?? string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            Assert.IsFalse(AppManager.IsPackageMatch("MSTeams", pattern),
                                string.Format("Perfil '{0}' não pode remover MSTeams.", currentName));
                        }
                    }

                    string[] requiredSecurityTweaks = new string[]
                    {
                        "DisableAdminShares",
                        "DisableSMBv1",
                        "DisableWDigest",
                        "DisableLLMNR",
                        "DisableAutoRunAndAutoPlay",
                        "DisableAnonymousAccess",
                        "DisableSMBInsecureGuestAuth",
                        "DisableRemoteAssistance",
                        "EnableDefenderPUAProtection"
                    };
                    foreach (string secId in requiredSecurityTweaks)
                    {
                        Assert.Contains(secId, pEmb.ApplyTweaks,
                            string.Format("Perfil '{0}' deve incluir '{1}'.", currentName, secId));
                    }
                });
            }

            runner.It("o perfil Corporativo preserva o OneDrive e os demais o removem", delegate
            {
                foreach (string name in presetNames)
                {
                    OptimizerPreset preset = OptimizationEngine.ImportPreset(embedded, name);
                    bool removesOneDrive = preset.ApplyTweaks != null && preset.ApplyTweaks.Exists(
                        delegate(string id) { return string.Equals(id, "RemoveOneDrive", StringComparison.OrdinalIgnoreCase); });
                    Assert.Equal(!string.Equals(name, "Corporativo", StringComparison.OrdinalIgnoreCase), removesOneDrive,
                        "Regra de preservação do OneDrive violada em " + name);
                }
            });
        }
    }
}
