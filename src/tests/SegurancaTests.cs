using System;
using System.Collections.Generic;
using System.IO;
using Otimizador;
using Otimizador.Core;

namespace Otimizador.Tests
{
    public static class SegurancaTests
    {
        public static void Run(TestSuiteRunner runner)
        {
            runner.Describe("Cibersegurança (compartilhamentos administrativos e CLI)");

            runner.It("seleciona AutoShareWks para Windows Cliente e AutoShareServer para Server", delegate
            {
                Assert.Equal("AutoShareWks", SecurityManager.GetAutoShareValueName(false));
                Assert.Equal("AutoShareServer", SecurityManager.GetAutoShareValueName(true));
            });

            runner.It("identifica apenas compartilhamentos administrativos ocultos padrão (C$, D$, ADMIN$)", delegate
            {
                Assert.IsTrue(SecurityManager.IsAdministrativeDefaultShareName("C$"));
                Assert.IsTrue(SecurityManager.IsAdministrativeDefaultShareName("d$"));
                Assert.IsTrue(SecurityManager.IsAdministrativeDefaultShareName("ADMIN$"));
                Assert.IsTrue(SecurityManager.IsAdministrativeDefaultShareName(" admin$ "));

                Assert.IsFalse(SecurityManager.IsAdministrativeDefaultShareName("IPC$"));
                Assert.IsFalse(SecurityManager.IsAdministrativeDefaultShareName("print$"));
                Assert.IsFalse(SecurityManager.IsAdministrativeDefaultShareName("PROGRAMAS & DRIVERS"));
                Assert.IsFalse(SecurityManager.IsAdministrativeDefaultShareName("Compartilhada"));
                Assert.IsFalse(SecurityManager.IsAdministrativeDefaultShareName("$"));
                Assert.IsFalse(SecurityManager.IsAdministrativeDefaultShareName("CC$"));
                Assert.IsFalse(SecurityManager.IsAdministrativeDefaultShareName(string.Empty));
                Assert.IsFalse(SecurityManager.IsAdministrativeDefaultShareName(null));
            });

            runner.It("filtra apenas compartilhamentos administrativos preservando manuais, IPC$ e print$", delegate
            {
                var found = SecurityManager.FilterAdministrativeShareNames(new string[]
                {
                    "C$", "D$", "ADMIN$", "IPC$", "print$", "PastaManual", "PROGRAMAS & DRIVERS"
                });
                Assert.Equal(3, found.Count);
                Assert.Equal("C$", found[0]);
                Assert.Equal("D$", found[1]);
                Assert.Equal("ADMIN$", found[2]);
            });

            runner.It("analisa saída do net share mesmo com nomes quebrados em múltiplas linhas", delegate
            {
                string sampleOutput = string.Join("\r\n", new string[]
                {
                    "Nome de compartilhamento   Recurso                          Observação",
                    "",
                    "-------------------------------------------------------------------------------",
                    "C$                         C:\\                              Recurso compartilhado padrão",
                    "IPC$                                                        IPC remoto",
                    "ADMIN$                     C:\\Windows                       Administração remota",
                    "PROGRAMAS & DRIVERS",
                    "                           D:\\TI\\PROGRAMAS & DRIVERS",
                    "Compartilhada              D:\\Compartilhada                 Pasta da equipe",
                    "print$                     C:\\Windows\\system32\\spool\\drivers",
                    "Comando concluído com êxito."
                });

                List<string> parsed = SecurityManager.ParseNetShareOutput(sampleOutput);
                Assert.Equal(6, parsed.Count);
                Assert.Contains("C$", parsed);
                Assert.Contains("IPC$", parsed);
                Assert.Contains("ADMIN$", parsed);
                Assert.Contains("PROGRAMAS & DRIVERS", parsed);
                Assert.Contains("Compartilhada", parsed);
                Assert.Contains("print$", parsed);
                Assert.IsFalse(parsed.Contains("Comando concluído com êxito."));
            });

            runner.It("analisa parâmetros da linha de comando e rejeita argumentos inválidos ou incompletos", delegate
            {
                CliOptions ok = Program.ParseArguments(new string[]
                {
                    "-Preset", "Corporativo", "--dry-run", "-Silent", "--skip-restore-point",
                    "--continue-without-restore-point", "-CustomApps", "BingNews,XboxApp"
                });
                Assert.IsNull(ok.ParseError);
                Assert.Equal("Corporativo", ok.Preset);
                Assert.IsTrue(ok.DryRun);
                Assert.IsTrue(ok.Silent);
                Assert.IsTrue(ok.SkipRestorePoint);
                Assert.IsTrue(ok.ContinueWithoutRestorePoint);
                Assert.Equal("BingNews,XboxApp", ok.CustomApps);

                CliOptions undo = Program.ParseArguments(new string[] { "-Undo", "-Help" });
                Assert.IsNull(undo.ParseError);
                Assert.IsTrue(undo.Undo);
                Assert.IsTrue(undo.Help);

                CliOptions badFlag = Program.ParseArguments(new string[] { "-OpcaoInexistente" });
                Assert.IsNotNull(badFlag.ParseError);

                CliOptions missingPreset = Program.ParseArguments(new string[] { "-Preset" });
                Assert.IsNotNull(missingPreset.ParseError);

                CliOptions missingApps = Program.ParseArguments(new string[] { "-CustomApps" });
                Assert.IsNotNull(missingApps.ParseError);

                string quoted = Program.BuildQuotedArguments(new string[] { "-Preset", @"C:\Minha Pasta\Perfil.json", @"C:\Pasta Final\", "argumento \"interno\"" });
                Assert.Equal("-Preset \"C:\\Minha Pasta\\Perfil.json\" \"C:\\Pasta Final\\\\\" \"argumento \\\"interno\\\"\"", quoted);
            });

            runner.It("mantém compatibilidade de leitura com o JSON do diário da v14.0 e ServiceRestart", delegate
            {
                string legacyJson = @"{
                    ""Computador"": ""PC-TESTE"",
                    ""CriadoEm"": ""2026-10-21T10:00:00"",
                    ""Entradas"": [
                        { ""Kind"": ""RegistryValue"", ""Hive"": ""LocalMachine"", ""SubKey"": ""SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters"", ""Name"": ""AutoShareWks"", ""Existed"": false },
                        { ""Kind"": ""ServiceRestart"", ""Name"": ""LanmanServer"" }
                    ]
                }";

                var doc = JsonHelper.Deserialize<JournalDocument>(legacyJson);
                Assert.IsNotNull(doc);
                Assert.Equal("PC-TESTE", doc.Computador);
                Assert.Equal(2, doc.Entradas.Count);
                Assert.Equal("RegistryValue", doc.Entradas[0].Kind);
                Assert.IsFalse(doc.Entradas[0].Existed);
                Assert.Equal("ServiceRestart", doc.Entradas[1].Kind);
                Assert.Equal("LanmanServer", doc.Entradas[1].Name);
            });

