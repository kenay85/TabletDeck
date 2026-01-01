using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TabletDeck;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly WsServer _server;
    private string _wsUrl;

    private readonly ToolStripMenuItem _miShowQr;
    private readonly ToolStripMenuItem _miEditor;
    private readonly ToolStripMenuItem _miCopyUrl;
    private readonly ToolStripMenuItem _miSendToAndroid;
    private readonly ToolStripMenuItem _miBuyCoffee;
    private readonly ToolStripMenuItem _miObs;
    private readonly ToolStripMenuItem _miLanguage;
    private readonly ToolStripMenuItem _miExit;

    private readonly Dictionary<string, ToolStripMenuItem> _langItems = new(StringComparer.OrdinalIgnoreCase);

    private int _isExiting;

    public TrayAppContext(WsServer server, string wsUrl)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _wsUrl = wsUrl ?? throw new ArgumentNullException(nameof(wsUrl));

        var menu = new ContextMenuStrip();

        _miShowQr = new ToolStripMenuItem("", null, (_, _) => ShowQr());
        _miEditor = new ToolStripMenuItem("", null, (_, _) => ShowEditor());
        _miCopyUrl = new ToolStripMenuItem("", null, (_, _) => Clipboard.SetText(_wsUrl));

        _miSendToAndroid = new ToolStripMenuItem("", null, async (_, _) => await SendFilesToAndroidAsync());
        _miBuyCoffee = new ToolStripMenuItem("", null, (_, _) => SupportLinks.OpenBuyMeACoffee());

        _miObs = new ToolStripMenuItem("", null, (_, _) =>
        {
            using var f = new ObsSettingsForm();
            f.ShowDialog();
        });

        _miLanguage = new ToolStripMenuItem("");
        BuildLanguageMenu();

        _miExit = new ToolStripMenuItem("", null, (_, _) => ExitApp());

        menu.Items.Add(_miShowQr);
        menu.Items.Add(_miEditor);
        menu.Items.Add(_miCopyUrl);
        menu.Items.Add(_miSendToAndroid);
        menu.Items.Add(_miBuyCoffee);
        menu.Items.Add(_miObs);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_miLanguage);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_miExit);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };

        RefreshTexts();
        UpdateLanguageChecks();

        Localization.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshTexts();
        UpdateLanguageChecks();
    }

    private void BuildLanguageMenu()
    {
        _miLanguage.DropDownItems.Clear();
        _langItems.Clear();

        foreach (var (code, nativeName) in Localization.SupportedLanguages)
        {
            var item = new ToolStripMenuItem(nativeName, null, (_, _) => Localization.SetLanguage(code));
            _miLanguage.DropDownItems.Add(item);
            _langItems[code] = item;
        }
    }

    private void RefreshTexts()
    {
        _tray.Text = Localization.T("win.tray.tooltip");

        _miShowQr.Text = Localization.T("win.tray.showQr");
        _miEditor.Text = Localization.T("win.tray.editor");
        _miCopyUrl.Text = Localization.T("win.tray.copyUrl");

        _miSendToAndroid.Text = Localization.T("win.tray.sendToAndroid");
        _miBuyCoffee.Text = Localization.T("win.tray.buyCoffee");

        _miObs.Text = Localization.T("win.tray.obs");
        _miLanguage.Text = Localization.T("win.tray.language");
        _miExit.Text = Localization.T("win.tray.exit");
    }

    private void UpdateLanguageChecks()
    {
        var active = Localization.LanguageCode;

        foreach (var (code, item) in _langItems)
            item.Checked = string.Equals(code, active, StringComparison.OrdinalIgnoreCase);
    }

    private void ShowQr()
    {
        using var f = new QrForm(_wsUrl, newUrl => _wsUrl = newUrl);
        f.ShowDialog();
    }

    private void ShowEditor()
    {
        using var f = new TileEditorForm(_server);
        f.ShowDialog();
    }

    private async Task SendFilesToAndroidAsync()
    {
        if (!_server.TryGetAnyConnectedClient(out var clientId))
        {
            MessageBox.Show(
                Localization.T("win.tray.sendToAndroid.noClient"),
                "TabletDeck",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var ofd = new OpenFileDialog
        {
            Multiselect = true,
            Title = Localization.T("win.tray.sendToAndroid.pickTitle"),
            Filter = "All files|*.*"
        };

        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        foreach (var path in ofd.FileNames)
        {
            try
            {
                await _server.SendFileToClientAsync(clientId, path, default);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{Localization.T("win.tray.sendToAndroid.failed")}\n\n{ex.Message}",
                    "TabletDeck",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }

        MessageBox.Show(
            Localization.T("win.tray.sendToAndroid.done"),
            "TabletDeck",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ExitApp()
    {
        if (Interlocked.Exchange(ref _isExiting, 1) != 0)
            return;

        _ = ShutdownAsync();
    }

    private async Task ShutdownAsync()
    {
        try
        {
            Localization.LanguageChanged -= OnLanguageChanged;
        }
        catch { }

        try
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        catch { }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _server.StopAsync().WaitAsync(timeout.Token);
        }
        catch { }

        try
        {
            Application.ExitThread();
        }
        catch { }

        // Bezpiecznik: jeœli coœ trzyma proces (np. host/websocket), nie zostawiaj procesu wisz¹cego.
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            try { Environment.Exit(0); } catch { }
            try { Process.GetCurrentProcess().Kill(entireProcessTree: true); } catch { }
        });
    }
}