using ITMultiTool.Services;

namespace ITMultiTool.Controls;

public sealed class UsbPanel : UserControl
{
    private readonly Label _status = Ui.StatusLabel();

    public UsbPanel()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        var group = new GroupBox { Text = "USB storage devices", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        layout.Controls.Add(_status);
        layout.Controls.Add(Ui.Row(Ui.Button("Lock USB storage", OnLock, 150), Ui.Button("Unlock USB storage", OnUnlock, 150)));
        layout.Controls.Add(Ui.Note(
            "Locking disables the USB mass-storage driver and denies read/write access to removable disks. " +
            "Keyboards, mice, printers and other non-storage USB devices keep working. " +
            "Drives that are already plugged in should be unplugged and reconnected for the change to apply."));
        group.Controls.Add(layout);
        Controls.Add(group);

        RefreshStatus();
    }

    private void OnLock(object? sender, EventArgs e)
    {
        Ui.TryRun(this, UsbStorageService.Lock);
        RefreshStatus();
    }

    private void OnUnlock(object? sender, EventArgs e)
    {
        Ui.TryRun(this, UsbStorageService.Unlock);
        RefreshStatus();
    }

    public void RefreshStatus() =>
        Ui.SetStatus(_status, UsbStorageService.IsLocked(), "Status: USB storage LOCKED", "Status: USB storage allowed");
}
