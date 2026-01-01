using System.Drawing;
using System.Windows.Forms;

namespace TabletDeck;

/// <summary>Rename tile dialog.</summary>
public sealed class RenameTileDialog : LocalizedForm
{
    private readonly Label _label = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly TextBox _tb = new() { Dock = DockStyle.Fill };

    public string NewLabel => _tb.Text.Trim();

    public RenameTileDialog(string currentLabel)
    {
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        Padding = new Padding(12);

        _tb.Text = currentLabel;

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var ok = new Button { AutoSize = true };
        var cancel = new Button { AutoSize = true };

        ok.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        grid.Controls.Add(_label, 0, 0);
        grid.Controls.Add(_tb, 1, 0);

        grid.Controls.Add(buttons, 0, 1);
        grid.SetColumnSpan(buttons, 2);

        Controls.Add(grid);

        // Store for localization
        _ok = ok;
        _cancel = cancel;

        ApplyLocalization();
    }

    private readonly Button _ok;
    private readonly Button _cancel;

    protected override void ApplyLocalization()
    {
        Text = Localization.T("win.rename.title");
        _label.Text = Localization.T("win.rename.name");
        _ok.Text = Localization.T("win.common.save");
        _cancel.Text = Localization.T("win.common.cancel");
    }
}
