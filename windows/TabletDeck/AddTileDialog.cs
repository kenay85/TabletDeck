using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;



namespace TabletDeck;

/// <summary>
/// Dialog dodawania kafelka:
/// - wpisujesz nazwę -> podpowiedzi z "zainstalowanych" (Start Menu + App Paths)
/// - wybór z listy -> uzupełnia ID jako run:<ścieżka>
/// - "Wybierz plik..." na wypadek ręcznego wskazania
/// </summary>
public sealed class AddTileDialog : LocalizedForm
{
    private Label? _info;
    private Label? _lblName;
    private Label? _lblActionId;
    private Label? _lblSuggestions;
    private Button? _btnBrowse;
    private Button? _btnOk;
    private Button? _btnCancel;

    private readonly TextBox _tbLabel = new() { Dock = DockStyle.Fill };
    private readonly TextBox _tbActionId = new() { Dock = DockStyle.Fill };

    private readonly ListBox _suggest = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false
    };

    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 250 };
    


    public string TileLabel => _tbLabel.Text.Trim();
    public string ActionId => _tbActionId.Text.Trim();

    public AddTileDialog()
    {
        Text = Localization.T("win.addTile.title");

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        Padding = new Padding(14);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 6
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _info = new Label
        {
            Text = Localization.T("win.addTile.hint"),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };

        _lblName = new Label { Text = Localization.T("win.addTile.name"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 6) };
        _lblActionId = new Label { Text = "ID", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 6) };

        _tbLabel.MinimumSize = new Size(380, 0);
        _tbActionId.MinimumSize = new Size(380, 0);

        _btnBrowse = new Button { Text = "...", AutoSize = true, MinimumSize = new Size(120, 0) };
        _btnBrowse.Click += (_, _) => BrowseForFile();

        _suggest.DisplayMember = nameof(AppCandidate.DisplayName);
        _suggest.DoubleClick += (_, _) => ApplySelectedSuggestion();
        _suggest.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                ApplySelectedSuggestion();
                e.Handled = true;
            }
        };

        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            RefreshSuggestions();
        };

        _tbLabel.TextChanged += (_, _) =>
        {
            // szybka podpowiedź dla znanych nazw -> launch:*
            TryFillKnownLaunch(_tbLabel.Text);

            _debounce.Stop();
            _debounce.Start();
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0)
        };


        _btnOk = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(90, 0) };
        _btnCancel = new Button { Text = "", AutoSize = true, MinimumSize = new Size(90, 0) };

        _btnOk.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(TileLabel))
            {
                MessageBox.Show(this, Localization.T("win.common.nameEmpty"), "TabletDeck",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ActionId))
            {
                MessageBox.Show(this, Localization.T("win.addTile.idHint"), "TabletDeck",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        _btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        buttons.Controls.Add(_btnOk);
        buttons.Controls.Add(_btnCancel);

        // layout
        grid.Controls.Add(_info, 0, 0);
        grid.SetColumnSpan(_info, 3);

        grid.Controls.Add(_lblName, 0, 1);
        grid.Controls.Add(_tbLabel, 1, 1);
        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 2, 1);

        _lblSuggestions = new Label { AutoSize = true, Margin = new Padding(0, 10, 0, 6) };
        grid.Controls.Add(_lblSuggestions, 0, 2);
        grid.SetColumnSpan(grid.GetControlFromPosition(0, 2)!, 3);

        _suggest.MinimumSize = new Size(0, 180);
        grid.Controls.Add(_suggest, 0, 3);
        grid.SetColumnSpan(_suggest, 3);

        grid.Controls.Add(_lblActionId, 0, 4);
        grid.Controls.Add(_tbActionId, 1, 4);
        grid.Controls.Add(_btnBrowse, 2, 4);

        grid.Controls.Add(buttons, 0, 5);
        grid.SetColumnSpan(buttons, 3);

        Controls.Add(grid);

        // wstępne wypełnienie
        RefreshSuggestions();
    }
    protected override void ApplyLocalization()
    {
        // Form title
        Text = Localization.T("win.addTile.title");

        // Optional fields (won't crash if not wired yet)
        if (_info != null) _info.Text = Localization.T("win.addTile.hint");
        if (_lblName != null) _lblName.Text = Localization.T("win.addTile.name");
        if (_lblActionId != null) _lblActionId.Text = "ID";

        if (_lblSuggestions != null) _lblSuggestions.Text = Localization.T("win.addTile.suggestions");

        if (_btnBrowse != null) _btnBrowse.Text = Localization.T("win.addTile.browseFile");
        if (_btnCancel != null) _btnCancel.Text = Localization.T("win.common.cancel");

        // OK zostawiamy "OK" (neutralne językowo, jak w wielu appkach)
        if (_btnOk != null) _btnOk.Text = "OK";
    }
    private void RefreshSuggestions()
    {
        var q = _tbLabel.Text.Trim();
        var hits = AppIndex.Search(q, take: 30).ToList();

        _suggest.BeginUpdate();
        _suggest.Items.Clear();
        foreach (var h in hits) _suggest.Items.Add(h);
        _suggest.EndUpdate();
    }

    private void ApplySelectedSuggestion()
    {
        if (_suggest.SelectedItem is not AppCandidate a) return;

        // run: <lnk/appref-ms/exe> => uruchamiamy dokładnie to, co jest w systemie
        _tbLabel.Text = a.DisplayName;
        _tbActionId.Text = $"run:{a.Target}";
    }

    private void BrowseForFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = Localization.T("win.addTile.choose"),
            Filter = "Aplikacje i skróty (*.exe;*.lnk;*.appref-ms;*.bat;*.cmd)|*.exe;*.lnk;*.appref-ms;*.bat;*.cmd|Wszystkie pliki (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var path = dlg.FileName;
        if (string.IsNullOrWhiteSpace(path)) return;

        if (string.IsNullOrWhiteSpace(_tbLabel.Text))
            _tbLabel.Text = Path.GetFileNameWithoutExtension(path);

        _tbActionId.Text = $"run:{path}";
    }

    private void TryFillKnownLaunch(string name)
    {
        // szybkie mapowanie dla najczęstszych – przydaje się, bo launch:* działa lepiej niż run:* dla systemowych
        var n = (name ?? "").Trim().ToLowerInvariant();
        if (n.Length < 3) return;

        string? id = null;

        if (n.Contains("notat")) id = "launch:notepad";
        else if (n.Contains("kalk")) id = "launch:calc";
        else if (n.Contains("eksplor")) id = "launch:explorer";
        else if (n.Contains("zada")) id = "launch:taskmgr";
        else if (n.Contains("chrome")) id = "launch:chrome";

        // nie nadpisuj jeśli user już coś wpisał ręcznie lub wybrał z listy
        if (!string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(_tbActionId.Text))
            _tbActionId.Text = id;
    }
}
