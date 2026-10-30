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

        PasswordDialogMode mode;
        try
        {
            mode = PasswordService.HasPassword() ? PasswordDialogMode.Login : PasswordDialogMode.Setup;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show($"Cannot read the password file.\n\n{ex.Message}", "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        using (var dialog = new PasswordDialog(mode))
        {
            if (dialog.ShowDialog() != DialogResult.OK)
                return 1;
        }

        Application.Run(new MainForm());
        return 0;
    }
}