            runner.It("registra ServiceRestart antes da chave para que o Undo reverta a chave antes de reiniciar o serviço", delegate
            {
                string tmpDir = TestHelpers.NewTempDirectory("OtimizadorSvcRestartTest");
                string subKey = "Software\\OtimizadorWindows-SvcRestart-" + Guid.NewGuid().ToString("N");
                try
                {
                    string journalPath = ChangeJournal.Start(tmpDir);
                    ChangeJournal.RegisterServiceRestartForUndo("ServicoInexistenteTeste_Otimizador");
                    ChangeJournal.SetRegistryValue("HKCU:\\" + subKey, "AutoShareWks", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    ChangeJournal.Stop();

                    var doc = JsonHelper.Deserialize<JournalDocument>(System.IO.File.ReadAllText(journalPath));
                    Assert.Equal(3, doc.Entradas.Count);
                    Assert.Equal("ServiceRestart", doc.Entradas[0].Kind);
                    Assert.Equal("RegistryKey", doc.Entradas[1].Kind);
                    Assert.Equal("RegistryValue", doc.Entradas[2].Kind);

                    UndoResult undo = ChangeJournal.RestoreJournalFile(journalPath);
                    Assert.Equal(0, undo.Failed);
                    Assert.Equal(3, undo.Restored);
                }
                finally
                {
                    try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(subKey, false); } catch { }
                    TestHelpers.DeleteDirectorySafe(tmpDir);
                }
            });

