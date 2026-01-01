using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TabletDeck;

public sealed class TileEditorForm : LocalizedForm
{
    private AppConfig _cfg;
    private readonly WsServer? _server;

    private Label? _lblScreen;
    private Label? _lblRows;
    private Label? _lblCols;
    private Label? _lblTileHeight;
    private Label? _lblIconSize;
    private Label? _lblHint;

    private readonly ComboBox _profiles = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _addProfile = new();
    private readonly Button _dupProfile = new();
    private readonly Button _renameProfile = new();
    private readonly Button _removeProfile = new();

    private readonly TextBox _filter = new();
    private readonly ListBox _catalog = new();
    private readonly Button _addAction = new();

    private readonly NumericUpDown _tileHeightDp = new() { Minimum = 60, Maximum = 320, Value = 106, Width = 70 };
    private readonly NumericUpDown _iconSizeDp = new() { Minimum = 16, Maximum = 256, Value = 82, Width = 70 };

    private readonly NumericUpDown _rows = new() { Minimum = 1, Maximum = 12, Value = 4, Width = 60 };
    private readonly NumericUpDown _cols = new() { Minimum = 1, Maximum = 12, Value = 5, Width = 60 };

    private readonly TableLayoutPanel _grid = new()
    {
        Dock = DockStyle.Fill,
        CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        BackColor = Color.Black
    };

    private bool _loadingUi;

    public TileEditorForm(WsServer? server = null)
    {
        _server = server;
Text = Localization.T("win.editor.title");
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;

        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(900, 600);

        _cfg = AppConfigStore.LoadOrCreate();

        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 420,
            FixedPanel = FixedPanel.Panel1
        };

        root.Panel1.Controls.Add(BuildLeft());
        root.Panel2.Controls.Add(BuildRight());

        Controls.Add(root);

        root.Panel1MinSize = 320;
        Shown += (_, __) => root.SplitterDistance = 420;

