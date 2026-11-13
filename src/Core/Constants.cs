using System;
using System.IO;

namespace Otimizador.Core
{
    public static class Constants
    {
        public const string OptimizerVersion = "15.1.3";

        public static string OptimizerDataRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "OtimizadorWindows");
            }
        }

        public static string OptimizerDryRunRoot
        {
            get { return Path.Combine(Path.GetTempPath(), "OtimizadorWindows-simulacao"); }
        }
    }

    public static class ExitCodes
    {
        public const int Sucesso = 0;
        public const int ErroDeUso = 1;
        public const int FalhasParciais = 2;
        public const int AbortadoSemBackup = 3;
    }
}
