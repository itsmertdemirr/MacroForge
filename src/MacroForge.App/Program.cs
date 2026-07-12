namespace MacroForge.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Catch anything that slips past local try/catch blocks (e.g. inside async
        // continuations or event handlers) so the app shows a message instead of
        // the raw .NET JIT-debugger crash dialog.
        Application.ThreadException += (_, e) => ShowFatalError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowFatalError(e.ExceptionObject as Exception ?? new Exception("Unknown fatal error."));

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void ShowFatalError(Exception ex)
    {
        MessageBox.Show(
            $"MacroForge hit an unexpected error and will try to continue:\n\n{ex.Message}",
            "MacroForge — Unexpected error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
