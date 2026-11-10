using System;
using System.Collections.Generic;
using Otimizador.Core;

namespace Otimizador.UI
{
    public static class ConsoleTUI
    {
        private const string Esc = "\x1b";
        public static bool UseAnsiColors { get; set; }

        public static string C_B { get { return UseAnsiColors ? Esc + "[38;2;52;152;219m" : string.Empty; } }
        public static string C_C { get { return UseAnsiColors ? Esc + "[38;2;26;188;156m" : string.Empty; } }
        public static string C_G { get { return UseAnsiColors ? Esc + "[38;2;149;165;166m" : string.Empty; } }
        public static string C_W { get { return UseAnsiColors ? Esc + "[38;2;236;240;241m" : string.Empty; } }
        public static string C_GN { get { return UseAnsiColors ? Esc + "[38;2;46;204;113m" : string.Empty; } }
        public static string C_R { get { return UseAnsiColors ? Esc + "[38;2;231;76;60m" : string.Empty; } }
        public static string C_Y { get { return UseAnsiColors ? Esc + "[38;2;241;196;15m" : string.Empty; } }
        public static string C_P { get { return UseAnsiColors ? Esc + "[38;2;155;89;182m" : string.Empty; } }
        public static string C_RST { get { return UseAnsiColors ? Esc + "[0m" : string.Empty; } }

        public static void ShowMainHeader(string subTitle = "PROTOCOLO DE OTIMIZAÇÃO & DEBLOAT", string osCaption = null)
        {
            try { Console.Clear(); } catch { }
            Console.WriteLine();
            Console.WriteLine(C_B + @"   ____  ____  _____ ___ __  __ ___ _____ _____ ____  " + C_RST);
            Console.WriteLine(C_B + @"  / __ \|  _ \|_   _|_ _|  \/  |_ _|__  /| ____|  _ \ " + C_RST);
            Console.WriteLine(C_B + @" | |  | | |_) | | |  | || |\/| || |  / / |  _| | |_) |" + C_RST);
            Console.WriteLine(C_B + @" | |__| |  __/  | |  | || |  | || | / /_ | |___|  _ < " + C_RST);
            Console.WriteLine(C_B + @"  \____/|_|     |_| |___|_|  |_|___/____||_____|_| \_\" + C_RST);
            Console.WriteLine(C_G + "  --------------------------------------------------------" + C_RST);
            Console.WriteLine(C_W + "            " + subTitle + "            " + C_RST);
            Console.WriteLine(C_G + "  --------------------------------------------------------" + C_RST);
            Console.WriteLine(C_G + "  [STATUS] Data: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            Console.WriteLine("  [STATUS] Host: " + Environment.MachineName + " | Usuário: " + Environment.UserName);
            if (!string.IsNullOrWhiteSpace(osCaption))
            {
                Console.WriteLine("  [STATUS] OS: " + osCaption);
            }
            Console.WriteLine(C_G + "  --------------------------------------------------------" + C_RST);
            Console.WriteLine();
        }

        public static void WriteLogEntry(string level, string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            switch (level)
            {
                case "ETAPA":
                    Console.WriteLine(string.Format("\n{0} >> [{1}] {2}{3}", C_C, time, message, C_RST));
                    break;
                case "OK":
                    Console.WriteLine(string.Format("{0}    [ OK ] {1}{2}", C_GN, message, C_RST));
                    break;
                case "AVISO":
                    Console.WriteLine(string.Format("{0}    [AVISO] {1}{2}", C_Y, message, C_RST));
                    break;
                case "ERRO":
                    Console.WriteLine(string.Format("{0}    [ERRO] {1}{2}", C_R, message, C_RST));
                    break;
                default:
                    Console.WriteLine(string.Format("{0}    [INFO] {1}{2}", C_G, message, C_RST));
                    break;
            }
        }

        public static void WriteProgress(int current, int total, string activity)
        {
            if (total <= 0) return;
            int pct = (int)Math.Round(((double)current / total) * 100.0);
            try
            {
                Console.Title = string.Format("Otimizador de Windows - {0} ({1}/{2} - {3}%)", activity, current, total, pct);
            }
            catch
            {
            }
        }

        public static void ShowExecutionSummary(ExecutionResult result, string logPath)
        {
            Console.WriteLine();
            Console.WriteLine(C_G + "  --------------------------------------------------------" + C_RST);
            if (result.DryRun)
            {
                Console.WriteLine(string.Format("{0}  SIMULAÇÃO CONCLUÍDA: {1} etapas seriam executadas (nada foi alterado).{2}",
                    C_C, result.Simulated, C_RST));
            }
            else
            {
                string color = result.Failed > 0 ? C_R : (result.Warnings > 0 ? C_Y : C_GN);
                Console.WriteLine(string.Format("{0}  RESULTADO: {1} OK | {2} com aviso | {3} com erro{4}",
                    color, result.Ok, result.Warnings, result.Failed, C_RST));
                foreach (StepResult step in result.Steps)
                {
                    if (step.Status == "ERRO")
                    {
                        Console.WriteLine(string.Format("{0}   - {1}: {2}{3}", C_R, step.Name, step.Detail, C_RST));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(logPath))
            {
                Console.WriteLine(string.Format("{0}  Log detalhado: {1}{2}", C_C, logPath, C_RST));
            }
            Console.WriteLine(C_G + "  --------------------------------------------------------" + C_RST);
        }

        public static bool ReadYesNo(string prompt)
        {
            Console.Write(string.Format("{0} [?] {1} (S/N): {2}", C_C, prompt, C_RST));
            string answer = Console.ReadLine() ?? string.Empty;
            string trimmed = answer.Trim();
            return trimmed.Equals("S", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("Y", StringComparison.OrdinalIgnoreCase);
        }

        public static string PromptInput(string prompt)
        {
            Console.Write(prompt);
            return (Console.ReadLine() ?? string.Empty).Trim();
        }
    }
}
