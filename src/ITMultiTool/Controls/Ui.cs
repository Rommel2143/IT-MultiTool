namespace ITMultiTool.Controls;

internal static class Ui
{
    public static readonly Color OkColor = Color.FromArgb(0, 128, 0);
    public static readonly Color LockedColor = Color.FromArgb(192, 0, 0);

    /// <summary>Runs an action and shows any failure in a message box. Returns true on success.</summary>
    public static bool TryRun(IWin32Window owner, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(owner, ex.Message, "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(owner, "Access denied. Run IT MultiTool as administrator.", "IT MultiTool",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, ex.Message, "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return false;
    }

    public static Button Button(string text, EventHandler onClick, int width = 120)
    {
        var button = new Button { Text = text, Width = width, Height = 32, Margin = new Padding(0, 0, 8, 0) };
        button.Click += onClick;
        return button;
    }

    public static Label StatusLabel() => new()
    {
        AutoSize = true,
        Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 11f, FontStyle.Bold),
        Margin = new Padding(0, 4, 0, 8),
    };

    public static Label Note(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(560, 0),
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 8, 0, 0),
    };

    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 4),
        };
        row.Controls.AddRange(controls);
        return row;
    }

    public static void SetStatus(Label label, bool locked, string lockedText, string unlockedText)
    {
        label.Text = locked ? lockedText : unlockedText;
        label.ForeColor = locked ? LockedColor : OkColor;
    }
}
