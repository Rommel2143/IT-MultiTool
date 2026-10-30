using ITMultiTool.Forms;
using ITMultiTool.Portal;
using ITMultiTool.Services;

namespace ITMultiTool;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        // Started by the scheduled task: serve the block page with no UI.
        if (args.Contains("--portal", StringComparer.OrdinalIgnoreCase))
            return PortalServer.Run();

        ApplicationConfiguration.Initialize();

        var mode = PasswordService.HasPassword() ? PasswordDialogMode.Login : PasswordDialogMode.Setup;
        using (var dialog = new PasswordDialog(mode))
        {
            if (dialog.ShowDialog() != DialogResult.OK)
                return 1;
        }

        Application.Run(new MainForm());
        return 0;
    }
}
