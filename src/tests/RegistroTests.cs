using System;
using System.IO;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;
using Otimizador.Core;

namespace Otimizador.Tests
{
    public static class RegistroTests
    {
        public static void Run(TestSuiteRunner runner)
        {
            string subKeyRoot = "Software\\OtimizadorWindows-Testes-" + Guid.NewGuid().ToString("N");
            string testRoot = "HKCU:\\" + subKeyRoot;
            string tempDir = TestHelpers.NewTempDirectory("OtimizadorRegistroTest");
            string journalDir = Path.Combine(tempDir, "estado");

            runner.Describe("Diário de alterações do registro");

            try
            {
                runner.It("registra a criação de chave e a remove ao reverter", delegate
                {
                    string journal = ChangeJournal.Start(journalDir);
                    ChangeJournal.SetRegistryValue(testRoot + "\\Sub", "Valor", 1, RegistryValueKind.DWord);
                    ChangeJournal.Stop();

                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyRoot + "\\Sub"))
                    {
                        Assert.IsNotNull(key);
                        Assert.Equal(1, Convert.ToInt32(key.GetValue("Valor")));
                    }

                    UndoResult res = ChangeJournal.RestoreJournalFile(journal);
                    Assert.Equal(0, res.Failed);
                    using (RegistryKey root = Registry.CurrentUser.OpenSubKey(subKeyRoot))
                    {
                        Assert.IsNull(root, "A chave temporária criada pelo ajuste deveria ter sido removida.");
                    }
                });

                runner.It("devolve valores originais (String, DWord, QWord, MultiString, Binary) e remove os novos", delegate
                {
                    byte[] origBin = new byte[] { 1, 2, 3, 4 };
                    string[] origMulti = new string[] { "a", "b" };

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyRoot))
                    {
                        key.SetValue("Texto", "original", RegistryValueKind.String);
                        key.SetValue("Numero", 7, RegistryValueKind.DWord);
                        key.SetValue("Longo", 9999999999L, RegistryValueKind.QWord);
                        key.SetValue("Lista", origMulti, RegistryValueKind.MultiString);
                        key.SetValue("Binario", origBin, RegistryValueKind.Binary);
                    }

                    string journal = ChangeJournal.Start(journalDir);
                    ChangeJournal.SetRegistryValue(testRoot, "Texto", "alterado", RegistryValueKind.String);
                    ChangeJournal.SetRegistryValue(testRoot, "Texto", "alterado-de-novo", RegistryValueKind.String);
                    ChangeJournal.SetRegistryValue(testRoot, "Numero", 0, RegistryValueKind.DWord);
                    ChangeJournal.SetRegistryValue(testRoot, "Longo", 123L, RegistryValueKind.QWord);
                    ChangeJournal.SetRegistryValue(testRoot, "Lista", new string[] { "x" }, RegistryValueKind.MultiString);
                    ChangeJournal.SetRegistryValue(testRoot, "Binario", new byte[] { 9, 9 }, RegistryValueKind.Binary);
                    ChangeJournal.SetRegistryValue(testRoot, "Novo", 1, RegistryValueKind.DWord);
                    ChangeJournal.Stop();

