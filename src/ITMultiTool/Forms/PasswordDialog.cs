using ITMultiTool.Services;

namespace ITMultiTool.Forms;

public enum PasswordDialogMode
{
    /// <summary>First run: create the password.</summary>
    Setup,
    /// <summary>Startup: enter the password.</summary>
    Login,
    /// <summary>Change an existing password.</summary>
    Change,
}

public sealed class PasswordDialog : Form
{
    private const int MaxAttempts = 5;

    private readonly PasswordDialogMode _mode;
    private readonly TextBox _current = PasswordBox();
    private readonly TextBox _new = PasswordBox();
    private readonly TextBox _confirm = PasswordBox();
    private readonly Label _error = new()
    {
        AutoSize = true,
        MaximumSize = new Size(320, 0),
        ForeColor = Color.FromArgb(192, 0, 0),
        Margin = new Padding(0, 6, 0, 0),
    };
    private int _failedAttempts;

    public PasswordDialog(PasswordDialogMode mode)
    {
        _mode = mode;

        Text = mode switch
        {
            PasswordDialogMode.Setup => "Create administrator password",
            PasswordDialogMode.Change => "Change administrator password",
            _ => "IT MultiTool - Sign in",
        };
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = mode != PasswordDialogMode.Change;
        StartPosition = mode == PasswordDialogMode.Change ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont ?? Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(24, 20, 24, 20);
        BackColor = Color.White;

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
        };

        layout.Controls.Add(new PictureBox
        {
            Image = Branding.LoadLogo(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(320, 64),
            Margin = new Padding(0, 0, 0, 6),
        });
        layout.Controls.Add(new Label
        {
            Text = Branding.CompanyName,
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            ForeColor = Branding.Blue,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(320, 26),
        });
        layout.Controls.Add(new Label
        {
            Text = mode switch
            {
                PasswordDialogMode.Setup => "Create the password that will be required to open IT MultiTool. " +
                                            $"Use at least {PasswordService.MinimumLength} characters with letters and a number or symbol.",
                PasswordDialogMode.Change => "Enter the current password, then choose a new one.",
                _ => "Enter the administrator password to continue.",
            },
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 4, 0, 12),
        });

        if (mode != PasswordDialogMode.Setup)
            AddField(layout, mode == PasswordDialogMode.Login ? "Password" : "Current password", _current);
        if (mode != PasswordDialogMode.Login)
        {
            AddField(layout, "New password", _new);
            AddField(layout, "Confirm new password", _confirm);
        }
        layout.Controls.Add(_error);

        var ok = new Button
        {
            Text = mode == PasswordDialogMode.Login ? "Sign in" : "Save",
            Size = new Size(100, 32),
            BackColor = Branding.Blue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 8, 0),
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += OnOk;

        var cancel = new Button
        {
            Text = mode == PasswordDialogMode.Change ? "Cancel" : "Exit",
            Size = new Size(100, 32),
            DialogResult = DialogResult.Cancel,
            Margin = Padding.Empty,
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Size = new Size(320, 36),
            Margin = new Padding(0, 14, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        (_mode == PasswordDialogMode.Setup ? _new : _current).Focus();
    }

    private void OnOk(object? sender, EventArgs e)
    {
        _error.Text = "";

        if (_mode != PasswordDialogMode.Setup && !CheckCurrentPassword())
            return;

        if (_mode == PasswordDialogMode.Login)
        {
            DialogResult = DialogResult.OK;
            return;
        }

        var problem = PasswordService.Validate(_new.Text);
        if (problem is null && _new.Text != _confirm.Text)
            problem = "The new passwords do not match.";
        if (problem is null && _mode == PasswordDialogMode.Change && _new.Text == _current.Text)
            problem = "The new password must be different from the current one.";

        if (problem is not null)
        {
            ShowError(problem, _new);
            return;
        }

        try
        {
            PasswordService.SetPassword(_new.Text);
        }
        catch (UnauthorizedAccessException)
        {
            ShowError("Access denied. Run IT MultiTool as administrator.", _new);
            return;
        }

        if (_mode == PasswordDialogMode.Change)
            MessageBox.Show(this, "Password changed.", "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
    }

    private bool CheckCurrentPassword()
    {
        UseWaitCursor = true;
        var valid = PasswordService.Verify(_current.Text);
        UseWaitCursor = false;
        if (valid)
            return true;

        _failedAttempts++;
        if (_failedAttempts >= MaxAttempts)
        {
            MessageBox.Show(this, "Too many incorrect attempts.", "IT MultiTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Abort;
            return false;
        }

        // Slow down guessing a little more with each wrong attempt.
        Thread.Sleep(500 * _failedAttempts);
        ShowError($"Incorrect password. {MaxAttempts - _failedAttempts} attempt(s) left.", _current);
        return false;
    }

    private void ShowError(string message, TextBox focus)
    {
        _error.Text = message;
        focus.SelectAll();
        focus.Focus();
    }

    private static void AddField(FlowLayoutPanel layout, string label, TextBox box)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 4, 0, 2) });
        layout.Controls.Add(box);
    }

    private static TextBox PasswordBox() => new()
    {
        UseSystemPasswordChar = true,
        Width = 320,
        Margin = new Padding(0, 0, 0, 6),
    };
}
