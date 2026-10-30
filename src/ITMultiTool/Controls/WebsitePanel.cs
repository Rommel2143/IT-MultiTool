using ITMultiTool.Services;

namespace ITMultiTool.Controls;

public sealed class WebsitePanel : UserControl
{
    private readonly TextBox _input = new() { Width = 380, Margin = new Padding(0, 4, 8, 0), PlaceholderText = "e.g. facebook.com, youtube.com" };
    private readonly ListBox _list = new()
    {
        Size = new Size(380, 220),
        SelectionMode = SelectionMode.MultiExtended,
        IntegralHeight = false,
        Margin = new Padding(0, 0, 8, 0),
    };
    private readonly Label _count = new() { AutoSize = true, Margin = new Padding(0, 8, 0, 4) };

    public WebsitePanel()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                OnBlock(this, EventArgs.Empty);
            }
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
                OnUnblock(this, EventArgs.Empty);
        };

        var group = new GroupBox { Text = "Block websites", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        layout.Controls.Add(Ui.Row(_input, Ui.Button("Block", OnBlock, 100)));
        layout.Controls.Add(_count);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        foreach (var button in new[]
                 {
                     Ui.Button("Unblock selected", OnUnblock, 140),
                     Ui.Button("Unblock all", OnUnblockAll, 140),
                     Ui.Button("Refresh", (_, _) => RefreshList(), 140),
                     Ui.Button("Flush DNS cache", OnFlushDns, 140),
                 })
        {
            button.Margin = new Padding(0, 0, 0, 8);
            buttons.Controls.Add(button);
        }
        layout.Controls.Add(Ui.Row(_list, buttons));
        layout.Controls.Add(Ui.Note(
            "Sites are blocked through the Windows hosts file, together with their www. subdomain. " +
            "Separate several sites with commas or spaces. Restart open browsers if a site still loads from cache."));
        group.Controls.Add(layout);
        Controls.Add(group);

        RefreshList();
    }

    private void OnBlock(object? sender, EventArgs e)
    {
        var sites = _input.Text.Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (sites.Length == 0)
            return;

        IReadOnlyList<string> added = [];
        if (Ui.TryRun(this, () => added = WebsiteBlockerService.Block(sites)))
        {
            _input.Clear();
            if (added.Count == 0)
                MessageBox.Show(this, "Already blocked.", "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        RefreshList();
    }

    private void OnUnblock(object? sender, EventArgs e)
    {
        var selected = _list.SelectedItems.Cast<string>().ToList();
        if (selected.Count == 0)
            return;

        Ui.TryRun(this, () => WebsiteBlockerService.Unblock(selected));
        RefreshList();
    }

    private void OnUnblockAll(object? sender, EventArgs e)
    {
        if (_list.Items.Count == 0)
            return;

        if (MessageBox.Show(this, "Unblock all websites?", "IT MultiTool", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        Ui.TryRun(this, WebsiteBlockerService.UnblockAll);
        RefreshList();
    }

    private void OnFlushDns(object? sender, EventArgs e)
    {
        var ok = WebsiteBlockerService.FlushDns();
        MessageBox.Show(this, ok ? "DNS cache flushed." : "Could not flush the DNS cache.", "IT MultiTool",
            MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    public void RefreshList()
    {
        IReadOnlyList<string> sites = [];
        Ui.TryRun(this, () => sites = WebsiteBlockerService.GetBlockedSites());

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var site in sites)
            _list.Items.Add(site);
        _list.EndUpdate();

        _count.Text = sites.Count == 1 ? "1 website blocked" : $"{sites.Count} websites blocked";
    }
}