                    UndoResult res = ChangeJournal.RestoreJournalFile(journal);
                    Assert.Equal(0, res.Failed);

                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyRoot))
                    {
                        Assert.IsNotNull(key);
                        Assert.Equal("original", Convert.ToString(key.GetValue("Texto")));
                        Assert.Equal(7, Convert.ToInt32(key.GetValue("Numero")));
                        Assert.Equal(9999999999L, Convert.ToInt64(key.GetValue("Longo")));
                        string[] restoredMulti = (string[])key.GetValue("Lista");
                        Assert.Equal(2, restoredMulti.Length);
                        Assert.Equal("a", restoredMulti[0]);
                        Assert.Equal("b", restoredMulti[1]);
                        byte[] restoredBin = (byte[])key.GetValue("Binario");
                        Assert.Equal(4, restoredBin.Length);
                        Assert.Equal(4, (int)restoredBin[3]);
                        Assert.IsNull(key.GetValue("Novo"));
                    }
                });

                runner.It("o Undo reverte várias execuções da mais nova para a mais antiga", delegate
                {
                    string undoDir = Path.Combine(tempDir, "estado-undo");
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyRoot))
                    {
                        key.SetValue("Nivel", 7, RegistryValueKind.DWord);
                    }

                    ChangeJournal.Start(undoDir);
                    ChangeJournal.SetRegistryValue(testRoot, "Nivel", 1, RegistryValueKind.DWord);
                    ChangeJournal.Stop();
                    Thread.Sleep(25);
                    ChangeJournal.Start(undoDir);
                    ChangeJournal.SetRegistryValue(testRoot, "Nivel", 2, RegistryValueKind.DWord);
                    ChangeJournal.Stop();

                    UndoResult result = UndoManager.InvokeSystemUndo(undoDir, true, false, true);
                    Assert.Equal("Diario", result.Mode);
                    Assert.Equal(0, result.Failed);
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyRoot))
                    {
                        Assert.Equal(7, Convert.ToInt32(key.GetValue("Nivel")));
                    }
                    Assert.Equal(0, ChangeJournal.GetPendingJournals(undoDir).Length);
                    Assert.Equal(2, Directory.GetFiles(undoDir, "*.restaurado.json").Length);
                });

                runner.It("redireciona HKCU para o perfil do usuário conectado quando indicado", delegate
                {
                    string sid = WindowsIdentity.GetCurrent().User.Value;
                    ChangeJournal.SetUserRegistryTarget(sid);
                    try
                    {
                        RegistryLocation loc = ChangeJournal.ConvertToRegistryLocation(@"HKCU:\Software\Teste");
                        Assert.Equal(RegistryHive.Users, loc.Hive);
                        Assert.Equal(sid + "\\Software\\Teste", loc.SubKey);
                    }
                    finally
                    {
                        ChangeJournal.SetUserRegistryTarget(null);
                    }

                    RegistryLocation reverted = ChangeJournal.ConvertToRegistryLocation(@"HKCU:\Software\Teste");
                    Assert.Equal(RegistryHive.CurrentUser, reverted.Hive);
                });

                runner.It("rejeita caminhos de registro fora do padrão", delegate
                {
                    Assert.Throws(delegate { ChangeJournal.ConvertToRegistryLocation(@"C:\Windows"); });
                });

                runner.It("evita sobrescrever diários ou logs existentes em execuções no mesmo instante", delegate
                {
                    string colDir = Path.Combine(tempDir, "colisao");
                    Directory.CreateDirectory(colDir);
                    string stamp = "2026-11-10_12-00-00-000";

                    string j1 = ChangeJournal.ResolveUniqueJournalPath(colDir, stamp);
                    File.WriteAllText(j1, "{}");
                    string j2 = ChangeJournal.ResolveUniqueJournalPath(colDir, stamp);
                    File.WriteAllText(j2, "{}");
                    string j3 = ChangeJournal.ResolveUniqueJournalPath(colDir, stamp);
                    File.WriteAllText(j3, "{}");

                    Assert.Equal("estado_2026-11-10_12-00-00-000.json", Path.GetFileName(j1));
                    Assert.Equal("estado_2026-11-10_12-00-00-000_002.json", Path.GetFileName(j2));
                    Assert.Equal("estado_2026-11-10_12-00-00-000_003.json", Path.GetFileName(j3));

                    FileInfo[] pending = ChangeJournal.GetPendingJournals(colDir);
                    Assert.Equal(3, pending.Length);
                    Assert.Equal(Path.GetFileName(j3), pending[0].Name);
                    Assert.Equal(Path.GetFileName(j2), pending[1].Name);
                    Assert.Equal(Path.GetFileName(j1), pending[2].Name);

                    string log1 = Path.Combine(colDir, "Otimizacao_HOST_2026-11-10_12-00-00.log");
                    File.WriteAllText(log1, "1");
                    string log2 = Logger.ResolveUniqueFilePath(log1);
                    Assert.Equal("Otimizacao_HOST_2026-11-10_12-00-00 (2).log", Path.GetFileName(log2));
                });
            }
            finally
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(subKeyRoot, false); } catch { }
                TestHelpers.DeleteDirectorySafe(tempDir);
            }
        }
    }
}
