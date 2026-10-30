using System.Security.Principal;
using ITMultiTool.Controls;

namespace ITMultiTool;

public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "IT MultiTool";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont ?? Font;
        ClientSize = new Size(700, 640);
        MinimumSize = new Size(640, 560);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 6) };
        tabs.TabPages.Add(CreatePage("Wallpaper", new WallpaperPanel()));
        tabs.TabPages.Add(CreatePage("USB Storage", new UsbPanel()));
        tabs.TabPages.Add(CreatePage("Website Blocker", new WebsitePanel()));

        // Pick up changes made outside the tool whenever a tab is opened.
        tabs.SelectedIndexChanged += (_, _) =>
        {
            switch (tabs.SelectedTab?.Controls[0])
            {
                case WallpaperPanel p: p.RefreshStatus(); break;
                case UsbPanel p: p.RefreshStatus(); break;
                case WebsitePanel p: p.RefreshList(); break;
            }
        };

        var status = new StatusStrip();
        var isAdmin = IsAdministrator();
        status.Items.Add(new ToolStripStatusLabel(isAdmin
            ? $"Running as administrator on {Environment.MachineName}"
            : "Not running as administrator - changes will fail")
        {
            ForeColor = isAdmin ? SystemColors.ControlText : Ui.LockedColor,
        });

        Controls.Add(tabs);
        Controls.Add(status);
    }

    private static TabPage CreatePage(string title, Control content)
    {
        var page = new TabPage(title) { UseVisualStyleBackColor = true };
        page.Controls.Add(content);
        return page;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
