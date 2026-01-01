using System;
using System.Drawing;
using System.Windows.Forms;
using QRCoder;

namespace TabletDeck;

public sealed class QrForm : LocalizedForm
{
    private readonly Label _hint;
    private readonly Label _addr;
    private readonly TextBox _urlBox;
    private readonly PictureBox _picture;
    private readonly Button _regenButton;
    private readonly Button _coffeeButton;

    private readonly Action<string>? _onUrlChanged;
    private string _wsUrl;

    public QrForm(string wsUrl, Action<string>? onUrlChanged = null)
    {
        _wsUrl = wsUrl;
        _onUrlChanged = onUrlChanged;

        Width = 640;
        Height = 740;
        StartPosition = FormStartPosition.CenterScreen;

        _hint = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        _picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.CenterImage };
        _addr = new Label { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(12, 10, 12, 6) };
        _urlBox = new TextBox { Dock = DockStyle.Bottom, ReadOnly = true, Text = _wsUrl };

        _regenButton = new Button { AutoSize = true, Height = 34, FlatStyle = FlatStyle.System };
        _regenButton.Click += (_, _) => RegenerateQrHost();

        _coffeeButton = new Button { AutoSize = true, Height = 34, FlatStyle = FlatStyle.System };
        _coffeeButton.Click += (_, _) => SupportLinks.OpenBuyMeACoffee();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 6, 12, 6),
            WrapContents = false,
        };
        buttons.Controls.Add(_coffeeButton);
        buttons.Controls.Add(_regenButton);

        Controls.Add(_picture);
        Controls.Add(_urlBox);
        Controls.Add(buttons);
        Controls.Add(_addr);
        Controls.Add(_hint);

        RefreshQrImage();
        ApplyLocalization();
    }

    protected override void ApplyLocalization()
    {
        Text = Localization.T("win.qr.title");
        _hint.Text = Localization.T("win.qr.hint");
        _addr.Text = Localization.T("win.qr.address");
        _regenButton.Text = Localization.T("win.qr.regenerate");
        _coffeeButton.Text = Localization.T("win.tray.buyCoffee");
    }

    private void RegenerateQrHost()
    {
        if (!Uri.TryCreate(_wsUrl, UriKind.Absolute, out var uri))
            return;

        var currentHost = uri.Host;
        var newHost = NetUtil.GetBestLocalIpv4OrLoopback(exclude: currentHost);

        if (string.Equals(currentHost, newHost, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                Localization.T("win.qr.regenerate.noAltIp"),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var b = new UriBuilder(uri) { Host = newHost };
        _wsUrl = b.Uri.ToString();

        _urlBox.Text = _wsUrl;
        RefreshQrImage();
        _onUrlChanged?.Invoke(_wsUrl);
    }

    private void RefreshQrImage()
    {
        var old = _picture.Image;
        _picture.Image = MakeQr(_wsUrl, 10);
        old?.Dispose();
    }

    private static Bitmap MakeQr(string payload, int pixelsPerModule)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var code = new QRCode(data);
        return code.GetGraphic(pixelsPerModule);
    }
}
