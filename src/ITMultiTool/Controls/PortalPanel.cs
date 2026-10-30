using System.Diagnostics;
using ITMultiTool.Services;

namespace ITMultiTool.Controls;

public sealed class PortalPanel : UserControl
{
    private readonly Label _status = Ui.StatusLabel();

    public PortalPanel()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);
        AutoScroll = true;

        var group = new GroupBox { Text = "Company block page", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        layout.Controls.Add(new Label
        {
            Text = $"When a user opens a blocked website, show the {Branding.CompanyName} block page instead of a browser error.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
        });
        layout.Controls.Add(_status);
        layout.Controls.Add(Ui.Row(
            Ui.Button("Install / Update", OnInstall, 140),
            Ui.Button("Remove", OnRemove, 120),
            Ui.Button("Preview page", OnPreview, 120)));
        layout.Controls.Add(Ui.Note(
            "Install copies IT MultiTool to Program Files and starts a background task (runs at every startup) that answers " +
            "requests for blocked sites. It also creates a certificate trusted only on this PC so HTTPS sites can show the page, " +
            "and switches Edge, Chrome and Brave from their own block page to this one. Restart open browsers after installing.\n\n" +
            "Remove undoes all of this, and browsers go back to showing their own block page."));
        group.Controls.Add(layout);
        Controls.Add(group);

        RefreshStatus();
    }

    private void OnInstall(object? sender, EventArgs e)
    {
        UseWaitCursor = true;
        var ok = Ui.TryRun(this, PortalService.Install);
        UseWaitCursor = false;

        if (ok)
        {
            // Give the task a moment to open its ports.
            Thread.Sleep(1500);
            MessageBox.Show(this, PortalService.IsRunning()
                    ? "Block page installed and running. Restart open browsers."
                    : "Block page installed, but it is not answering yet. If this persists, check that no other program uses port 80/443 " +
                      @"and see C:\ProgramData\ITMultiTool\portal.log.",
                "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        RefreshStatus();
    }

    private void OnRemove(object? sender, EventArgs e)
    {
        if (MessageBox.Show(this, "Remove the block page? Blocked sites stay blocked, but browsers will show their own error page.",
                "IT MultiTool", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        UseWaitCursor = true;
        Ui.TryRun(this, PortalService.Uninstall);
        UseWaitCursor = false;
        RefreshStatus();
    }

    private void OnPreview(object? sender, EventArgs e)
    {
        if (!PortalService.IsRunning())
        {
            MessageBox.Show(this, "The block page is not running. Install it first.", "IT MultiTool",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("http://127.0.0.1/") { UseShellExecute = true });
    }

    public void RefreshStatus()
    {
        var installed = PortalService.IsInstalled();
        var running = installed && PortalService.IsRunning();
        _status.Text = !installed ? "Status: Not installed" : running ? "Status: Installed and running" : "Status: Installed, NOT running";
        _status.ForeColor = running ? Ui.OkColor : installed ? Ui.LockedColor : SystemColors.GrayText;
    }
}