            runner.Describe("Cibersegurança - Lote 1 (Endurecimento de sistema e Undo)");

            runner.It("define exatamente os caminhos e valores esperados para todos os 8 ajustes do Lote 1", delegate
            {
                RegistrySettingSpec[] smbv1 = SecurityManager.GetSMBv1ServerSettings();
                Assert.Equal(1, smbv1.Length);
                Assert.Equal(SecurityManager.LanmanParametersPath, smbv1[0].Path);
                Assert.Equal("SMB1", smbv1[0].Name);
                Assert.Equal(0, smbv1[0].Value);

                RegistrySettingSpec[] wdigest = SecurityManager.GetWDigestSettings();
                Assert.Equal(1, wdigest.Length);
                Assert.Equal(SecurityManager.WDigestPath, wdigest[0].Path);
                Assert.Equal("UseLogonCredential", wdigest[0].Name);
                Assert.Equal(0, wdigest[0].Value);

                RegistrySettingSpec[] llmnr = SecurityManager.GetLLMNRSettings();
                Assert.Equal(1, llmnr.Length);
                Assert.Equal(SecurityManager.DnsClientPolicyPath, llmnr[0].Path);
                Assert.Equal("EnableMulticast", llmnr[0].Name);
                Assert.Equal(0, llmnr[0].Value);

                RegistrySettingSpec[] autorun = SecurityManager.GetAutoRunAndAutoPlaySettings();
                Assert.Equal(5, autorun.Length);
                Assert.Equal(255, autorun[0].Value);
                Assert.Equal(1, autorun[1].Value);
                Assert.Equal(255, autorun[2].Value);
                Assert.Equal(1, autorun[3].Value);
                Assert.Equal(1, autorun[4].Value);

                RegistrySettingSpec[] anon = SecurityManager.GetAnonymousAccessSettings();
                Assert.Equal(2, anon.Length);
                Assert.Equal("RestrictAnonymous", anon[0].Name);
                Assert.Equal(1, anon[0].Value);
                Assert.Equal("RestrictAnonymousSAM", anon[1].Name);
                Assert.Equal(1, anon[1].Value);

                RegistrySettingSpec[] smbGuest = SecurityManager.GetSMBInsecureGuestAuthSettings();
                Assert.Equal(2, smbGuest.Length);
                Assert.Equal("AllowInsecureGuestAuth", smbGuest[0].Name);
                Assert.Equal(0, smbGuest[0].Value);
                Assert.Equal(0, smbGuest[1].Value);

                RegistrySettingSpec[] remoteAssist = SecurityManager.GetRemoteAssistanceSettings();
                Assert.Equal(2, remoteAssist.Length);
                Assert.Equal("fAllowToGetHelp", remoteAssist[0].Name);
                Assert.Equal(0, remoteAssist[0].Value);
                Assert.Equal("fAllowFullControl", remoteAssist[1].Name);
                Assert.Equal(0, remoteAssist[1].Value);

                RegistrySettingSpec[] pua = SecurityManager.GetDefenderPUAProtectionSettings();
                Assert.Equal(2, pua.Length);
                Assert.Equal("PUAProtection", pua[0].Name);
                Assert.Equal(1, pua[0].Value);
                Assert.Equal("MpEnablePus", pua[1].Name);
                Assert.Equal(1, pua[1].Value);
            });

            runner.It("remove MRxSmb10 de DependOnService preservando os demais drivers sem alterações quando ausente", delegate
            {
                string[] updated = SecurityManager.FilterDependOnService(
                    new string[] { "Bowser", "mrxsmb10", "MRxSmb20", "NSI" },
                    "MRxSmb10");
                Assert.IsNotNull(updated);
                Assert.Equal(3, updated.Length);
                Assert.Equal("Bowser", updated[0]);
                Assert.Equal("MRxSmb20", updated[1]);
                Assert.Equal("NSI", updated[2]);

                string[] unchanged = SecurityManager.FilterDependOnService(
                    new string[] { "Bowser", "MRxSmb20", "NSI" },
                    "MRxSmb10");
                Assert.IsNull(unchanged);
            });