        LoadProfilesToUi();
        RefreshCatalogList();
        ReloadGridFromActiveProfile();
    }

    private Control BuildLeft()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _filter.Dock = DockStyle.Fill;
        _filter.Margin = new Padding(0, 0, 8, 0);
        _filter.TextChanged += (_, _) => RefreshCatalogList();

        _addAction.AutoSize = true;
        _addAction.MinimumSize = new Size(110, 32);
        _addAction.Click += (_, _) => AddActionFromDialog();

        topBar.Controls.Add(_filter, 0, 0);
        topBar.Controls.Add(_addAction, 1, 0);

        _catalog.Dock = DockStyle.Fill;
        _catalog.IntegralHeight = false;

        _catalog.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;

            var idx = _catalog.IndexFromPoint(e.Location);
            if (idx < 0) return;

            _catalog.SelectedIndex = idx;
            if (_catalog.SelectedItem is not ActionItem ai) return;

            _catalog.DoDragDrop(ai.Id, DragDropEffects.Copy);
        };

        _catalog.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;

            var menu = new ContextMenuStrip();
            menu.Items.Add(Localization.T("win.editor.addProgram") + "...", null, (_, _) => AddActionFromDialog());

            var idx = _catalog.IndexFromPoint(e.Location);
            if (idx >= 0)
            {
                _catalog.SelectedIndex = idx;
                if (_catalog.SelectedItem is ActionItem ai)
                {
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add(Localization.T("win.editor.rename") + "...", null, (_, _) => RenameCatalogItem(ai));
                    menu.Items.Add(Localization.T("win.editor.removeFromCatalog"), null, (_, _) => RemoveCatalogItem(ai));
                }
            }

            menu.Show(_catalog, e.Location);
        };

        _lblHint = new Label
        {
            Dock = DockStyle.Top,
            Text = "Przeciągnij akcję na kafelek.\nPPM na kafelku: wyczyść.",
            AutoSize = true
        };

        panel.Controls.Add(topBar, 0, 0);
        panel.Controls.Add(_catalog, 0, 1);
        panel.Controls.Add(_lblHint, 0, 2);

        return panel;
    }

    private Control BuildRight()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var topHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Height = 100
        };

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        topHost.Controls.Add(top);

        foreach (var b in new[] { _addProfile, _dupProfile, _renameProfile, _removeProfile })
        {
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Padding = new Padding(10, 6, 10, 6);
            b.Margin = new Padding(6, 3, 6, 3);
            b.MinimumSize = new Size(0, 32);
        }

        _profiles.Margin = new Padding(6, 3, 6, 3);
        _rows.Margin = new Padding(6, 3, 6, 3);
        _cols.Margin = new Padding(6, 3, 6, 3);
        _tileHeightDp.Margin = new Padding(6, 3, 6, 3);
        _iconSizeDp.Margin = new Padding(6, 3, 6, 3);

        _profiles.MinimumSize = new Size(180, 32);
        _rows.MinimumSize = new Size(60, 32);
        _cols.MinimumSize = new Size(60, 32);

        _profiles.SelectedIndexChanged += (_, _) =>
        {
            if (_profiles.SelectedItem is ProfileLayout p)
            {
                _cfg = SetActiveProfile(_cfg, p.Id);
                SaveConfigToDisk();

                ReloadGridFromActiveProfile();
            }
        };

        _addProfile.Click += (_, _) => AddProfile();
        _dupProfile.Click += (_, _) => DuplicateProfile();
        _renameProfile.Click += (_, _) => RenameProfile();
        _removeProfile.Click += (_, _) => RemoveProfile();

        _rows.ValueChanged += (_, _) => { if (!_loadingUi) ResizeActive((int)_rows.Value, (int)_cols.Value); };
        _cols.ValueChanged += (_, _) => { if (!_loadingUi) ResizeActive((int)_rows.Value, (int)_cols.Value); };

        _tileHeightDp.ValueChanged += (_, _) => { if (!_loadingUi) SaveTileStyle(); };
        _iconSizeDp.ValueChanged += (_, _) => { if (!_loadingUi) SaveTileStyle(); };

        _lblScreen = new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        top.Controls.Add(_lblScreen);
        top.Controls.Add(_profiles);
        top.Controls.Add(_addProfile);
        top.Controls.Add(_dupProfile);
        top.Controls.Add(_renameProfile);
        top.Controls.Add(_removeProfile);

        _lblRows = new Label { AutoSize = true, Padding = new Padding(16, 6, 0, 0) };
        top.Controls.Add(_lblRows);
        top.Controls.Add(_rows);
        _lblCols = new Label { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
        top.Controls.Add(_lblCols);
        top.Controls.Add(_cols);

        _lblTileHeight = new Label { AutoSize = true, Padding = new Padding(16, 6, 0, 0) };
        top.Controls.Add(_lblTileHeight);
        top.Controls.Add(_tileHeightDp);
        _lblIconSize = new Label { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
        top.Controls.Add(_lblIconSize);
        top.Controls.Add(_iconSizeDp);

        panel.Controls.Add(topHost, 0, 0);
        panel.Controls.Add(_grid, 0, 1);
        return panel;
    }

    private void SaveTileStyle()
    {
        if (_loadingUi) return;

        var p = GetActiveProfile();
        if (p is null) return;

        var updated = p with
        {
            TileHeightDp = (int)_tileHeightDp.Value,
            IconSizeDp = (int)_iconSizeDp.Value
        };

        _cfg = UpsertProfile(_cfg, updated);
        _cfg = SetActiveProfile(_cfg, updated.Id);
        SaveConfigToDisk();

    }


    private void SaveConfigToDisk()
    {
        AppConfigStore.Save(_cfg);          // <-- zapis do %AppData%\TabletDeck\config.json
        _server?.BroadcastLayoutNow();      // <-- natychmiastowy push układu na tablet (bez reconnecta)
    }

    private void LoadProfilesToUi()
    {
        _cfg = AppConfigStore.LoadOrCreate();
        _cfg = SetActiveProfile(_cfg, _cfg.ActiveProfileId ?? "");

        var profiles = _cfg.Profiles ?? new List<ProfileLayout>();
        _profiles.Items.Clear();
        foreach (var p in profiles) _profiles.Items.Add(p);

        _profiles.DisplayMember = nameof(ProfileLayout.Name);

        var activeIdx = profiles.FindIndex(p => p.Id == _cfg.ActiveProfileId);
        _profiles.SelectedIndex = activeIdx >= 0 ? activeIdx : (profiles.Count > 0 ? 0 : -1);
    }

    private void RefreshCatalogList()
    {
        var filter = _filter.Text.Trim();
        _catalog.Items.Clear();

        IEnumerable<ActionItem> items = _cfg.Catalog;
        if (!string.IsNullOrWhiteSpace(filter))
            items = items.Where(a => a.Label.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var a in items.OrderBy(a => a.Label))
            _catalog.Items.Add(a);

        _catalog.DisplayMember = nameof(ActionItem.Label);
    }

    private ProfileLayout? GetActiveProfile()
    {
        var profiles = _cfg.Profiles;
        if (profiles is null || profiles.Count == 0) return null;
        var id = _cfg.ActiveProfileId;
        return profiles.FirstOrDefault(p => p.Id == id) ?? profiles[0];
    }

    private void ReloadGridFromActiveProfile()
    {
        var p = GetActiveProfile();
        if (p is null) return;

        _loadingUi = true;

        _rows.Value = p.Rows;
        _cols.Value = p.Cols;

        _tileHeightDp.Value = Math.Clamp(p.TileHeightDp, (int)_tileHeightDp.Minimum, (int)_tileHeightDp.Maximum);
        _iconSizeDp.Value = Math.Clamp(p.IconSizeDp, (int)_iconSizeDp.Minimum, (int)_iconSizeDp.Maximum);

        _loadingUi = false;

        BuildGrid(p.Rows, p.Cols);

        for (var i = 0; i < p.Cells.Count; i++)
            SetCellUi(i, p.Cells[i]);
    }

    private void BuildGrid(int rows, int cols)
    {
        _grid.SuspendLayout();
        _grid.Controls.Clear();
        _grid.RowStyles.Clear();
        _grid.ColumnStyles.Clear();

        _grid.RowCount = rows;
        _grid.ColumnCount = cols;

        for (var r = 0; r < rows; r++)
            _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        for (var c = 0; c < cols; c++)
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));

        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                var idx = r * cols + c;

                var cell = new Panel
                {
                    Dock = DockStyle.Fill,
                    AllowDrop = true,
                    BackColor = Color.FromArgb(12, 12, 12),
                    Margin = new Padding(2)
                };

                var text = new Label
                {
                    Dock = DockStyle.Fill,
                    ForeColor = Color.Lime,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold),
                    Tag = idx
                };

                cell.Controls.Add(text);

                cell.DragEnter += (_, e) =>
                {
                    if (e.Data?.GetDataPresent(typeof(string)) == true)
                        e.Effect = DragDropEffects.Copy;
                };

                cell.DragDrop += (_, e) =>
                {
                    var actionId = e.Data?.GetData(typeof(string)) as string;
                    if (string.IsNullOrWhiteSpace(actionId)) return;
                    SetCell(idx, actionId);
                };

                void ShowMenu(Control anchor)
                {
                    var menu = new ContextMenuStrip();
                    menu.Items.Add(Localization.T("win.editor.clearTile"), null, (_, _) => SetCell(idx, null));
                    menu.Show(anchor, anchor.PointToClient(Cursor.Position));
                }

                cell.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowMenu(cell); };
                text.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowMenu(text); };

                _grid.Controls.Add(cell, c, r);
            }

        _grid.ResumeLayout();
    }

    private void SetCell(int idx, string? actionId)
    {
        var p = GetActiveProfile();
        if (p is null) return;

        var cells = p.Cells.ToList();
        if (idx < 0 || idx >= cells.Count) return;

        cells[idx] = actionId;

        var updated = p with { Cells = cells };
        _cfg = UpsertProfile(_cfg, updated);
        _cfg = SetActiveProfile(_cfg, updated.Id);
        SaveConfigToDisk();


        SetCellUi(idx, actionId);
    }

    private void SetCellUi(int idx, string? actionId)
    {
        var cols = (int)_cols.Value;
        var r = idx / cols;
        var c = idx % cols;

        var panel = _grid.GetControlFromPosition(c, r) as Panel;
        var label = panel?.Controls.OfType<Label>().FirstOrDefault();
        if (label is null) return;

        if (string.IsNullOrWhiteSpace(actionId))
        {
            label.Text = "—";
            label.ForeColor = Color.FromArgb(90, 255, 90);
            return;
        }

        var item = _cfg.Catalog.FirstOrDefault(a => a.Id == actionId);
        label.Text = item?.Label ?? actionId;
        label.ForeColor = Color.Lime;
    }

    private void ResizeActive(int rows, int cols)
    {
        var p = GetActiveProfile();
        if (p is null) return;

        rows = Math.Clamp(rows, 1, 12);
        cols = Math.Clamp(cols, 1, 12);

        if (rows == p.Rows && cols == p.Cols) return;

        var newCells = Enumerable.Repeat<string?>(null, rows * cols).ToList();
        var minRows = Math.Min(rows, p.Rows);
        var minCols = Math.Min(cols, p.Cols);

        for (var rr = 0; rr < minRows; rr++)
            for (var cc = 0; cc < minCols; cc++)
                newCells[rr * cols + cc] = p.Cells[rr * p.Cols + cc];

        var updated = p with { Rows = rows, Cols = cols, Cells = newCells };
        _cfg = UpsertProfile(_cfg, updated);
        _cfg = SetActiveProfile(_cfg, updated.Id);
        SaveConfigToDisk();

        LoadProfilesToUi();
        ReloadGridFromActiveProfile();
    }

    private void AddProfile()
    {
        var name = Prompt(Localization.T("win.editor.newScreenName"), Localization.T("win.editor.newScreen"));
        if (string.IsNullOrWhiteSpace(name)) return;

        var p = AppConfigStore.NewProfile(name.Trim(), (int)_rows.Value, (int)_cols.Value);

        _cfg = AppConfigStore.LoadOrCreate();
        _cfg = _cfg with { Profiles = (_cfg.Profiles ?? new List<ProfileLayout>()).Append(p).ToList() };
        _cfg = SetActiveProfile(_cfg, p.Id);
        SaveConfigToDisk();


        LoadProfilesToUi();
        ReloadGridFromActiveProfile();
    }

    private void DuplicateProfile()
    {
        var p = GetActiveProfile();
        if (p is null) return;

        var clone = new ProfileLayout(
            Id: Guid.NewGuid().ToString("N"),
            Name: p.Name + " (kopia)",
            Rows: p.Rows,
            Cols: p.Cols,
            Cells: p.Cells.ToList()
        );

        _cfg = _cfg with { Profiles = (_cfg.Profiles ?? new List<ProfileLayout>()).Append(clone).ToList() };
        _cfg = SetActiveProfile(_cfg, clone.Id);
        SaveConfigToDisk();


        LoadProfilesToUi();
        ReloadGridFromActiveProfile();
    }

    private void RenameProfile()
    {
        var p = GetActiveProfile();
        if (p is null) return;

        var name = Prompt(Localization.T("win.editor.renameScreenName"), p.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        var updated = p with { Name = name.Trim() };
        _cfg = UpsertProfile(_cfg, updated);
        _cfg = SetActiveProfile(_cfg, updated.Id);
        SaveConfigToDisk();


        LoadProfilesToUi();
        ReloadGridFromActiveProfile();
    }

    private void RemoveProfile()
    {
        var profiles = _cfg.Profiles;
        if (profiles is null || profiles.Count <= 1)
        {
            MessageBox.Show(Localization.T("win.editor.needOneScreen"), "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var p = GetActiveProfile();
        if (p is null) return;

        if (MessageBox.Show(Localization.T("win.editor.removeScreenConfirm", p.Name), Localization.T("win.common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var list = profiles.Where(x => x.Id != p.Id).ToList();
        var newActive = list[0];

        _cfg = _cfg with { Profiles = list, ActiveProfileId = newActive.Id };
        SaveConfigToDisk();


        LoadProfilesToUi();
        ReloadGridFromActiveProfile();
    }

    private void AddActionFromDialog()
    {
        using var dlg = new AddTileDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var id = dlg.ActionId.Trim();
        var label = dlg.TileLabel.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label)) return;

        var list = _cfg.Catalog.ToList();
        var i = list.FindIndex(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) list[i] = new ActionItem(id, label);
        else list.Add(new ActionItem(id, label));

        _cfg = _cfg with { Catalog = list };
        SaveConfigToDisk();


        RefreshCatalogList();
        ReloadGridFromActiveProfile();
    }

    private void RenameCatalogItem(ActionItem ai)
    {
        var name = Prompt(Localization.T("win.editor.programName"), ai.Label);
        if (string.IsNullOrWhiteSpace(name)) return;

        var list = _cfg.Catalog.ToList();
        var i = list.FindIndex(a => string.Equals(a.Id, ai.Id, StringComparison.OrdinalIgnoreCase));
        if (i < 0) return;

        list[i] = new ActionItem(ai.Id, name.Trim());
        _cfg = _cfg with { Catalog = list };

        SaveConfigToDisk();

        RefreshCatalogList();
        ReloadGridFromActiveProfile();
    }

    private void RemoveCatalogItem(ActionItem ai)
    {
        if (MessageBox.Show(this,
                $"Usunąć '{ai.Label}' z katalogu?\n\nKafelki używające tej akcji zostaną wyczyszczone.",
                "TabletDeck", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var newCatalog = _cfg.Catalog
            .Where(a => !string.Equals(a.Id, ai.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var profiles = (_cfg.Profiles ?? new List<ProfileLayout>())
            .Select(p => p with
            {
                Cells = p.Cells.Select(x => string.Equals(x, ai.Id, StringComparison.OrdinalIgnoreCase) ? null : x).ToList()
            })
            .ToList();

        _cfg = _cfg with { Catalog = newCatalog, Profiles = profiles };
        _cfg = SetActiveProfile(_cfg, _cfg.ActiveProfileId ?? "");

        SaveConfigToDisk();

        RefreshCatalogList();
        ReloadGridFromActiveProfile();
    }

    private static string? Prompt(string title, string value)
    {
        using var f = new Form
        {
            Text = title,
            Width = 520,
            Height = 200,
            MinimumSize = new Size(520, 200),
            StartPosition = FormStartPosition.CenterParent,
            AutoScaleMode = AutoScaleMode.Dpi
        };

        var tbHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var tb = new TextBox { Dock = DockStyle.Top, Text = value };
        tbHost.Controls.Add(tb);

        var ok = new Button { Text = "OK", Dock = DockStyle.Right, DialogResult = DialogResult.OK, Width = 120, Height = 56 };
        var cancel = new Button { Text = Localization.T("win.common.cancel"), Dock = DockStyle.Right, DialogResult = DialogResult.Cancel, Width = 120, Height = 56 };
        ok.MinimumSize = new Size(120, 56);
        cancel.MinimumSize = new Size(120, 56);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(22) };
        bottom.Controls.Add(cancel);
        bottom.Controls.Add(ok);

        f.Controls.Add(tbHost);
        f.Controls.Add(bottom);

        f.AcceptButton = ok;
        f.CancelButton = cancel;

        return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
    }

    private static AppConfig SetActiveProfile(AppConfig cfg, string profileId)
    {
        if (cfg.Profiles.Count == 0) return cfg;

        var id = profileId;
        if (string.IsNullOrWhiteSpace(id) || !cfg.Profiles.Any(p => p.Id == id))
            id = cfg.Profiles[0].Id;

        return cfg with { ActiveProfileId = id };
    }

    private static AppConfig UpsertProfile(AppConfig cfg, ProfileLayout profile)
    {
        var list = cfg.Profiles.ToList();
        var idx = list.FindIndex(p => p.Id == profile.Id);
        if (idx >= 0) list[idx] = profile;
        else list.Add(profile);

        var active = cfg.ActiveProfileId;
        if (string.IsNullOrWhiteSpace(active) || !list.Any(p => p.Id == active))
            active = list[0].Id;

        return cfg with { Profiles = list, ActiveProfileId = active };
    }


    protected override void ApplyLocalization()
    {
        Text = Localization.T("win.editor.title");

        _addProfile.Text = Localization.T("win.editor.addScreen");
        _dupProfile.Text = Localization.T("win.editor.duplicate");
        _renameProfile.Text = Localization.T("win.editor.rename");
        _removeProfile.Text = Localization.T("win.editor.delete");

        _addAction.Text = Localization.T("win.editor.addProgram");
        _filter.PlaceholderText = Localization.T("win.editor.search");

        if (_lblScreen != null) _lblScreen.Text = Localization.T("win.editor.screen");
        if (_lblRows != null) _lblRows.Text = Localization.T("win.editor.rows");
        if (_lblCols != null) _lblCols.Text = Localization.T("win.editor.cols");
        if (_lblTileHeight != null) _lblTileHeight.Text = Localization.T("win.editor.tileHeight");
        if (_lblIconSize != null) _lblIconSize.Text = Localization.T("win.editor.iconSize");
        if (_lblHint != null) _lblHint.Text = Localization.T("win.editor.hint");
    }
}