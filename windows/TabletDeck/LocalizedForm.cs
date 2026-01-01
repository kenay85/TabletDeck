using System;
using System.Windows.Forms;

namespace TabletDeck;

/// <summary>
/// Form that refreshes visible texts when the app language changes.
/// </summary>
public abstract class LocalizedForm : Form
{
    protected LocalizedForm()
    {
        Localization.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyLocalization();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Localization.LanguageChanged -= OnLanguageChanged;

        base.Dispose(disposing);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action(ApplyLocalization));
            return;
        }

        ApplyLocalization();
    }

    /// <summary>Set all user-visible texts here.</summary>
    protected abstract void ApplyLocalization();
}