            runner.It("aplica todas as especificações do Lote 1 em chaves temporárias HKCU e reverte exatamente via Undo", delegate
            {
                string tmpDir = TestHelpers.NewTempDirectory("OtimizadorLote1Test");
                string testPrefix = "Software\\OtimizadorWindows-Lote1-" + Guid.NewGuid().ToString("N");
                var allSpecs = new List<RegistrySettingSpec>();
                allSpecs.AddRange(SecurityManager.GetSMBv1ServerSettings());
                allSpecs.AddRange(SecurityManager.GetWDigestSettings());
                allSpecs.AddRange(SecurityManager.GetLLMNRSettings());
                allSpecs.AddRange(SecurityManager.GetAutoRunAndAutoPlaySettings());
                allSpecs.AddRange(SecurityManager.GetAnonymousAccessSettings());
                allSpecs.AddRange(SecurityManager.GetSMBInsecureGuestAuthSettings());
                allSpecs.AddRange(SecurityManager.GetRemoteAssistanceSettings());
                allSpecs.AddRange(SecurityManager.GetDefenderPUAProtectionSettings());

                try
                {
                    // Pré-semeia um valor anterior para testar preservação e restauração de valor existente no Undo
                    string preExistingPath = SecurityManager.BuildMappedTestPath(SecurityManager.LsaPath, testPrefix);
                    RegistryLocation preExistingLoc = ChangeJournal.ConvertToRegistryLocation(preExistingPath);
                    using (Microsoft.Win32.RegistryKey preKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(preExistingLoc.SubKey))
                    {
                        preKey.SetValue("RestrictAnonymous", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    }

                    string journalPath = ChangeJournal.Start(tmpDir);
                    SecurityManager.ApplyRegistrySettings(allSpecs, testPrefix);
                    ChangeJournal.Stop();

                    foreach (RegistrySettingSpec spec in allSpecs)
                    {
                        string mapped = SecurityManager.BuildMappedTestPath(spec.Path, testPrefix);
                        RegistryLocation loc = ChangeJournal.ConvertToRegistryLocation(mapped);
                        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(loc.SubKey))
                        {
                            Assert.IsNotNull(k, "Chave deveria existir após aplicação: " + loc.SubKey);
                            Assert.Equal(spec.Value, Convert.ToInt32(k.GetValue(spec.Name)),
                                string.Format("Valor divergente para {0}.", spec.Name));
                        }
                    }

                    UndoResult undo = ChangeJournal.RestoreJournalFile(journalPath);
                    Assert.Equal(0, undo.Failed);
                    Assert.GreaterThan(undo.Restored, allSpecs.Count);

                    using (Microsoft.Win32.RegistryKey restoredKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(preExistingLoc.SubKey))
                    {
                        Assert.IsNotNull(restoredKey, "Chave pré-existente deve permanecer após Undo.");
                        Assert.Equal(0, Convert.ToInt32(restoredKey.GetValue("RestrictAnonymous")), "Valor original pré-existente deve voltar para 0.");
                        Assert.IsNull(restoredKey.GetValue("RestrictAnonymousSAM"), "Valor criado pelo Lote 1 deve ser removido no Undo.");
                    }

                    RegistryLocation wdigestLoc = ChangeJournal.ConvertToRegistryLocation(
                        SecurityManager.BuildMappedTestPath(SecurityManager.WDigestPath, testPrefix));
                    using (Microsoft.Win32.RegistryKey removedWDigest = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(wdigestLoc.SubKey))
                    {
                        Assert.IsNull(removedWDigest, "Chaves criadas apenas pela otimização devem ser removidas no Undo.");
                    }
                }
                finally
                {
                    try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testPrefix, false); } catch { }
                    TestHelpers.DeleteDirectorySafe(tmpDir);
                }
            });

            runner.Describe("BackupManager, Sanitização de Comandos e Fallbacks");

            runner.It("sanitiza rótulo do Ponto de Restauração e limita descrição a 63 caracteres", delegate
            {
                Assert.Equal("Personalizado", BackupManager.SanitizeRestorePointLabel(null));
                Assert.Equal("Personalizado", BackupManager.SanitizeRestorePointLabel("   !!!   "));
                Assert.Equal("Perfil_Corporativo_whoami_2026", BackupManager.SanitizeRestorePointLabel("Perfil Corporativo; $(whoami) 2026"));

                string longLabel = new string('A', 60);
                string sanitized = BackupManager.SanitizeRestorePointLabel(longLabel);
                Assert.Equal(32, sanitized.Length);

                string fullDesc = BackupManager.NewRestorePointDescription(longLabel);
                Assert.IsTrue(fullDesc.Length <= 63, "Descrição deve caber no limite do Windows (<= 63 chars), veio: " + fullDesc.Length);
            });

            runner.It("confirma Ponto de Restauração apenas quando descrição e janela temporal coincidem", delegate
            {
                DateTime now = new DateTime(2026, 11, 13, 10, 0, 0);
                Assert.IsTrue(BackupManager.IsMatchingRestorePoint("Otimizador_Gamer_2026", now.AddSeconds(5), "Otimizador_Gamer_2026", now));
                Assert.IsTrue(BackupManager.IsMatchingRestorePoint("Otimizador_Gamer_2026", now.AddMinutes(-1), "Otimizador_Gamer_2026", now));
                Assert.IsTrue(BackupManager.IsMatchingRestorePoint("Otimizador_Gamer_2026", DateTime.MinValue, "Otimizador_Gamer_2026", now));

                Assert.IsFalse(BackupManager.IsMatchingRestorePoint("Otimizador_Gamer_2026", now.AddMinutes(-5), "Otimizador_Gamer_2026", now));
                Assert.IsFalse(BackupManager.IsMatchingRestorePoint("Outro_Ponto", now, "Otimizador_Gamer_2026", now));
                Assert.IsFalse(BackupManager.IsMatchingRestorePoint("otimizador_gamer_2026", now, "Otimizador_Gamer_2026", now));
            });

            runner.It("monta diagnóstico claro de falha do Ponto de Restauração (evento 8216 e códigos)", delegate
            {
                string withRecent = BackupManager.BuildNotConfirmedMessage(new RestorePointInfo
                {
                    Description = "Windows Update",
                    CreationTime = new DateTime(2026, 11, 13, 9, 30, 0)
                });
                Assert.IsTrue(withRecent.Contains("8216"), "Deveria citar o evento 8216.");
                Assert.IsTrue(withRecent.Contains("Windows Update"), "Deveria citar o ponto recente existente.");

                string withoutPoints = BackupManager.BuildNotConfirmedMessage(null);
                Assert.IsTrue(withoutPoints.Contains("Proteção do Sistema"));

                string withRecentDiag = BackupManager.BuildNotConfirmedMessage(new RestorePointInfo
                {
                    Description = "Windows Update",
                    CreationTime = new DateTime(2026, 11, 13, 9, 30, 0)
                }, "Evento 8216 (SystemRestore): ponto recente ignorado");
                Assert.IsTrue(withRecentDiag.Contains("Detalhe do Windows: Evento 8216"));

                DateTime refNow = new DateTime(2026, 11, 13, 12, 0, 0);
                Assert.IsTrue(BackupManager.IsRecentRestorePoint(new RestorePointInfo { Description = "P1", CreationTime = refNow.AddHours(-2) }, refNow, 24));
                Assert.IsFalse(BackupManager.IsRecentRestorePoint(new RestorePointInfo { Description = "P2", CreationTime = refNow.AddHours(-30) }, refNow, 24));
                Assert.IsFalse(BackupManager.IsRecentRestorePoint(new RestorePointInfo { Description = "P3", CreationTime = DateTime.MinValue }, refNow, 24));
                Assert.IsFalse(BackupManager.IsRecentRestorePoint(null, refNow, 24));

                Assert.IsTrue(BackupManager.DescribeSystemRestoreReturnCode(1058).Contains("1058"));
                Assert.IsTrue(BackupManager.DescribeSystemRestoreReturnCode(112).Contains("112"));
                Assert.IsTrue(BackupManager.DescribeSystemRestoreReturnCode(999).Contains("0x000003E7"));
            });

            runner.It("resolve executáveis padrão para System32 e valida caminhos confiáveis de desinstaladores", delegate
            {
                string resolvedCmd = SystemInfo.ResolveSystemExecutable("cmd.exe");
                Assert.IsTrue(Path.IsPathRooted(resolvedCmd));
                Assert.IsTrue(PerformanceManager.TestPathIsWithin(resolvedCmd, Environment.SystemDirectory),
                    "cmd.exe deveria resolver para System32, veio: " + resolvedCmd);

                Assert.IsTrue(AppManager.IsValidWinGetPackageId("Microsoft.GamingApp"));
                Assert.IsTrue(AppManager.IsValidWinGetPackageId("XP9CXNGPPJ97XX"));
                Assert.IsFalse(AppManager.IsValidWinGetPackageId(null));
                Assert.IsFalse(AppManager.IsValidWinGetPackageId("pkg\" --silent"));
                Assert.IsFalse(AppManager.IsValidWinGetPackageId("pkg; calc.exe"));

                string fakeRoot = TestHelpers.NewTempDirectory("OtimizadorTrustedExeTest");
                try
                {
                    string trustedSub = Path.Combine(fakeRoot, @"Microsoft\Edge\Application");
                    string untrustedSub = Path.Combine(fakeRoot, "Downloads");
                    Directory.CreateDirectory(trustedSub);
                    Directory.CreateDirectory(untrustedSub);

                    string trustedExe = Path.Combine(trustedSub, "setup.exe");
                    string wrongNameExe = Path.Combine(trustedSub, "outro.exe");
                    string hijackedExe = Path.Combine(untrustedSub, "setup.exe");
                    File.WriteAllText(trustedExe, "stub");
                    File.WriteAllText(wrongNameExe, "stub");
                    File.WriteAllText(hijackedExe, "stub");

                    string[] roots = new string[] { Path.Combine(fakeRoot, @"Microsoft\Edge") };
                    Assert.IsTrue(AppManager.IsTrustedUninstallExecutable(trustedExe, "setup.exe", roots));
                    Assert.IsFalse(AppManager.IsTrustedUninstallExecutable(wrongNameExe, "setup.exe", roots));
                    Assert.IsFalse(AppManager.IsTrustedUninstallExecutable(hijackedExe, "setup.exe", roots));
                    Assert.IsFalse(AppManager.IsTrustedUninstallExecutable(Path.Combine(trustedSub, @"..\..\..\Downloads\setup.exe"), "setup.exe", roots));
                    Assert.IsFalse(AppManager.IsTrustedUninstallExecutable(@"setup.exe", "setup.exe", roots));
                }
                finally
                {
                    TestHelpers.DeleteDirectorySafe(fakeRoot);
                }
            });

            runner.It("evita colisão ao arquivar diário restaurado quando .restaurado.json já existe", delegate
            {
                string tmpDir = TestHelpers.NewTempDirectory("OtimizadorUndoCollisionTest");
                string subKey = "Software\\OtimizadorWindows-UndoCol-" + Guid.NewGuid().ToString("N");
                try
                {
                    string journalPath = ChangeJournal.Start(tmpDir);
                    ChangeJournal.SetRegistryValue("HKCU:\\" + subKey, "Teste", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    ChangeJournal.Stop();

                    string baseName = Path.GetFileNameWithoutExtension(journalPath);
                    string existingRestored = Path.Combine(tmpDir, baseName + ".restaurado.json");
                    File.WriteAllText(existingRestored, "{}");

                    UndoResult res = UndoManager.InvokeSystemUndo(tmpDir);
                    Assert.Equal(0, res.Failed);
                    Assert.GreaterThan(res.Restored, 0);
                    Assert.IsFalse(File.Exists(journalPath), "Diário original deveria ter sido arquivado.");
                    Assert.IsTrue(File.Exists(Path.Combine(tmpDir, baseName + ".restaurado (2).json")),
                        "Arquivo restaurado duplicado deveria usar sufixo único (2).");
                }
                finally
                {
                    try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(subKey, false); } catch { }
                    TestHelpers.DeleteDirectorySafe(tmpDir);
                }
            });

            runner.It("identifica quando o armazenamento VSS (shadowstorage) precisa ser configurado ou redimensionado", delegate
            {
                Assert.IsTrue(BackupManager.NeedsShadowStorageSetup(null));
                Assert.IsTrue(BackupManager.NeedsShadowStorageSetup("Nenhum item encontrado que atenda à consulta."));
                Assert.IsTrue(BackupManager.NeedsShadowStorageSetup("No items found that satisfy the query."));

                string zeroMaxPt = string.Join("\r\n", new string[]
                {
                    "Para o volume: (C:)\\\\?\\Volume{abc}\\",
                    "Volume de Armazenamento de Cópias de Sombra: (C:)\\\\?\\Volume{abc}\\",
                    "  Espaço Usado de Armazenamento de Cópias de Sombra: 0 bytes (0%)",
                    "  Espaço Alocado de Armazenamento de Cópias de Sombra: 0 bytes (0%)",
                    "  Espaço Máximo de Armazenamento de Cópias de Sombra: 0 bytes (0%)"
                });
                Assert.IsTrue(BackupManager.NeedsShadowStorageSetup(zeroMaxPt));

                string validPt = string.Join("\r\n", new string[]
                {
                    "Para o volume: (C:)\\\\?\\Volume{abc}\\",
                    "Volume de Armazenamento de Cópias de Sombra: (C:)\\\\?\\Volume{abc}\\",
                    "  Espaço Usado de Armazenamento de Cópias de Sombra: 0 bytes (0%)",
                    "  Espaço Alocado de Armazenamento de Cópias de Sombra: 0 bytes (0%)",
                    "  Espaço Máximo de Armazenamento de Cópias de Sombra: 4,77 GB (5%)"
                });
                Assert.IsFalse(BackupManager.NeedsShadowStorageSetup(validPt));

                string validEnUnbounded = string.Join("\r\n", new string[]
                {
                    "For volume: (C:)\\\\?\\Volume{abc}\\",
                    "Shadow Copy Storage volume: (C:)\\\\?\\Volume{abc}\\",
                    "  Used Shadow Copy Storage space: 0 bytes (0%)",
                    "  Allocated Shadow Copy Storage space: 0 bytes (0%)",
                    "  Maximum Shadow Copy Storage space: UNBOUNDED"
                });
                Assert.IsFalse(BackupManager.NeedsShadowStorageSetup(validEnUnbounded));

                string msgWithDiag = BackupManager.BuildNotConfirmedMessage(null, "Evento 8193 (VSS): erro ao criar cópia");
                Assert.IsTrue(msgWithDiag.Contains("Detalhe do Windows: Evento 8193 (VSS)"));
                string msgBase = BackupManager.BuildNotConfirmedMessage(null);
                Assert.IsFalse(msgBase.Contains("Detalhe do Windows:"));
            });

            runner.It("espelha o log da sessão para caminho acessível sem perder linhas iniciais", delegate
            {
                string tmpRoot = TestHelpers.NewTempDirectory("OtimizadorLogMirrorTest");
                try
                {
                    string primary = Path.Combine(tmpRoot, @"ProgramData\logs\Otimizacao_TESTE.log");
                    string mirror = Path.Combine(tmpRoot, @"Documentos\OtimizadorWindows\Otimizacao_TESTE.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(primary));

                    Logger.SetTarget(primary, null, null);
                    Logger.Write("INFO", "Linha inicial antes do espelho");

                    string attached = Logger.SetAccessibleLogTarget(mirror);
                    Assert.Equal(mirror, attached);
                    Assert.Equal(mirror, Logger.GetDisplayLogFilePath());

                    Logger.Write("OK", "Linha depois do espelho");

                    string primaryContent = File.ReadAllText(primary);
                    string mirrorContent = File.ReadAllText(mirror);
                    Assert.IsTrue(primaryContent.Contains("Linha inicial antes do espelho"));
                    Assert.IsTrue(primaryContent.Contains("Linha depois do espelho"));
                    Assert.IsTrue(mirrorContent.Contains("Linha inicial antes do espelho"));
                    Assert.IsTrue(mirrorContent.Contains("Linha depois do espelho"));

                    Logger.SetTarget(null, null, null);
                    Assert.IsNull(Logger.GetDisplayLogFilePath());
                }
                finally
                {
                    Logger.SetTarget(null, null, null);
                    TestHelpers.DeleteDirectorySafe(tmpRoot);
                }
            });
        }
    }
}
