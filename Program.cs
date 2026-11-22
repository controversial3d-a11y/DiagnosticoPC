using System.Windows.Forms;

namespace DiagnosticoPC;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            MessageBox.Show(
                "Se produjo un error inesperado en la interfaz.\r\n\r\n" + e.Exception.Message,
                "Diagnóstico PC",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    "Se produjo un error inesperado.\r\n\r\n" + ex.Message,
                    "Diagnóstico PC",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };
        Application.Run(new MainForm());
    }
}
