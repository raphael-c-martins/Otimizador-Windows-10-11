using System;
using System.Collections.Generic;
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
        }
    }
}
