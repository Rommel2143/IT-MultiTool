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
    private readonly CheckedListBox _categories = new()
    {
        Size = new Size(380, 170),
        CheckOnClick = true,
        IntegralHeight = false,
        Margin = new Padding(0, 0, 8, 0),
    };
    private readonly Label _categoryDetails = Ui.Note("Select a list to see which sites it contains.");

    public WebsitePanel()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);
        AutoScroll = true;
        _list.Height = 160;

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
            "Sites are blocked in the Windows hosts file and in the Edge, Chrome, Brave and Firefox blocklist policies. " +
            "Separate several sites with commas or spaces. Edge and Chrome apply changes within a few seconds (reload open tabs); " +
            "Firefox needs a restart. Browsers will show \"managed by your organization\" while sites are blocked."));
        group.Controls.Add(layout);

        _categories.Items.AddRange([.. SiteCategories.All]);
        _categories.SelectedIndexChanged += (_, _) => ShowCategoryDetails();

        var presetGroup = new GroupBox { Text = "Non-work website lists", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var presetLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        var presetButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        foreach (var button in new[]
                 {
                     Ui.Button("Block checked", OnBlockCategories, 140),
                     Ui.Button("Unblock checked", OnUnblockCategories, 140),
                 })
        {
            button.Margin = new Padding(0, 0, 0, 8);
            presetButtons.Controls.Add(button);
        }
        presetLayout.Controls.Add(Ui.Row(_categories, presetButtons));
        presetLayout.Controls.Add(_categoryDetails);
        presetGroup.Controls.Add(presetLayout);

        // Dock=Top stacks in reverse order of addition.
        Controls.Add(presetGroup);
        Controls.Add(new Panel { Height = 12, Dock = DockStyle.Top });
        Controls.Add(group);

        Ui.TryRun(this, WebsiteBlockerService.UpgradeEntries);
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

    private List<SiteCategory> CheckedCategories() => _categories.CheckedItems.Cast<SiteCategory>().ToList();

    private void OnBlockCategories(object? sender, EventArgs e)
    {
        var checkedLists = CheckedCategories();
        if (checkedLists.Count == 0)
        {
            MessageBox.Show(this, "Tick one or more lists first.", "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        IReadOnlyList<string> added = [];
        if (Ui.TryRun(this, () => added = WebsiteBlockerService.Block(checkedLists.SelectMany(c => c.Domains))))
        {
            MessageBox.Show(this, added.Count == 0 ? "All of these sites were already blocked." : $"Blocked {added.Count} new sites.",
                "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        RefreshList();
    }

    private void OnUnblockCategories(object? sender, EventArgs e)
    {
        var checkedLists = CheckedCategories();
        if (checkedLists.Count == 0)
            return;

        Ui.TryRun(this, () => WebsiteBlockerService.Unblock(checkedLists.SelectMany(c => c.Domains)));
        RefreshList();
    }

    private void ShowCategoryDetails()
    {
        if (_categories.SelectedItem is not SiteCategory category)
            return;

        var note = category.Note.Length > 0 ? category.Note + "\n" : "";
        _categoryDetails.Text = note + string.Join(", ", category.Domains);
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
