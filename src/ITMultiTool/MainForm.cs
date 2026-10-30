using System.Security.Principal;
using ITMultiTool.Controls;
using ITMultiTool.Forms;
using ITMultiTool.Services;

namespace ITMultiTool;

public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = $"IT MultiTool - {Branding.CompanyName}";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont ?? Font;
        ClientSize = new Size(700, 830);
        MinimumSize = new Size(640, 560);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 6) };
        tabs.TabPages.Add(CreatePage("Wallpaper", new WallpaperPanel()));
        tabs.TabPages.Add(CreatePage("USB Storage", new UsbPanel()));
        tabs.TabPages.Add(CreatePage("Website Blocker", new WebsitePanel()));
        tabs.TabPages.Add(CreatePage("Block Page", new PortalPanel()));

        // Pick up changes made outside the tool whenever a tab is opened.
        tabs.SelectedIndexChanged += (_, _) =>
        {
            switch (tabs.SelectedTab?.Controls[0])
            {
                case WallpaperPanel p: p.RefreshStatus(); break;
                case UsbPanel p: p.RefreshStatus(); break;
                case WebsitePanel p: p.RefreshList(); break;
                case PortalPanel p: p.RefreshStatus(); break;
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

        // Fill must be added before the docked edges so it takes the remaining space.
        Controls.Add(tabs);
        Controls.Add(CreateHeader());
        Controls.Add(status);
    }

    private static Control CreateHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.White, Padding = new Padding(16, 10, 16, 10) };
        var logo = new PictureBox
        {
            Image = Branding.LoadLogo(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Left,
            Width = 82,
        };
        var title = new Label
        {
            Text = Branding.CompanyName,
            Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 14f, FontStyle.Bold),
            ForeColor = Branding.Blue,
            AutoSize = true,
            Location = new Point(110, 12),
        };
        var subtitle = new Label
        {
            Text = "IT MultiTool",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(112, 40),
        };
        var rule = new Panel { Dock = DockStyle.Bottom, Height = 3, BackColor = Branding.Blue };

        var changePassword = new LinkLabel
        {
            Text = "Change password",
            AutoSize = true,
            LinkColor = Branding.Blue,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        changePassword.LinkClicked += (_, _) =>
        {
            using var dialog = new PasswordDialog(PasswordDialogMode.Change);
            dialog.ShowDialog(header.FindForm());
        };
        // Keep the link pinned to the right edge as the window resizes.
        header.Resize += (_, _) => changePassword.Location = new Point(header.ClientSize.Width - changePassword.Width - 16, 26);

        header.Controls.AddRange([title, subtitle, changePassword, logo, rule]);
        return header;
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
