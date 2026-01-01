using System;
using System.Drawing;
using System.Windows.Forms;

namespace TabletDeck;

public sealed class ObsSettingsForm : LocalizedForm
{
    private readonly TextBox _host = new() { Width = 240 };
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Width = 120 };
    private readonly TextBox _password = new() { Width = 240, UseSystemPasswordChar = true };

    private readonly CheckBox _enabled = new();
    private readonly Button _save = new() { AutoSize = true };
    private readonly Button _cancel = new() { AutoSize = true };

    private readonly Label _lblHost = new() { AutoSize = true };
    private readonly Label _lblPort = new() { AutoSize = true };
    private readonly Label _lblPassword = new() { AutoSize = true };

    public ObsSettingsForm()
    {
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(12);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 5
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        grid.Controls.Add(_enabled, 0, 0);
        grid.SetColumnSpan(_enabled, 2);

        grid.Controls.Add(_lblHost, 0, 1);
        grid.Controls.Add(_host, 1, 1);

        grid.Controls.Add(_lblPort, 0, 2);
        grid.Controls.Add(_port, 1, 2);

        grid.Controls.Add(_lblPassword, 0, 3);
        grid.Controls.Add(_password, 1, 3);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        buttons.Controls.Add(_save);
        buttons.Controls.Add(_cancel);
        grid.Controls.Add(buttons, 0, 4);
        grid.SetColumnSpan(buttons, 2);

        _save.Click += (_, __) => SaveAndClose();
        _cancel.Click += (_, __) => Close();

        Controls.Add(grid);

        var cur = ObsSettingsStore.Load();
        _host.Text = cur.Host ?? "127.0.0.1";
        _port.Value = cur.Port <= 0 ? 4455 : cur.Port;
        _password.Text = cur.Password ?? "";
        _enabled.Checked = cur.Enabled;

        ApplyLocalization();
    }

    protected override void ApplyLocalization()
    {
        Text = Localization.T("win.obs.title");
        _enabled.Text = Localization.T("win.obs.enabled");
        _save.Text = Localization.T("win.common.save");
        _cancel.Text = Localization.T("win.common.cancel");

        _lblHost.Text = Localization.T("win.obs.host");
        _lblPort.Text = Localization.T("win.obs.port");
        _lblPassword.Text = Localization.T("win.obs.password");
    }

    private void SaveAndClose()
    {
        var s = new ObsSettings(
            Host: _host.Text.Trim().Length == 0 ? "127.0.0.1" : _host.Text.Trim(),
            Port: (int)_port.Value,
            Password: string.IsNullOrWhiteSpace(_password.Text) ? null : _password.Text,
            Enabled: _enabled.Checked
        );

        ObsSettingsStore.Save(s);
        Close();
    }
}
