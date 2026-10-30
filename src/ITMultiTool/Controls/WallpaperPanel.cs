using ITMultiTool.Services;

namespace ITMultiTool.Controls;

public sealed class WallpaperPanel : UserControl
{
    private readonly TextBox _pathBox = new() { Width = 380, Margin = new Padding(0, 4, 8, 0) };
    private readonly ComboBox _styleBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly PictureBox _preview = new()
    {
        Size = new Size(320, 180),
        SizeMode = PictureBoxSizeMode.Zoom,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.Black,
        Margin = new Padding(0, 8, 0, 8),
    };
    private readonly Label _lockStatus = Ui.StatusLabel();

    public WallpaperPanel()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        _styleBox.Items.AddRange(Enum.GetNames<WallpaperStyle>());
        _styleBox.SelectedItem = WallpaperService.GetCurrentStyle().ToString();
        _pathBox.TextChanged += (_, _) => LoadPreview(_pathBox.Text);

        var changeGroup = new GroupBox { Text = "Change wallpaper", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var changeLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        changeLayout.Controls.Add(Ui.Row(_pathBox, Ui.Button("Browse...", OnBrowse, 100)));
        changeLayout.Controls.Add(Ui.Row(new Label { Text = "Style:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, _styleBox));
        changeLayout.Controls.Add(_preview);
        changeLayout.Controls.Add(Ui.Row(Ui.Button("Apply wallpaper", OnApply, 140)));
        changeGroup.Controls.Add(changeLayout);

        var lockGroup = new GroupBox { Text = "Wallpaper lock", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var lockLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        lockLayout.Controls.Add(_lockStatus);
        lockLayout.Controls.Add(Ui.Row(Ui.Button("Lock wallpaper", OnLock), Ui.Button("Unlock wallpaper", OnUnlock)));
        lockLayout.Controls.Add(Ui.Note(
            "Locking stops users from changing the desktop background and pins the current wallpaper. " +
            "Users may need to sign out and back in for the lock to apply everywhere."));
        lockGroup.Controls.Add(lockLayout);

        // Dock=Top stacks in reverse order of addition.
        Controls.Add(lockGroup);
        Controls.Add(new Panel { Height = 12, Dock = DockStyle.Top });
        Controls.Add(changeGroup);

        var current = WallpaperService.GetCurrentWallpaper();
        if (current is not null && File.Exists(current))
            _pathBox.Text = current;
        RefreshStatus();
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        var patterns = string.Join(";", WallpaperService.SupportedExtensions.Select(x => "*" + x));
        using var dialog = new OpenFileDialog
        {
            Title = "Select wallpaper image",
            Filter = $"Images ({patterns})|{patterns}|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _pathBox.Text = dialog.FileName;
    }

    private void OnApply(object? sender, EventArgs e)
    {
        var style = Enum.Parse<WallpaperStyle>((string)_styleBox.SelectedItem!);
        if (Ui.TryRun(this, () => WallpaperService.SetWallpaper(_pathBox.Text.Trim(), style)))
            MessageBox.Show(this, "Wallpaper applied.", "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshStatus();
    }

    private void OnLock(object? sender, EventArgs e)
    {
        Ui.TryRun(this, WallpaperService.Lock);
        RefreshStatus();
    }

    private void OnUnlock(object? sender, EventArgs e)
    {
        Ui.TryRun(this, WallpaperService.Unlock);
        RefreshStatus();
    }

    public void RefreshStatus() =>
        Ui.SetStatus(_lockStatus, WallpaperService.IsLocked(), "Status: LOCKED", "Status: Unlocked");

    private void LoadPreview(string path)
    {
        var old = _preview.Image;
        _preview.Image = null;
        old?.Dispose();

        if (!File.Exists(path))
            return;

        try
        {
            // Load from a copy so the file isn't kept locked while previewed.
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream);
            _preview.Image = new Bitmap(image);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or OutOfMemoryException)
        {
            // Not a readable image; leave the preview empty.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _preview.Image?.Dispose();
        base.Dispose(disposing);
    }
}
